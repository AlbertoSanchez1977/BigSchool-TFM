using BigSchool.Domain.SharedKernel.Enums;
using BigSchool.Domain.SharedKernel.ValueObjects;
using FluentAssertions;
using Xunit;

namespace BigSchool.Domain.Tests.ValueObjects;

public class MoneyTests
{
    [Fact]
    public void Create_WithValidData_SetsAmountAndCurrency()
    {
        var money = Money.Create(100.50m, Currency.EUR);

        money.Amount.Should().Be(100.50m);
        money.Currency.Should().Be(Currency.EUR);
    }

    [Fact]
    public void Create_RoundsToTwoDecimals_BankersRounding()
    {
        Money.Create(10.005m, Currency.EUR).Amount.Should().Be(10.00m);
        Money.Create(10.015m, Currency.EUR).Amount.Should().Be(10.02m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.01)]
    [InlineData(999999.99)]
    public void Create_AllowsZeroAndPositive(decimal amount)
    {
        var act = () => Money.Create(amount, Currency.USD);
        act.Should().NotThrow();
    }

    [Fact]
    public void Equality_IsByValue()
    {
        Money.Create(10m, Currency.EUR).Should().Be(Money.Create(10m, Currency.EUR));
        Money.Create(10m, Currency.EUR).Should().NotBe(Money.Create(10m, Currency.USD));
    }
}
