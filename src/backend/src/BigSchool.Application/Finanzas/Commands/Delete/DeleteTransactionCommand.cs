using MediatR;

namespace BigSchool.Application.Finanzas.Commands.Delete;

public record DeleteTransactionCommand(int IdTransaction, int IdUser) : IRequest;
