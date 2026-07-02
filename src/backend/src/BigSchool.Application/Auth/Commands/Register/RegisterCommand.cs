using BigSchool.Application.Auth.DTOs;
using MediatR;

namespace BigSchool.Application.Auth.Commands.Register;

public record RegisterCommand(string Email, string Password, string FullName) : IRequest<AuthResponseDto>;
