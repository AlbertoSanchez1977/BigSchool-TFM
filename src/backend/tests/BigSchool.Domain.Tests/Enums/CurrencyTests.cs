using BigSchool.Domain.SharedKernel.Enums;
using FluentAssertions;
using Xunit;

namespace BigSchool.Domain.Tests.Enums;

public class CurrencyTests
{
    [Theory]
    [InlineData(Currency.EUR, "EUR")]
    [InlineData(Currency.USD, "USD")]
    [InlineData(Currency.GBP, "GBP")]
    [InlineData(Currency.CHF, "CHF")]
    [InlineData(Currency.JPY, "JPY")]
    public void EnumName_IsIso4217Alpha3(Currency currency, string expectedAlpha3)
    {
        currency.ToString().Should().Be(expectedAlpha3);
    }

    [Theory]
    [InlineData(Currency.EUR, (short)978)]
    [InlineData(Currency.USD, (short)840)]
    [InlineData(Currency.JPY, (short)392)]
    public void EnumValue_IsIso4217Numeric(Currency currency, short expectedNumeric)
    {
        ((short)currency).Should().Be(expectedNumeric);
    }

    [Fact]
    public void Parse_FromAlpha3_RoundTrips()
    {
        Enum.Parse<Currency>("USD").Should().Be(Currency.USD);
    }
}
