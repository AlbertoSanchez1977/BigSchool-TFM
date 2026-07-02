using BigSchool.Application.Auth.DTOs;
using BigSchool.Domain.Auth.Entities;
using BigSchool.Domain.Auth.Exceptions;
using MediatR;
using BigSchool.Application.Auth.Interfaces.Repositories;
using BigSchool.Application.Auth.Interfaces.Services;

namespace BigSchool.Application.Auth.Commands.Register;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, AuthResponseDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;

    public RegisterCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtService jwtService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
    }

    public async Task<AuthResponseDto> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        if (await _userRepository.ExistsWithEmailAsync(request.Email, cancellationToken))
        {
            throw new EmailAlreadyExistsDomainException(request.Email);
        }

        var (hash, salt) = _passwordHasher.HashPassword(request.Password);
        var user = User.Create(request.Email, hash, salt, request.FullName);

        await _userRepository.AddAsync(user, cancellationToken);
        await _userRepository.UnitOfWork.SaveChangesAsync();

        var token = _jwtService.GenerateToken(user.IdUser, user.Email);

        return new AuthResponseDto(token.AccessToken, token.ExpiresAt, user.Email, user.FullName);
    }
}
