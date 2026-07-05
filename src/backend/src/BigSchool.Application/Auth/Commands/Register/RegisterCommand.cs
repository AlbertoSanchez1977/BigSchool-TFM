using BigSchool.Application.Auth.DTOs;
using BigSchool.Domain.SharedKernel.Enums;
using MediatR;

namespace BigSchool.Application.Auth.Commands.Register;

public record RegisterCommand(string Email, string Password, string FullName, Currency BaseCurrency) : IRequest<AuthResponseDto>;
