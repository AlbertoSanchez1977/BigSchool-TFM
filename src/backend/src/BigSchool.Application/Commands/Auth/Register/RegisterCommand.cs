using BigSchool.Application.DTOs.Auth;
using MediatR;

namespace BigSchool.Application.Commands.Auth.Register;

public record RegisterCommand(string Email, string Password, string FullName) : IRequest<AuthResponseDto>;
