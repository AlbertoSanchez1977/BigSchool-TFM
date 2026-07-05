using BigSchool.Application.Finance.Queries.Transactions.GetMonthlyChart;
using BigSchool.Application.SharedKernel.Interfaces;
using BigSchool.Domain.Finance.Enums;
using BigSchool.Domain.SharedKernel.Enums;
using Dapper;
using MediatR;

namespace BigSchool.Application.Finance.Queries.Transactions.GetMonthly;

public class GetMonthlyQueryHandler : IRequestHandler<GetMonthlyQuery, IReadOnlyList<MonthlyChartPointDto>>
{
    private readonly IDbConnectionFactory _dbFactory;

    public GetMonthlyQueryHandler(IDbConnectionFactory dbFactory) => _dbFactory = dbFactory;

    private const string GETMONTHLY_QUERY = @"SELECT
                YEAR(TransactionDate)  AS Year,
                MONTH(TransactionDate) AS Month,
                COALESCE(SUM(CASE WHEN Type = @Income  THEN BaseAmount ELSE 0 END), 0) AS Income,
                COALESCE(SUM(CASE WHEN Type = @Expense THEN BaseAmount ELSE 0 END), 0) AS Expense
            FROM Transactions
            WHERE IdUser = @IdUser AND IdStatus <> @StatusDeleted
              AND (@From IS NULL OR TransactionDate >= @From)
              AND (@To   IS NULL OR TransactionDate <= @To)
              AND (@Category IS NULL OR IdMainCategory = @Category)
              AND (@Type IS NULL OR Type = @Type)
            GROUP BY YEAR(TransactionDate), MONTH(TransactionDate)
            ORDER BY Year, Month;";

    public async Task<IReadOnlyList<MonthlyChartPointDto>> Handle(GetMonthlyQuery request, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@IdUser", request.IdUser);
        parameters.Add("@StatusDeleted", EntityStatus.Deleted);
        parameters.Add("@From", request.From);
        parameters.Add("@To", request.To);
        parameters.Add("@Category", request.Category.HasValue ? (int?)request.Category.Value : null);
        parameters.Add("@Type", request.Type.HasValue ? (short?)request.Type.Value : null);
        parameters.Add("@Income", (short)TransactionType.Income);
        parameters.Add("@Expense", (short)TransactionType.Expense);

        using var conn = _dbFactory.CreateConnection();
        var rows = await conn.QueryAsync<MonthlyChartPointDto>(GETMONTHLY_QUERY, parameters);
        return rows.ToList();
    }
}
