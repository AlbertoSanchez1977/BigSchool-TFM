using MediatR;

namespace BigSchool.Application.Finance.Commands.Delete;

public record DeleteTransactionCommand(int IdTransaction, int IdUser) : IRequest;
