using BigSchool.Domain.Finance.Enums;
using BigSchool.Domain.SharedKernel.Enums;
using Dapper;
using MediatR;
using BigSchool.Application.SharedKernel.Interfaces;

namespace BigSchool.Application.Finance.Queries.Transactions.GetTransactionSummary;

public class GetTransactionSummaryQueryHandler : IRequestHandler<GetTransactionSummaryQuery, TransactionSummaryDto>
{
    private readonly IDbConnectionFactory _dbFactory;

    public GetTransactionSummaryQueryHandler(IDbConnectionFactory dbFactory) => _dbFactory = dbFactory;

    // Consolidación siempre sobre BaseAmount (moneda base del usuario).
    private const string GETTRANSACTIONSUMMARY_QUERY = @"SELECT
                COALESCE(SUM(CASE WHEN Type = @Income  THEN BaseAmount ELSE 0 END), 0) AS TotalIncome,
                COALESCE(SUM(CASE WHEN Type = @Expense THEN BaseAmount ELSE 0 END), 0) AS TotalExpense,
                COALESCE(MAX(BaseCurrency), '') AS BaseCurrency
            FROM Transactions
            WHERE IdUser = @IdUser AND IdStatus <> @StatusDeleted
              AND (@From IS NULL OR TransactionDate >= @From)
              AND (@To IS NULL OR TransactionDate <= @To);";

    public async Task<TransactionSummaryDto> Handle(GetTransactionSummaryQuery request, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@IdUser", request.IdUser);
        parameters.Add("@StatusDeleted", EntityStatus.Deleted);
        parameters.Add("@Income", (short)TransactionType.Income);
        parameters.Add("@Expense", (short)TransactionType.Expense);
        parameters.Add("@From", request.From);
        parameters.Add("@To", request.To);

        using var conn = _dbFactory.CreateConnection();
        var row = await conn.QuerySingleAsync<(decimal TotalIncome, decimal TotalExpense, string BaseCurrency)>(
            GETTRANSACTIONSUMMARY_QUERY, parameters);

        return new TransactionSummaryDto(
            row.TotalIncome, row.TotalExpense, row.TotalIncome - row.TotalExpense, row.BaseCurrency);
    }
}
