using BigSchool.Application.DTOs.Transactions;
using BigSchool.Application.Interfaces;
using BigSchool.Domain.Enums;
using Dapper;
using MediatR;

namespace BigSchool.Application.Queries.Transactions.GetTransactionById;

public class GetTransactionByIdQueryHandler : IRequestHandler<GetTransactionByIdQuery, TransactionListItemDto?>
{
    private readonly IDbConnectionFactory _dbFactory;

    public GetTransactionByIdQueryHandler(IDbConnectionFactory dbFactory) => _dbFactory = dbFactory;

    private const string GETTRANSACTIONBYID_QUERY = @"SELECT IdTransaction, Type, IdMainCategory, IdSubCategory,
                                                             Description, TransactionDate, OriginalAmount,
                                                             OriginalCurrency, ExchangeRate, BaseAmount,
                                                             BaseCurrency, RateDate
                                                      FROM Transactions
                                                      WHERE IdTransaction = @IdTransaction
                                                        AND IdUser = @IdUser
                                                        AND IdStatus <> @StatusDeleted
                                                      LIMIT 1;";

    public async Task<TransactionListItemDto?> Handle(GetTransactionByIdQuery request, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@IdTransaction", request.IdTransaction);
        parameters.Add("@IdUser", request.IdUser);
        parameters.Add("@StatusDeleted", EntityStatus.Deleted);

        using var conn = _dbFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<TransactionListItemDto>(GETTRANSACTIONBYID_QUERY, parameters);
    }
}
