using BigSchool.Application.Commands.Investments.UpdateHolding;
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Domain.Investments.Entities;
using BigSchool.Domain.SharedKernel.Enums;
using BigSchool.Domain.SharedKernel.Exceptions;
using BigSchool.Domain.SharedKernel.Interfaces;
using BigSchool.Domain.SharedKernel.ValueObjects;
using FluentAssertions;
using Moq;
using Xunit;

namespace BigSchool.Application.Tests.Commands.Investments;

public class UpdateHoldingCommandHandlerTests
{
    private readonly Mock<IPortfolioRepository> _portfolios = new();
    private readonly UpdateHoldingCommandHandler _handler;

    public UpdateHoldingCommandHandlerTests()
    {
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<bool>())).ReturnsAsync(1);
        _portfolios.Setup(r => r.UnitOfWork).Returns(uow.Object);
        _handler = new UpdateHoldingCommandHandler(_portfolios.Object);
    }

    [Fact]
    public async Task Handle_UpdatesNotes()
    {
        var portfolio = Portfolio.Create(7, "P", Currency.EUR);
        var holding = portfolio.AddHolding(3, 10m, Money.Create(4.5m, Currency.EUR),
            Currency.EUR, 1m, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), "old");
        _portfolios.Setup(r => r.GetByIdWithHoldingsAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(portfolio);

        var dto = await _handler.Handle(new UpdateHoldingCommand(5, 7, holding.IdHolding, "new"), CancellationToken.None);

        dto.Notes.Should().Be("new");
    }

    [Fact]
    public async Task Handle_ForeignPortfolio_ThrowsNotFound()
    {
        var portfolio = Portfolio.Create(7, "P", Currency.EUR);
        _portfolios.Setup(r => r.GetByIdWithHoldingsAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(portfolio);

        var act = () => _handler.Handle(new UpdateHoldingCommand(5, 999, 1, "x"), CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
