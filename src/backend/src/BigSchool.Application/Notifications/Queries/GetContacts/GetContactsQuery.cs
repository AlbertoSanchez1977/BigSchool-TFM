using BigSchool.Application.Notifications.DTOs;
using BigSchool.Application.SharedKernel.Common;
using MediatR;

namespace BigSchool.Application.Notifications.Queries.GetContacts;

public record GetContactsQuery(int Page, int PageSize) : IRequest<PagedResult<ContactListItemDto>>;
