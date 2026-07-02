using BigSchool.Application.Commands.Investments.DeleteHolding;
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Domain.Entities;
using BigSchool.Domain.SharedKernel.Enums;
using BigSchool.Domain.SharedKernel.Exceptions;
using BigSchool.Domain.SharedKernel.Interfaces;
using BigSchool.Domain.SharedKernel.ValueObjects;
using FluentAssertions;
using Moq;
using Xunit;

namespace BigSchool.Application.Tests.Commands.Investments;

public class DeleteHoldingCommandHandlerTests
{
    private readonly Mock<IPortfolioRepository> _portfolios = new();
    private readonly DeleteHoldingCommandHandler _handler;

    public DeleteHoldingCommandHandlerTests()
    {
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<bool>())).ReturnsAsync(1);
        _portfolios.Setup(r => r.UnitOfWork).Returns(uow.Object);
        _handler = new DeleteHoldingCommandHandler(_portfolios.Object);
    }

    [Fact]
    public async Task Handle_SoftDeletesHolding_AndReversesRealized()
    {
        var portfolio = Portfolio.Create(7, "P", Currency.EUR);
        var holding = portfolio.AddHolding(3, 100m, Money.Create(100m, Currency.EUR), Currency.EUR, 1m,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), null);
        portfolio.SellShares(3, 40m, Money.Create(150m, Currency.EUR), Currency.EUR, 1m,
            new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 2));
        _portfolios.Setup(r => r.GetByIdWithHoldingsAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(portfolio);

        await _handler.Handle(new DeleteHoldingCommand(5, 7, holding.IdHolding), CancellationToken.None);

        portfolio.Holdings.Single().IdStatus.Should().Be(EntityStatus.Deleted);
        portfolio.RealizedPnL.Amount.Should().Be(0m);
    }

    [Fact]
    public async Task Handle_ForeignPortfolio_ThrowsNotFound()
    {
        var portfolio = Portfolio.Create(7, "P", Currency.EUR);
        _portfolios.Setup(r => r.GetByIdWithHoldingsAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(portfolio);

        var act = () => _handler.Handle(new DeleteHoldingCommand(5, 999, 1), CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
