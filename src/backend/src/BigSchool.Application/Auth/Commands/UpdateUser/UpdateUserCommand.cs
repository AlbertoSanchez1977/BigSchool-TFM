using BigSchool.Application.Auth.DTOs;
using MediatR;

namespace BigSchool.Application.Auth.Commands.UpdateUser;

public record UpdateUserCommand(int IdUser, string FullName, string? Password) : IRequest<UserProfileDto>;
