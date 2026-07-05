using BigSchool.Application.SharedKernel.Interfaces;
using BigSchool.Domain.Finance.Enums;
using BigSchool.Domain.SharedKernel.Enums;
using Dapper;
using MediatR;

namespace BigSchool.Application.Finance.Queries.Transactions.GetByCategory;

public class GetTransactionsByCategoryQueryHandler
    : IRequestHandler<GetTransactionsByCategoryQuery, IReadOnlyList<CategoryTotalDto>>
{
    private readonly IDbConnectionFactory _dbFactory;

    public GetTransactionsByCategoryQueryHandler(IDbConnectionFactory dbFactory) => _dbFactory = dbFactory;

    private const string GETBYCATEGORY_QUERY = @"SELECT IdMainCategory, SUM(BaseAmount) AS Total
            FROM Transactions
            WHERE IdUser = @IdUser AND IdStatus <> @StatusDeleted
              AND (@Type IS NULL OR Type = @Type)
              AND (@From IS NULL OR TransactionDate >= @From)
              AND (@To   IS NULL OR TransactionDate <= @To)
            GROUP BY IdMainCategory
            ORDER BY Total DESC;";

    private sealed record Row(int IdMainCategory, decimal Total);

    public async Task<IReadOnlyList<CategoryTotalDto>> Handle(GetTransactionsByCategoryQuery request, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@IdUser", request.IdUser);
        parameters.Add("@StatusDeleted", EntityStatus.Deleted);
        parameters.Add("@Type", request.Type.HasValue ? (short?)request.Type.Value : null);
        parameters.Add("@From", request.From);
        parameters.Add("@To", request.To);

        using var conn = _dbFactory.CreateConnection();
        var rows = await conn.QueryAsync<Row>(GETBYCATEGORY_QUERY, parameters);
        return rows.Select(r => new CategoryTotalDto(r.IdMainCategory, ((MainCategory)r.IdMainCategory).ToString(), r.Total)).ToList();
    }
}
