using BigSchool.Domain.Entities;
using BigSchool.Domain.Enums;
using BigSchool.Domain.SharedKernel.Enums;
using BigSchool.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace BigSchool.Domain.Tests.Entities;
public class CompanyTests
{
    private static readonly DateOnly D1 = new(2026, 1, 2);
    private static readonly DateOnly D2 = new(2026, 3, 2);

    private static Company NewCompany()
        => Company.Create("Apple Inc.", "aapl", Sector.Technology, Market.NASDAQ, Currency.USD);

    [Fact]
    public void Create_NormalizesTicker_AndSetsActive()
    {
        var company = NewCompany();

        company.Name.Should().Be("Apple Inc.");
        company.Ticker.Should().Be("AAPL");            // normaliza a mayúsculas
        company.Sector.Should().Be(Sector.Technology);
        company.Market.Should().Be(Market.NASDAQ);
        company.Currency.Should().Be(Currency.USD);
        company.IdStatus.Should().Be(EntityStatus.Active);
        company.Valuations.Should().BeEmpty();
    }

    [Theory]
    [InlineData("", "AAPL")]
    [InlineData("Apple", "")]
    public void Create_WithEmptyNameOrTicker_Throws(string name, string ticker)
    {
        var act = () => Company.Create(name, ticker, (Sector?)null, (Market?)null, Currency.USD);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddValuation_AddsPriceInCompanyCurrency()
    {
        var company = NewCompany();

        var valuation = company.AddValuation(195.50m, D1, "manual");

        company.Valuations.Should().ContainSingle();
        valuation.Price.Amount.Should().Be(195.50m);
        valuation.Price.Currency.Should().Be(Currency.USD);   // hereda la moneda de la empresa
        valuation.Date.Should().Be(D1);
        valuation.Source.Should().Be("manual");
        valuation.IdStatus.Should().Be(EntityStatus.Active);
    }

    [Fact]
    public void AddValuation_NonPositivePrice_Throws()
    {
        var company = NewCompany();

        var act = () => company.AddValuation(0m, D1, null);

        act.Should().Throw<ArgumentException>().WithMessage("*precio*");
    }

    [Fact]
    public void AddValuation_DuplicateDate_Throws()
    {
        var company = NewCompany();
        company.AddValuation(195.50m, D1, null);

        var act = () => company.AddValuation(200m, D1, null);

        act.Should().Throw<DuplicateValuationDomainException>();
    }

    [Fact]
    public void AddValuation_DifferentDates_BothAdded()
    {
        var company = NewCompany();
        company.AddValuation(195.50m, D1, null);
        company.AddValuation(210m, D2, null);

        company.Valuations.Should().HaveCount(2);
    }
}
