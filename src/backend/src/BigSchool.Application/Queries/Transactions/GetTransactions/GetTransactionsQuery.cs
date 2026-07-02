using BigSchool.Application.Common;
using BigSchool.Application.DTOs.Transactions;
using BigSchool.Domain.Finanzas.Enums;
using MediatR;

namespace BigSchool.Application.Queries.Transactions.GetTransactions;

public record GetTransactionsQuery(
    int IdUser,
    TransactionType? Type,
    MainCategory? IdMainCategory,
    DateOnly? From,
    DateOnly? To,
    int Page,
    int PageSize) : IRequest<PagedResult<TransactionListItemDto>>
{
    public static int NormalizePage(int page) => page < 1 ? 1 : page;

    public static int NormalizePageSize(int pageSize) => pageSize switch
    {
        <= 0 => 20,
        > 100 => 100,
        _ => pageSize
    };
}
