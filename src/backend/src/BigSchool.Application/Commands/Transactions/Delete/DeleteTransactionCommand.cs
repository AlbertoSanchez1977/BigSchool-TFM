using MediatR;

namespace BigSchool.Application.Commands.Transactions.Delete;

public record DeleteTransactionCommand(int IdTransaction, int IdUser) : IRequest;
