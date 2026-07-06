using BigSchool.Application.Investments.Commands.RenamePortfolio;
using BigSchool.Application.Investments.Interfaces.Repositories;
using BigSchool.Domain.Investments.Entities;
using BigSchool.Domain.SharedKernel.Enums;
using BigSchool.Domain.SharedKernel.Exceptions;
using BigSchool.Domain.SharedKernel.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace BigSchool.Application.Tests.Commands.Investments;

public class RenamePortfolioCommandHandlerTests
{
    private readonly Mock<IPortfolioRepository> _portfolios = new();
    private readonly RenamePortfolioCommandHandler _handler;

    public RenamePortfolioCommandHandlerTests()
    {
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<bool>())).ReturnsAsync(1);
        _portfolios.Setup(r => r.UnitOfWork).Returns(uow.Object);
        _handler = new RenamePortfolioCommandHandler(_portfolios.Object);
    }

    [Fact]
    public async Task Handle_ValidName_RenamesAndSaves()
    {
        var portfolio = Portfolio.Create(7, "Original", Currency.EUR);
        _portfolios.Setup(r => r.GetByIdWithHoldingsAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(portfolio);

        await _handler.Handle(new RenamePortfolioCommand(5, 7, "Nueva cartera"), CancellationToken.None);

        portfolio.Name.Should().Be("Nueva cartera");
    }

    [Fact]
    public async Task Handle_ForeignPortfolio_ThrowsNotFound()
    {
        var portfolio = Portfolio.Create(7, "Original", Currency.EUR);
        _portfolios.Setup(r => r.GetByIdWithHoldingsAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(portfolio);

        var act = () => _handler.Handle(new RenamePortfolioCommand(5, 999, "Robada"), CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
