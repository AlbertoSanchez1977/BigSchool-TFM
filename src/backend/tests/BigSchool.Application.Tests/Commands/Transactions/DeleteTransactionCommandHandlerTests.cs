using BigSchool.Application.Commands.Transactions.Delete;
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Domain.Finanzas.Entities;
using BigSchool.Domain.Finanzas.Enums;
using BigSchool.Domain.SharedKernel.Enums;
using BigSchool.Domain.SharedKernel.Interfaces;
using BigSchool.Domain.SharedKernel.ValueObjects;
using FluentAssertions;
using Moq;
using Xunit;

namespace BigSchool.Application.Tests.Commands.Transactions;

public class DeleteTransactionCommandHandlerTests
{
    private readonly Mock<ITransactionRepository> _txRepo = new();
    private readonly DeleteTransactionCommandHandler _handler;

    public DeleteTransactionCommandHandlerTests()
    {
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<bool>())).ReturnsAsync(1);
        _txRepo.Setup(r => r.UnitOfWork).Returns(uow.Object);
        _handler = new DeleteTransactionCommandHandler(_txRepo.Object);
    }

    private static Transaction ExistingTx(int userId = 1)
        => Transaction.Create(userId, TransactionType.Expense, MainCategory.Luxuries, null, "old",
            Money.Create(100m, Currency.EUR), Currency.EUR, 1m, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 1));

    [Fact]
    public async Task Delete_SoftDeletesTransaction()
    {
        var tx = ExistingTx();
        _txRepo.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(tx);

        await _handler.Handle(new DeleteTransactionCommand(IdTransaction: 5, IdUser: 1), CancellationToken.None);

        tx.IdStatus.Should().Be(EntityStatus.Deleted);
    }
}
