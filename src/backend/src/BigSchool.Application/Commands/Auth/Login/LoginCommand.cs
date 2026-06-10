using BigSchool.Application.DTOs.Auth;
using MediatR;

namespace BigSchool.Application.Commands.Auth.Login;

public record LoginCommand(string Email, string Password) : IRequest<AuthResponseDto>;
