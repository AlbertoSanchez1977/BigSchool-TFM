using BigSchool.Domain.Entities;
using BigSchool.Domain.Enums;
using BigSchool.Domain.Exceptions;
using BigSchool.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace BigSchool.Domain.Tests.Entities;

public class PortfolioTests
{
    // Helper: precio de compra/venta como Money en moneda de la empresa (USD por defecto).
    private static Money Px(decimal amount, Currency ccy = Currency.USD) => Money.Create(amount, ccy);

    private static Portfolio NewPortfolio() => Portfolio.Create(1, "Mi cartera", Currency.EUR);

    [Fact]
    public void Create_InitializesRealizedPnLToZeroInBaseCurrency()
    {
        var p = Portfolio.Create(1, "  Cartera  ", Currency.EUR);

        p.IdUser.Should().Be(1);
        p.Name.Should().Be("Cartera"); // trim
        p.RealizedPnL.Amount.Should().Be(0m);
        p.RealizedPnL.Currency.Should().Be(Currency.EUR);
        p.IdStatus.Should().Be(EntityStatus.Active);
        p.Holdings.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_InvalidUser_Throws(int userId)
    {
        var act = () => Portfolio.Create(userId, "X", Currency.EUR);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_EmptyName_Throws()
    {
        var act = () => Portfolio.Create(1, "  ", Currency.EUR);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddHolding_CreatesLot_WithFrozenCostSnapshot()
    {
        var p = NewPortfolio();

        var h = p.AddHolding(companyId: 1, shares: 100m, buyPrice: Px(195m),
            baseCurrency: Currency.EUR, rate: 0.9m, buyDate: new DateOnly(2026, 1, 1),
            rateDate: new DateOnly(2026, 1, 1), notes: "lote 1");

        p.Holdings.Should().ContainSingle();
        h.Shares.Should().Be(100m);
        h.OpenShares.Should().Be(100m);
        h.AvgBuyPrice.Original.Amount.Should().Be(195m);
        h.AvgBuyPrice.Original.Currency.Should().Be(Currency.USD);
        h.AvgBuyPrice.Rate.Should().Be(0.9m);
        h.AvgBuyPrice.Base.Amount.Should().Be(175.50m); // 195 * 0.9
        h.AvgBuyPrice.Base.Currency.Should().Be(Currency.EUR);
    }

    [Fact]
    public void AddHolding_BaseCurrencyMismatch_Throws()
    {
        var p = NewPortfolio(); // base EUR
        var act = () => p.AddHolding(1, 10m, Px(195m), Currency.USD, 1m,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), null);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddHolding_SameCompanyTwice_CreatesTwoLots()
    {
        var p = NewPortfolio();
        p.AddHolding(1, 100m, Px(100m), Currency.EUR, 1m, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), null);
        p.AddHolding(1, 50m, Px(120m), Currency.EUR, 1m, new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 1), null);

        p.Holdings.Should().HaveCount(2);
    }

    [Fact]
    public void SellShares_SingleLot_PartialSell_GeneratesOneDisposal_AndAccumulatesRealized()
    {
        var p = NewPortfolio();
        // Compra EUR (rate 1) para aislar el cálculo: coste unidad base = 100
        p.AddHolding(3, 100m, Px(100m, Currency.EUR), Currency.EUR, 1m, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), null);

        var disposals = p.SellShares(companyId: 3, shares: 40m, sellPrice: Px(150m, Currency.EUR),
            baseCurrency: Currency.EUR, rate: 1m, sellDate: new DateOnly(2026, 3, 2), rateDate: new DateOnly(2026, 3, 2));

        disposals.Should().ContainSingle();
        disposals[0].Shares.Should().Be(40m);
        disposals[0].RealizedPnL.Amount.Should().Be(2000m); // (150-100)*40
        p.RealizedPnL.Amount.Should().Be(2000m);
        p.Holdings.Single().OpenShares.Should().Be(60m);
    }

    [Fact]
    public void SellShares_FIFO_ConsumesOldestLotsFirst_AcrossLots()
    {
        var p = NewPortfolio();
        // Lote A (más antiguo): 100 @ coste base 100. Lote B: 100 @ coste base 200.
        p.AddHolding(3, 100m, Px(100m, Currency.EUR), Currency.EUR, 1m, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), "A");
        p.AddHolding(3, 100m, Px(200m, Currency.EUR), Currency.EUR, 1m, new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 1), "B");

        // Vende 150 @ 250: consume 100 de A y 50 de B → 2 disposals.
        var disposals = p.SellShares(3, 150m, Px(250m, Currency.EUR), Currency.EUR, 1m,
            new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 2));

        disposals.Should().HaveCount(2);
        disposals[0].Shares.Should().Be(100m);                 // lote A completo
        disposals[0].RealizedPnL.Amount.Should().Be(15000m);   // (250-100)*100
        disposals[1].Shares.Should().Be(50m);                  // parte de lote B
        disposals[1].RealizedPnL.Amount.Should().Be(2500m);    // (250-200)*50

        var lots = p.Holdings.OrderBy(h => h.BuyDate).ToList();
        lots[0].OpenShares.Should().Be(0m);   // A cerrado
        lots[0].IsClosed.Should().BeTrue();
        lots[1].OpenShares.Should().Be(50m);  // B parcialmente abierto

        p.RealizedPnL.Amount.Should().Be(17500m); // 15000 + 2500
    }

    [Fact]
    public void SellShares_IncludesFxEffect_BetweenBuyAndSellDates()
    {
        var p = NewPortfolio(); // base EUR
        // Compra USD 100 @ rate 0.9 → coste base 90/u.
        p.AddHolding(1, 10m, Px(100m), Currency.EUR, 0.9m, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), null);
        // Vende USD 100 @ rate 1.0 (mismo precio en USD, pero el FX subió) → venta base 100/u.
        var disposals = p.SellShares(1, 10m, Px(100m), Currency.EUR, 1.0m,
            new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 2));

        // El P/L realizado captura el efecto FX: (100 - 90) * 10 = 100, aunque el precio en USD no cambió.
        disposals[0].RealizedPnL.Amount.Should().Be(100m);
        p.RealizedPnL.Amount.Should().Be(100m);
    }

    [Fact]
    public void SellShares_MoreThanAvailable_ThrowsInsufficientShares()
    {
        var p = NewPortfolio();
        p.AddHolding(3, 100m, Px(100m, Currency.EUR), Currency.EUR, 1m, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), null);

        var act = () => p.SellShares(3, 101m, Px(150m, Currency.EUR), Currency.EUR, 1m,
            new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 2));

        act.Should().Throw<InsufficientSharesDomainException>();
        p.RealizedPnL.Amount.Should().Be(0m); // no se mutó nada
    }

    [Fact]
    public void SellShares_OnlyConsidersThatCompany_OpenLots()
    {
        var p = NewPortfolio();
        p.AddHolding(1, 50m, Px(100m), Currency.EUR, 0.9m, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), null); // AAPL
        p.AddHolding(3, 50m, Px(100m, Currency.EUR), Currency.EUR, 1m, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), null); // SAN

        // Vender 60 de SAN (id 3) debe fallar aunque haya 50 de AAPL: solo cuentan los lotes de la misma Company.
        var act = () => p.SellShares(3, 60m, Px(150m, Currency.EUR), Currency.EUR, 1m,
            new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 2));
        act.Should().Throw<InsufficientSharesDomainException>();
    }

    [Fact]
    public void UpdateHolding_UpdatesNotes()
    {
        var p = NewPortfolio();
        var h = p.AddHolding(1, 10m, Px(100m), Currency.EUR, 0.9m, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), "old");

        p.UpdateHolding(h.IdHolding, "new notes");

        // IdHolding es 0 en memoria (no persistido); UpdateHolding lo localiza igual por referencia de id 0.
        p.Holdings.Single().Notes.Should().Be("new notes");
    }

    [Fact]
    public void UpdateHolding_UnknownId_ThrowsNotFound()
    {
        var p = NewPortfolio();
        var act = () => p.UpdateHolding(999, "x");
        act.Should().Throw<NotFoundException>();
    }

    [Fact]
    public void DeleteHolding_SoftDeletesLotAndDisposals_AndReversesRealized()
    {
        var p = NewPortfolio();
        var h = p.AddHolding(3, 100m, Px(100m, Currency.EUR), Currency.EUR, 1m, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), null);
        p.SellShares(3, 40m, Px(150m, Currency.EUR), Currency.EUR, 1m, new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 2));
        p.RealizedPnL.Amount.Should().Be(2000m);

        p.DeleteHolding(h.IdHolding);

        p.Holdings.Single().IdStatus.Should().Be(EntityStatus.Deleted);
        p.Holdings.Single().Disposals.Should().OnlyContain(d => d.IdStatus == EntityStatus.Deleted);
        p.RealizedPnL.Amount.Should().Be(0m); // realizado revertido
    }
}
