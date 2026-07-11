using BigSchool.Domain.Finance.Enums;
using MediatR;

namespace BigSchool.Application.Finance.Queries.Transactions.GetByCategory;

public record GetTransactionsByCategoryQuery(int IdUser, TransactionType? Type, DateOnly? From, DateOnly? To)
    : IRequest<IReadOnlyList<CategoryTotalDto>>;
