using BigSchool.Domain.Entities;
using BigSchool.Domain.Enums;
using BigSchool.Domain.Events;
using BigSchool.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace BigSchool.Domain.Tests.Entities;

public class TransactionTests
{
    private static readonly DateOnly TxDate = new(2026, 6, 17);

    private static Transaction CreateUsd()
        => Transaction.Create(
            userId: 1,
            type: TransactionType.Expense,
            category: MainCategory.Luxuries,
            subCategoryId: 11,
            description: "Cena en Londres",
            original: Money.Create(100m, Currency.USD),
            baseCurrency: Currency.EUR,
            rate: 0.92m,
            transactionDate: TxDate,
            rateDate: TxDate);

    [Fact]
    public void Create_DifferentCurrency_BuildsConversionSnapshot()
    {
        var tx = CreateUsd();

        tx.IdUser.Should().Be(1);
        tx.Type.Should().Be(TransactionType.Expense);
        tx.IdMainCategory.Should().Be(MainCategory.Luxuries);
        tx.IdSubCategory.Should().Be(11);
        tx.TransactionDate.Should().Be(TxDate);
        tx.Conversion.Original.Should().Be(Money.Create(100m, Currency.USD));
        tx.Conversion.Rate.Should().Be(0.92m);
        tx.Conversion.Base.Should().Be(Money.Create(92.00m, Currency.EUR));
        tx.IdStatus.Should().Be(EntityStatus.Active);
    }

    [Fact]
    public void Create_SameCurrency_RateOne_BaseEqualsOriginal()
    {
        var tx = Transaction.Create(1, TransactionType.Income, MainCategory.Salary, null, "Nómina",
            Money.Create(2000m, Currency.EUR), Currency.EUR, rate: 1m, TxDate, TxDate);

        tx.Conversion.Base.Should().Be(Money.Create(2000m, Currency.EUR));
    }

    [Fact]
    public void Create_RaisesTransactionCreatedEvent()
    {
        var tx = CreateUsd();

        tx.DomainEvents.Should().ContainSingle(e => e is TransactionCreatedEvent);
    }

    [Fact]
    public void Create_WithNonPositiveAmount_Throws()
    {
        var act = () => Transaction.Create(1, TransactionType.Expense, MainCategory.Luxuries, null, null,
            Money.Create(0m, Currency.EUR), Currency.EUR, 1m, TxDate, TxDate);

        act.Should().Throw<ArgumentException>().WithMessage("*importe*");
    }

    [Fact]
    public void Update_RebuildsSnapshot_AndSetsUpdatedAt()
    {
        var tx = CreateUsd();

        tx.Update(TransactionType.Expense, MainCategory.EssentialExpenses, null, "Corregido",
            Money.Create(50m, Currency.USD), Currency.EUR, rate: 0.90m, TxDate, TxDate);

        tx.IdMainCategory.Should().Be(MainCategory.EssentialExpenses);
        tx.Conversion.Base.Should().Be(Money.Create(45.00m, Currency.EUR));
        tx.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Delete_SetsStatusDeleted()
    {
        var tx = CreateUsd();

        tx.Delete();

        tx.IdStatus.Should().Be(EntityStatus.Deleted);
    }
}
