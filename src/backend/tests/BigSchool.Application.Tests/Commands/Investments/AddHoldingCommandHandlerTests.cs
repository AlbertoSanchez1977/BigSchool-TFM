using BigSchool.Application.Commands.Investments.AddHolding;
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Application.Interfaces.Services;
using BigSchool.Domain.Entities;
using BigSchool.Domain.Enums;
using BigSchool.Domain.Exceptions;
using BigSchool.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace BigSchool.Application.Tests.Commands.Investments;

public class AddHoldingCommandHandlerTests
{
    private readonly Mock<IPortfolioRepository> _portfolios = new();
    private readonly Mock<ICompanyRepository> _companies = new();
    private readonly Mock<IExchangeRateProvider> _rates = new();
    private readonly AddHoldingCommandHandler _handler;

    public AddHoldingCommandHandlerTests()
    {
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<bool>())).ReturnsAsync(1);
        _portfolios.Setup(r => r.UnitOfWork).Returns(uow.Object);
        _handler = new AddHoldingCommandHandler(_portfolios.Object, _companies.Object, _rates.Object);
    }

    private static Portfolio PortfolioOf(int userId, Currency baseCcy) => Portfolio.Create(userId, "P", baseCcy);

    [Fact]
    public async Task Handle_UsdCompanyEurBase_FreezesConvertedCostSnapshot()
    {
        var portfolio = PortfolioOf(7, Currency.EUR);
        _portfolios.Setup(r => r.GetByIdWithHoldingsAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(portfolio);
        _companies.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Company.Create("Apple", "AAPL", Sector.Technology, Market.NASDAQ, Currency.USD));
        _rates.Setup(r => r.GetRateAsync(Currency.USD, Currency.EUR, new DateOnly(2026, 1, 1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0.9m);

        var cmd = new AddHoldingCommand(5, 7, 1, 100m, 195m, new DateOnly(2026, 1, 1), "lote");
        var dto = await _handler.Handle(cmd, CancellationToken.None);

        dto.Shares.Should().Be(100m);
        dto.BuyOriginalAmount.Should().Be(195m);
        dto.BuyOriginalCurrency.Should().Be(Currency.USD);
        dto.BuyExchangeRate.Should().Be(0.9m);
        dto.BuyBaseAmount.Should().Be(175.50m);
        dto.BuyBaseCurrency.Should().Be(Currency.EUR);
        portfolio.Holdings.Should().ContainSingle();
        _portfolios.Verify(r => r.UnitOfWork.SaveChangesAsync(It.IsAny<bool>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EurCompanyEurBase_UsesRateOne_NoProviderCall()
    {
        var portfolio = PortfolioOf(7, Currency.EUR);
        _portfolios.Setup(r => r.GetByIdWithHoldingsAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(portfolio);
        _companies.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Company.Create("Santander", "SAN", Sector.Financials, Market.BME, Currency.EUR));

        var cmd = new AddHoldingCommand(5, 7, 3, 10m, 4.5m, new DateOnly(2026, 1, 1), null);
        var dto = await _handler.Handle(cmd, CancellationToken.None);

        dto.BuyExchangeRate.Should().Be(1m);
        dto.BuyBaseAmount.Should().Be(4.5m);
        _rates.Verify(r => r.GetRateAsync(It.IsAny<Currency>(), It.IsAny<Currency>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_PortfolioOfAnotherUser_ThrowsNotFound()
    {
        var portfolio = PortfolioOf(7, Currency.EUR);
        _portfolios.Setup(r => r.GetByIdWithHoldingsAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(portfolio);

        var cmd = new AddHoldingCommand(5, 999, 1, 100m, 195m, new DateOnly(2026, 1, 1), null);
        var act = () => _handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_UnknownCompany_ThrowsNotFound()
    {
        var portfolio = PortfolioOf(7, Currency.EUR);
        _portfolios.Setup(r => r.GetByIdWithHoldingsAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(portfolio);
        _companies.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync((Company?)null);

        var cmd = new AddHoldingCommand(5, 7, 404, 100m, 195m, new DateOnly(2026, 1, 1), null);
        var act = () => _handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
