using BigSchool.Application.Finance.DTOs;
using MediatR;

namespace BigSchool.Application.Finance.Queries.Transactions.GetTransactionById;

public record GetTransactionByIdQuery(int IdTransaction, int IdUser) : IRequest<TransactionListItemDto?>;
