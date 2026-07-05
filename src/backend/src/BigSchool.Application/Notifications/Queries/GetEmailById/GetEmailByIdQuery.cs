using BigSchool.Application.Notifications.DTOs;
using MediatR;

namespace BigSchool.Application.Notifications.Queries.GetEmailById;

public record GetEmailByIdQuery(int IdEmailLog, int IdUser) : IRequest<EmailLogDto?>;
