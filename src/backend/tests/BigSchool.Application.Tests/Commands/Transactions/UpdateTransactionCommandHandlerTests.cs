using BigSchool.Application.Finanzas.Commands.Update;
using BigSchool.Domain.Auth.Entities;
using BigSchool.Domain.Finanzas.Entities;
using BigSchool.Domain.Finanzas.Enums;
using BigSchool.Domain.SharedKernel.Enums;
using BigSchool.Domain.SharedKernel.Exceptions;
using BigSchool.Domain.SharedKernel.Interfaces;
using BigSchool.Domain.SharedKernel.ValueObjects;
using FluentAssertions;
using Moq;
using Xunit;
using BigSchool.Application.Auth.Interfaces.Repositories;
using BigSchool.Application.Finanzas.Interfaces.Repositories;
using BigSchool.Application.SharedKernel.Interfaces.Services;

namespace BigSchool.Application.Tests.Commands.Transactions;

public class UpdateTransactionCommandHandlerTests
{
    private readonly Mock<ITransactionRepository> _txRepo = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IExchangeRateProvider> _rates = new();
    private readonly UpdateTransactionCommandHandler _handler;

    public UpdateTransactionCommandHandlerTests()
    {
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<bool>())).ReturnsAsync(1);
        _txRepo.Setup(r => r.UnitOfWork).Returns(uow.Object);
        _handler = new UpdateTransactionCommandHandler(_txRepo.Object, _userRepo.Object, _rates.Object);
    }

    private static Transaction ExistingTx(int userId = 1)
        => Transaction.Create(userId, TransactionType.Expense, MainCategory.Luxuries, null, "old",
            Money.Create(100m, Currency.EUR), Currency.EUR, 1m, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 1));

    [Fact]
    public async Task Update_ChangesAmountAndRebuildsConversion()
    {
        var tx = ExistingTx();
        _txRepo.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(tx);
        _userRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(User.Create("u@test.com", "h", "s", "User", Currency.EUR));

        var command = new UpdateTransactionCommand(
            IdTransaction: 5, IdUser: 1, Type: TransactionType.Expense,
            IdMainCategory: MainCategory.EssentialExpenses, IdSubCategory: null, Description: "new",
            TransactionDate: new DateOnly(2026, 6, 2), Amount: 60m, Currency: Currency.EUR);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.BaseAmount.Should().Be(60m);
        result.IdMainCategory.Should().Be(MainCategory.EssentialExpenses);
    }

    [Fact]
    public async Task Update_OtherUsersTransaction_ThrowsNotFound()
    {
        _txRepo.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(ExistingTx(userId: 2));

        var command = new UpdateTransactionCommand(5, 1, TransactionType.Expense, MainCategory.Luxuries,
            null, null, new DateOnly(2026, 6, 2), 60m, Currency.EUR);

        await _handler.Invoking(h => h.Handle(command, CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();
    }
}
