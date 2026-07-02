using BigSchool.Application.Interfaces;
using BigSchool.Domain.Finanzas.Enums;
using BigSchool.Domain.SharedKernel.Enums;
using Dapper;
using MediatR;

namespace BigSchool.Application.Queries.Transactions.GetMonthlyChart;

public class GetMonthlyChartQueryHandler : IRequestHandler<GetMonthlyChartQuery, IReadOnlyList<MonthlyChartPointDto>>
{
    private readonly IDbConnectionFactory _dbFactory;

    public GetMonthlyChartQueryHandler(IDbConnectionFactory dbFactory) => _dbFactory = dbFactory;

    private const string GETMONTHLYCHART_QUERY = @"SELECT
                YEAR(TransactionDate)  AS Year,
                MONTH(TransactionDate) AS Month,
                COALESCE(SUM(CASE WHEN Type = @Income  THEN BaseAmount ELSE 0 END), 0) AS Income,
                COALESCE(SUM(CASE WHEN Type = @Expense THEN BaseAmount ELSE 0 END), 0) AS Expense
            FROM Transactions
            WHERE IdUser = @IdUser AND IdStatus <> @StatusDeleted AND YEAR(TransactionDate) = @Year
            GROUP BY YEAR(TransactionDate), MONTH(TransactionDate)
            ORDER BY Month;";

    public async Task<IReadOnlyList<MonthlyChartPointDto>> Handle(
        GetMonthlyChartQuery request, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@IdUser", request.IdUser);
        parameters.Add("@StatusDeleted", EntityStatus.Deleted);
        parameters.Add("@Year", request.Year);
        parameters.Add("@Income", (short)TransactionType.Income);
        parameters.Add("@Expense", (short)TransactionType.Expense);

        using var conn = _dbFactory.CreateConnection();
        var rows = await conn.QueryAsync<MonthlyChartPointDto>(GETMONTHLYCHART_QUERY, parameters);
        return rows.ToList();
    }
}
