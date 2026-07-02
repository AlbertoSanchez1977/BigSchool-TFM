using BigSchool.Application.Auth.DTOs;
using MediatR;

namespace BigSchool.Application.Auth.Commands.Login;

public record LoginCommand(string Email, string Password) : IRequest<AuthResponseDto>;
