using BigSchool.Application.SharedKernel.Common;
using BigSchool.Application.Finance.DTOs;
using BigSchool.Domain.Finance.Enums;
using MediatR;

namespace BigSchool.Application.Finance.Queries.Transactions.GetTransactions;

public record GetTransactionsQuery(
    int IdUser,
    TransactionType? Type,
    MainCategory? IdMainCategory,
    DateOnly? From,
    DateOnly? To,
    int Page,
    int PageSize) : IRequest<PagedResult<TransactionListItemDto>>;
