using BigSchool.Domain.Finance.Entities;
using BigSchool.Domain.SharedKernel.Exceptions;
using MediatR;
using BigSchool.Application.Finance.Interfaces.Repositories;

namespace BigSchool.Application.Finance.Commands.Delete;

public class DeleteTransactionCommandHandler : IRequestHandler<DeleteTransactionCommand>
{
    private readonly ITransactionRepository _transactionRepository;

    public DeleteTransactionCommandHandler(ITransactionRepository transactionRepository)
    {
        _transactionRepository = transactionRepository;
    }

    public async Task Handle(DeleteTransactionCommand request, CancellationToken cancellationToken)
    {
        var transaction = await _transactionRepository.GetByIdAsync(request.IdTransaction, cancellationToken);
        if (transaction is null || transaction.IdUser != request.IdUser)
            throw new NotFoundException(nameof(Transaction), request.IdTransaction);

        transaction.Delete();
        await _transactionRepository.UnitOfWork.SaveChangesAsync();
    }
}
