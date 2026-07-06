using BigSchool.Application.Investments.Commands.DeletePortfolio;
using BigSchool.Application.Investments.Interfaces.Repositories;
using BigSchool.Domain.Investments.Entities;
using BigSchool.Domain.Investments.Exceptions;
using BigSchool.Domain.SharedKernel.Enums;
using BigSchool.Domain.SharedKernel.Exceptions;
using BigSchool.Domain.SharedKernel.Interfaces;
using BigSchool.Domain.SharedKernel.ValueObjects;
using FluentAssertions;
using Moq;
using Xunit;

namespace BigSchool.Application.Tests.Commands.Investments;

public class DeletePortfolioCommandHandlerTests
{
    private readonly Mock<IPortfolioRepository> _portfolios = new();
    private readonly DeletePortfolioCommandHandler _handler;

    public DeletePortfolioCommandHandlerTests()
    {
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<bool>())).ReturnsAsync(1);
        _portfolios.Setup(r => r.UnitOfWork).Returns(uow.Object);
        _handler = new DeletePortfolioCommandHandler(_portfolios.Object);
    }

    [Fact]
    public async Task Handle_NoHoldings_SoftDeletesAndSaves()
    {
        var portfolio = Portfolio.Create(7, "P", Currency.EUR);
        _portfolios.Setup(r => r.GetByIdWithHoldingsAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(portfolio);

        await _handler.Handle(new DeletePortfolioCommand(5, 7), CancellationToken.None);

        portfolio.IdStatus.Should().Be(EntityStatus.Deleted);
    }

    [Fact]
    public async Task Handle_ForeignPortfolio_ThrowsNotFound()
    {
        var portfolio = Portfolio.Create(7, "P", Currency.EUR);
        _portfolios.Setup(r => r.GetByIdWithHoldingsAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(portfolio);

        var act = () => _handler.Handle(new DeletePortfolioCommand(5, 999), CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_OpenPosition_ThrowsHasOpenPositions()
    {
        var portfolio = Portfolio.Create(7, "P", Currency.EUR);
        portfolio.AddHolding(3, 10m, Money.Create(100m, Currency.EUR), Currency.EUR, 1m,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), null);
        _portfolios.Setup(r => r.GetByIdWithHoldingsAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(portfolio);

        var act = () => _handler.Handle(new DeletePortfolioCommand(5, 7), CancellationToken.None);
        await act.Should().ThrowAsync<PortfolioHasOpenPositionsDomainException>();
    }
}
