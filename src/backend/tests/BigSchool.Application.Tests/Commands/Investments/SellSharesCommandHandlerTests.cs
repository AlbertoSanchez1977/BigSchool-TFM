using BigSchool.Application.Investments.Commands.SellShares;
using BigSchool.Domain.Investments.Entities;
using BigSchool.Domain.Investments.Enums;
using BigSchool.Domain.SharedKernel.Enums;
using BigSchool.Domain.Investments.Exceptions;
using BigSchool.Domain.SharedKernel.Exceptions;
using BigSchool.Domain.SharedKernel.Interfaces;
using BigSchool.Domain.SharedKernel.ValueObjects;
using FluentAssertions;
using Moq;
using Xunit;
using BigSchool.Application.Investments.Interfaces.Repositories;
using BigSchool.Application.SharedKernel.Interfaces.Services;

namespace BigSchool.Application.Tests.Commands.Investments;

public class SellSharesCommandHandlerTests
{
    private readonly Mock<IPortfolioRepository> _portfolios = new();
    private readonly Mock<ICompanyRepository> _companies = new();
    private readonly Mock<IExchangeRateProvider> _rates = new();
    private readonly SellSharesCommandHandler _handler;

    public SellSharesCommandHandlerTests()
    {
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<bool>())).ReturnsAsync(1);
        _portfolios.Setup(r => r.UnitOfWork).Returns(uow.Object);
        _handler = new SellSharesCommandHandler(_portfolios.Object, _companies.Object, _rates.Object);
    }

    // Cartera EUR con dos lotes de SAN (EUR, rate 1) para FIFO determinista sin proveedor.
    private Portfolio TwoLotPortfolioSan()
    {
        var p = Portfolio.Create(7, "P", Currency.EUR);
        p.AddHolding(3, 100m, Money.Create(100m, Currency.EUR), Currency.EUR, 1m, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), "A");
        p.AddHolding(3, 100m, Money.Create(200m, Currency.EUR), Currency.EUR, 1m, new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 1), "B");
        return p;
    }

    [Fact]
    public async Task Handle_FifoAcrossLots_GeneratesNDisposals_AndConsolidatesRealized()
    {
        var portfolio = TwoLotPortfolioSan();
        _portfolios.Setup(r => r.GetByIdWithHoldingsAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(portfolio);
        _companies.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Company.Create("Santander", "SAN", Sector.Financials, Market.BME, Currency.EUR));

        var cmd = new SellSharesCommand(5, 7, 3, 150m, 250m, new DateOnly(2026, 3, 2), null);
        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.Disposals.Should().HaveCount(2);
        result.Disposals[0].Shares.Should().Be(100m);
        result.Disposals[0].RealizedPnL.Should().Be(15000m); // (250-100)*100
        result.Disposals[1].Shares.Should().Be(50m);
        result.Disposals[1].RealizedPnL.Should().Be(2500m);  // (250-200)*50
        result.PortfolioRealizedPnL.Should().Be(17500m);
        result.RealizedPnLCurrency.Should().Be(Currency.EUR);
        _rates.Verify(r => r.GetRateAsync(It.IsAny<Currency>(), It.IsAny<Currency>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UsdCompany_ResolvesRateAtSellDate()
    {
        var portfolio = Portfolio.Create(7, "P", Currency.EUR);
        portfolio.AddHolding(1, 10m, Money.Create(100m, Currency.USD), Currency.EUR, 0.9m, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), null);
        _portfolios.Setup(r => r.GetByIdWithHoldingsAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(portfolio);
        _companies.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Company.Create("Apple", "AAPL", Sector.Technology, Market.NASDAQ, Currency.USD));
        _rates.Setup(r => r.GetRateAsync(Currency.USD, Currency.EUR, new DateOnly(2026, 3, 2), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1.0m);

        var cmd = new SellSharesCommand(5, 7, 1, 10m, 100m, new DateOnly(2026, 3, 2), null);
        var result = await _handler.Handle(cmd, CancellationToken.None);

        // coste base 90/u (100*0.9); venta base 100/u (100*1.0) → realizado (100-90)*10 = 100
        result.PortfolioRealizedPnL.Should().Be(100m);
        result.Disposals[0].SellExchangeRate.Should().Be(1.0m);
    }

    [Fact]
    public async Task Handle_InsufficientShares_ThrowsDomainException()
    {
        var portfolio = TwoLotPortfolioSan(); // 200 abiertas
        _portfolios.Setup(r => r.GetByIdWithHoldingsAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(portfolio);
        _companies.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Company.Create("Santander", "SAN", Sector.Financials, Market.BME, Currency.EUR));

        var cmd = new SellSharesCommand(5, 7, 3, 201m, 250m, new DateOnly(2026, 3, 2), null);
        var act = () => _handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<InsufficientSharesDomainException>();
    }

    [Fact]
    public async Task Handle_ForeignPortfolio_ThrowsNotFound()
    {
        var portfolio = TwoLotPortfolioSan();
        _portfolios.Setup(r => r.GetByIdWithHoldingsAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(portfolio);

        var cmd = new SellSharesCommand(5, 999, 3, 10m, 250m, new DateOnly(2026, 3, 2), null);
        var act = () => _handler.Handle(cmd, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
