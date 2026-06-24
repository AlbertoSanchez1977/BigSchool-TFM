using BigSchool.Application.Commands.Transactions.Create;
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Application.Interfaces.Services;
using BigSchool.Domain.Entities;
using BigSchool.Domain.Enums;
using BigSchool.Domain.Exceptions;
using BigSchool.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace BigSchool.Application.Tests.Commands.Transactions;

public class CreateTransactionCommandHandlerTests
{
    private readonly Mock<ITransactionRepository> _txRepo = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IExchangeRateProvider> _rates = new();
    private readonly CreateTransactionCommandHandler _handler;

    public CreateTransactionCommandHandlerTests()
    {
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<bool>())).ReturnsAsync(1);
        _txRepo.Setup(r => r.UnitOfWork).Returns(uow.Object);

        _handler = new CreateTransactionCommandHandler(_txRepo.Object, _userRepo.Object, _rates.Object);
    }

    private static User UserWithBase(Currency baseCurrency)
        => User.Create("u@test.com", "h", "s", "User", baseCurrency);

    [Fact]
    public async Task Handle_ForeignCurrency_ResolvesRateAndConverts()
    {
        _userRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserWithBase(Currency.EUR));
        _rates.Setup(r => r.GetRateAsync(Currency.USD, Currency.EUR, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0.92m);

        var command = new CreateTransactionCommand(
            IdUser: 1, Type: TransactionType.Expense, IdMainCategory: MainCategory.Luxuries,
            IdSubCategory: null, Description: "Cena", TransactionDate: new DateOnly(2026, 6, 17),
            Amount: 100m, Currency: Currency.USD);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.OriginalCurrency.Should().Be(Currency.USD);
        result.BaseAmount.Should().Be(92.00m);
        result.BaseCurrency.Should().Be(Currency.EUR);
        _txRepo.Verify(r => r.AddAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoCurrency_DefaultsToUserBaseCurrency_RateOne()
    {
        _userRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserWithBase(Currency.EUR));

        var command = new CreateTransactionCommand(
            IdUser: 1, Type: TransactionType.Income, IdMainCategory: MainCategory.Salary,
            IdSubCategory: null, Description: "Nómina", TransactionDate: new DateOnly(2026, 6, 1),
            Amount: 2000m, Currency: null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.OriginalCurrency.Should().Be(Currency.EUR);
        result.ExchangeRate.Should().Be(1m);
        result.BaseAmount.Should().Be(2000m);
        _rates.Verify(r => r.GetRateAsync(It.IsAny<Currency>(), It.IsAny<Currency>(),
            It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UnknownUser_ThrowsNotFound()
    {
        _userRepo.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var command = new CreateTransactionCommand(
            IdUser: 99, Type: TransactionType.Expense, IdMainCategory: MainCategory.Luxuries,
            IdSubCategory: null, Description: null, TransactionDate: new DateOnly(2026, 6, 17),
            Amount: 10m, Currency: Currency.EUR);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
