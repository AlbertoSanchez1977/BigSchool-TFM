using BigSchool.Application.Notifications.DTOs;
using BigSchool.Application.SharedKernel.Common;
using BigSchool.Application.SharedKernel.Interfaces;
using BigSchool.Domain.SharedKernel.Enums;
using Dapper;
using MediatR;

namespace BigSchool.Application.Notifications.Queries.GetContacts;

public class GetContactsQueryHandler : IRequestHandler<GetContactsQuery, PagedResult<ContactListItemDto>>
{
    private readonly IDbConnectionFactory _dbFactory;

    public GetContactsQueryHandler(IDbConnectionFactory dbFactory) => _dbFactory = dbFactory;

    private const string GETCONTACTS_QUERY = @"SELECT COUNT(*) FROM Contacts WHERE IdStatus <> @StatusDeleted;
            SELECT IdContact, FullName, Email, Message, CreatedAt
            FROM Contacts WHERE IdStatus <> @StatusDeleted
            ORDER BY CreatedAt DESC, IdContact DESC
            LIMIT @PageSize OFFSET @Offset;";

    public async Task<PagedResult<ContactListItemDto>> Handle(GetContactsQuery request, CancellationToken cancellationToken)
    {
        var page = Pagination.NormalizePage(request.Page);
        var pageSize = Pagination.NormalizePageSize(request.PageSize);

        var parameters = new DynamicParameters();
        parameters.Add("@StatusDeleted", EntityStatus.Deleted);
        parameters.Add("@PageSize", pageSize);
        parameters.Add("@Offset", (page - 1) * pageSize);

        using var conn = _dbFactory.CreateConnection();
        using var multi = await conn.QueryMultipleAsync(GETCONTACTS_QUERY, parameters);
        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<ContactListItemDto>()).ToList();
        return new PagedResult<ContactListItemDto>(items, page, pageSize, total);
    }
}
