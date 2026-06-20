using BigSchool.Application.DTOs.Transactions;
using MediatR;

namespace BigSchool.Application.Queries.Transactions.GetTransactionById;

public record GetTransactionByIdQuery(int IdTransaction, int IdUser) : IRequest<TransactionListItemDto?>;
