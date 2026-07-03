using BigSchool.Application.SharedKernel.Common;
using BigSchool.Application.Finanzas.DTOs;
using BigSchool.Domain.Finanzas.Enums;
using MediatR;

namespace BigSchool.Application.Finanzas.Queries.Transactions.GetTransactions;

public record GetTransactionsQuery(
    int IdUser,
    TransactionType? Type,
    MainCategory? IdMainCategory,
    DateOnly? From,
    DateOnly? To,
    int Page,
    int PageSize) : IRequest<PagedResult<TransactionListItemDto>>;
