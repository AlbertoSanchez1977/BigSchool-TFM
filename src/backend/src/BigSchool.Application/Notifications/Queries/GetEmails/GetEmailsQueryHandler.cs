using BigSchool.Application.Notifications.DTOs;
using BigSchool.Application.SharedKernel.Common;
using BigSchool.Application.SharedKernel.Interfaces;
using BigSchool.Domain.SharedKernel.Enums;
using Dapper;
using MediatR;

namespace BigSchool.Application.Notifications.Queries.GetEmails;

public class GetEmailsQueryHandler : IRequestHandler<GetEmailsQuery, PagedResult<EmailLogListItemDto>>
{
    private readonly IDbConnectionFactory _dbFactory;

    public GetEmailsQueryHandler(IDbConnectionFactory dbFactory) => _dbFactory = dbFactory;

    private const string EMAILS_WHERE = @"WHERE IdStatus <> @StatusDeleted AND (IdUser = @IdUser OR IdUser IS NULL)";

    private const string GETEMAILS_QUERY = @"SELECT COUNT(*) FROM EmailLogs " + EMAILS_WHERE + @";
            SELECT IdEmailLog, IdUser, Recipient, Subject, Type, SentAt
            FROM EmailLogs " + EMAILS_WHERE + @"
            ORDER BY SentAt DESC, IdEmailLog DESC
            LIMIT @PageSize OFFSET @Offset;";

    public async Task<PagedResult<EmailLogListItemDto>> Handle(GetEmailsQuery request, CancellationToken cancellationToken)
    {
        var page = Pagination.NormalizePage(request.Page);
        var pageSize = Pagination.NormalizePageSize(request.PageSize);

        var parameters = new DynamicParameters();
        parameters.Add("@StatusDeleted", EntityStatus.Deleted);
        parameters.Add("@IdUser", request.IdUser);
        parameters.Add("@PageSize", pageSize);
        parameters.Add("@Offset", (page - 1) * pageSize);

        using var conn = _dbFactory.CreateConnection();
        using var multi = await conn.QueryMultipleAsync(GETEMAILS_QUERY, parameters);
        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<EmailLogListItemDto>()).ToList();
        return new PagedResult<EmailLogListItemDto>(items, page, pageSize, total);
    }
}
