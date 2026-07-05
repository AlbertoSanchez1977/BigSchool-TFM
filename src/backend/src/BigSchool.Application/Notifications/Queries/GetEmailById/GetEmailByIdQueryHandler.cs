using BigSchool.Application.Notifications.DTOs;
using BigSchool.Application.SharedKernel.Interfaces;
using BigSchool.Domain.SharedKernel.Enums;
using Dapper;
using MediatR;

namespace BigSchool.Application.Notifications.Queries.GetEmailById;

public class GetEmailByIdQueryHandler : IRequestHandler<GetEmailByIdQuery, EmailLogDto?>
{
    private readonly IDbConnectionFactory _dbFactory;

    public GetEmailByIdQueryHandler(IDbConnectionFactory dbFactory) => _dbFactory = dbFactory;

    private const string GETEMAILBYID_QUERY = @"SELECT IdEmailLog, IdUser, Recipient, Subject, Body, Type, SentAt
            FROM EmailLogs
            WHERE IdEmailLog = @IdEmailLog AND IdStatus <> @StatusDeleted
              AND (IdUser = @IdUser OR IdUser IS NULL)
            LIMIT 1;";

    public async Task<EmailLogDto?> Handle(GetEmailByIdQuery request, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@IdEmailLog", request.IdEmailLog);
        parameters.Add("@IdUser", request.IdUser);
        parameters.Add("@StatusDeleted", EntityStatus.Deleted);

        using var conn = _dbFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<EmailLogDto?>(GETEMAILBYID_QUERY, parameters);
    }
}
