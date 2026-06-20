using BigSchool.Application.Common;
using BigSchool.Application.DTOs.Transactions;
using BigSchool.Application.Interfaces;
using BigSchool.Domain.Enums;
using Dapper;
using MediatR;

namespace BigSchool.Application.Queries.Transactions.GetTransactions;

public class GetTransactionsQueryHandler : IRequestHandler<GetTransactionsQuery, PagedResult<TransactionListItemDto>>
{
    private readonly IDbConnectionFactory _dbFactory;

    public GetTransactionsQueryHandler(IDbConnectionFactory dbFactory) => _dbFactory = dbFactory;

    private const string TRANSACTIONS_WHERE = @"WHERE IdUser = @IdUser AND IdStatus <> @StatusDeleted
                                                  AND (@Type IS NULL OR Type = @Type)
                                                  AND (@IdMainCategory IS NULL OR IdMainCategory = @IdMainCategory)
                                                  AND (@From IS NULL OR TransactionDate >= @From)
                                                  AND (@To IS NULL OR TransactionDate <= @To)";

    private const string GETTRANSACTIONS_QUERY = @"SELECT COUNT(*) FROM Transactions " + TRANSACTIONS_WHERE + @";
            SELECT IdTransaction, Type, IdMainCategory, IdSubCategory, Description, TransactionDate,
                   OriginalAmount, OriginalCurrency, ExchangeRate, BaseAmount, BaseCurrency, RateDate
            FROM Transactions " + TRANSACTIONS_WHERE + @"
            ORDER BY TransactionDate DESC, IdTransaction DESC
            LIMIT @PageSize OFFSET @Offset;";

    public async Task<PagedResult<TransactionListItemDto>> Handle(
        GetTransactionsQuery request, CancellationToken cancellationToken)
    {
        var page = GetTransactionsQuery.NormalizePage(request.Page);
        var pageSize = GetTransactionsQuery.NormalizePageSize(request.PageSize);

        var parameters = new DynamicParameters();
        parameters.Add("@IdUser", request.IdUser);
        parameters.Add("@StatusDeleted", EntityStatus.Deleted);
        parameters.Add("@Type", request.Type.HasValue ? (short?)request.Type.Value : null);
        parameters.Add("@IdMainCategory", request.IdMainCategory.HasValue ? (int?)request.IdMainCategory.Value : null);
        parameters.Add("@From", request.From);
        parameters.Add("@To", request.To);
        parameters.Add("@PageSize", pageSize);
        parameters.Add("@Offset", (page - 1) * pageSize);

        using var conn = _dbFactory.CreateConnection();
        using var multi = await conn.QueryMultipleAsync(GETTRANSACTIONS_QUERY, parameters);
        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<TransactionListItemDto>()).ToList();

        return new PagedResult<TransactionListItemDto>(items, page, pageSize, total);
    }
}
