using BigSchool.Application.Auth.DTOs;
using MediatR;

namespace BigSchool.Application.Auth.Commands.Refresh;

public record RefreshTokenCommand(string AccessToken) : IRequest<AuthResponseDto>;
