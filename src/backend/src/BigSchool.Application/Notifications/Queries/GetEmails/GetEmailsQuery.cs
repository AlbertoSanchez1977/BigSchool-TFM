using BigSchool.Application.Notifications.DTOs;
using BigSchool.Application.SharedKernel.Common;
using MediatR;

namespace BigSchool.Application.Notifications.Queries.GetEmails;

public record GetEmailsQuery(int IdUser, int Page, int PageSize) : IRequest<PagedResult<EmailLogListItemDto>>;
