using BigSchool.Domain.SharedKernel.Enums;
using BigSchool.Domain.SharedKernel.ValueObjects;
using FluentAssertions;
using Xunit;

namespace BigSchool.Domain.Tests.ValueObjects;

public class MoneyConversionTests
{
    private static readonly DateOnly RateDate = new(2026, 6, 17);

    [Fact]
    public void Create_DifferentCurrency_ComputesBaseAmount()
    {
        var original = Money.Create(100m, Currency.USD);

        var conversion = MoneyConversion.Create(original, Currency.EUR, rate: 0.92m, RateDate);

        conversion.Original.Should().Be(Money.Create(100m, Currency.USD));
        conversion.Rate.Should().Be(0.92m);
        conversion.Base.Should().Be(Money.Create(92.00m, Currency.EUR));
        conversion.RateDate.Should().Be(RateDate);
    }

    [Fact]
    public void Create_SameCurrency_RequiresRateOne_AndBaseEqualsOriginal()
    {
        var original = Money.Create(50m, Currency.EUR);

        var conversion = MoneyConversion.Create(original, Currency.EUR, rate: 1m, RateDate);

        conversion.Base.Amount.Should().Be(50.00m);
        conversion.Base.Currency.Should().Be(Currency.EUR);
    }

    [Fact]
    public void Create_SameCurrency_WithRateNotOne_Throws()
    {
        var original = Money.Create(50m, Currency.EUR);

        var act = () => MoneyConversion.Create(original, Currency.EUR, rate: 1.1m, RateDate);

        act.Should().Throw<ArgumentException>().WithMessage("*moneda base*");
    }

    [Fact]
    public void Create_WithNonPositiveRate_Throws()
    {
        var original = Money.Create(10m, Currency.USD);

        var act = () => MoneyConversion.Create(original, Currency.EUR, rate: 0m, RateDate);

        act.Should().Throw<ArgumentException>();
    }
}
