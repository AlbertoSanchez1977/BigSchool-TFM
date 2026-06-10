using BigSchool.Application.DTOs.Auth;
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Application.Interfaces.Services;
using BigSchool.Domain.Exceptions;
using MediatR;

namespace BigSchool.Application.Commands.Auth.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResponseDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;

    public LoginCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtService jwtService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
    }

    public async Task<AuthResponseDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);

        if (user is null || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            throw new InvalidCredentialsDomainException();
        }

        user.UpdateLastLogin();
        await _userRepository.UnitOfWork.SaveChangesAsync();

        var token = _jwtService.GenerateToken(user.IdUser, user.Email);

        return new AuthResponseDto(token.AccessToken, token.ExpiresAt, user.Email, user.FullName);
    }
}
