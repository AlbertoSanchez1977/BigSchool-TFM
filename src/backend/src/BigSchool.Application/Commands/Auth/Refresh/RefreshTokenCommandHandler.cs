using BigSchool.Application.DTOs.Auth;
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Application.Interfaces.Services;
using MediatR;

namespace BigSchool.Application.Commands.Auth.Refresh;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthResponseDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IJwtService _jwtService;

    public RefreshTokenCommandHandler(IUserRepository userRepository, IJwtService jwtService)
    {
        _userRepository = userRepository;
        _jwtService = jwtService;
    }

    public async Task<AuthResponseDto> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var userId = _jwtService.ExtractUserId(request.AccessToken);
        if (userId is null)
            throw new UnauthorizedAccessException("Token de acceso inválido.");

        var user = await _userRepository.GetByIdAsync(userId.Value, cancellationToken);
        if (user is null)
            throw new UnauthorizedAccessException("La sesión ya no es válida.");

        var token = _jwtService.GenerateToken(user.IdUser, user.Email);

        return new AuthResponseDto(token.AccessToken, token.ExpiresAt, user.Email, user.FullName);
    }
}
