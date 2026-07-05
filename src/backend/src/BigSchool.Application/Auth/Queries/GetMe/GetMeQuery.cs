using BigSchool.Application.Auth.DTOs;
using MediatR;

namespace BigSchool.Application.Auth.Queries.GetMe;

public record GetMeQuery(int IdUser) : IRequest<UserProfileDto?>;
