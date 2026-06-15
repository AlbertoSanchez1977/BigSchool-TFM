using BigSchool.Application.DTOs.Auth;
using MediatR;

namespace BigSchool.Application.Commands.Auth.Refresh;

public record RefreshTokenCommand(string AccessToken) : IRequest<AuthResponseDto>;
