using BigSchool.Application.Auth.DTOs;
using BigSchool.Application.SharedKernel.Interfaces;
using BigSchool.Domain.SharedKernel.Enums;
using Dapper;
using MediatR;

namespace BigSchool.Application.Auth.Queries.GetMe;

public class GetMeQueryHandler : IRequestHandler<GetMeQuery, UserProfileDto?>
{
    private readonly IDbConnectionFactory _dbFactory;

    public GetMeQueryHandler(IDbConnectionFactory dbFactory) => _dbFactory = dbFactory;

    private const string GETME_QUERY = @"SELECT IdUser, Email, FullName, BaseCurrency, LastLoginDate
                                         FROM Users
                                         WHERE IdUser = @IdUser AND IdStatus <> @StatusDeleted
                                         LIMIT 1;";

    public async Task<UserProfileDto?> Handle(GetMeQuery request, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@IdUser", request.IdUser);
        parameters.Add("@StatusDeleted", EntityStatus.Deleted);

        using var conn = _dbFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<UserProfileDto?>(GETME_QUERY, parameters);
    }
}
