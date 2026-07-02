using BigSchool.Application.Finanzas.DTOs;
using MediatR;

namespace BigSchool.Application.Finanzas.Queries.Transactions.GetTransactionById;

public record GetTransactionByIdQuery(int IdTransaction, int IdUser) : IRequest<TransactionListItemDto?>;
