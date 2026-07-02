using BigSchool.Application.Investments.Commands.CreatePortfolio;
using BigSchool.Application.SharedKernel.Interfaces.Services;
using BigSchool.Domain.Investments.Entities;
using BigSchool.Domain.SharedKernel.Enums;
using BigSchool.Domain.SharedKernel.Exceptions;
using BigSchool.Domain.SharedKernel.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;
using BigSchool.Application.Investments.Interfaces.Repositories;

namespace BigSchool.Application.Tests.Commands.Investments;

public class CreatePortfolioCommandHandlerTests
{
    private readonly Mock<IPortfolioRepository> _portfolios = new();
    private readonly Mock<IUserBaseCurrencyProvider> _userBaseCurrency = new();
    private readonly CreatePortfolioCommandHandler _handler;

    public CreatePortfolioCommandHandlerTests()
    {
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<bool>())).ReturnsAsync(1);
        _portfolios.Setup(r => r.UnitOfWork).Returns(uow.Object);
        _handler = new CreatePortfolioCommandHandler(_portfolios.Object, _userBaseCurrency.Object);
    }

    [Fact]
    public async Task Handle_CreatesPortfolio_WithRealizedPnLZeroInUserBase()
    {
        _userBaseCurrency.Setup(p => p.GetBaseCurrencyAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Currency.USD);

        var result = await _handler.Handle(new CreatePortfolioCommand(7, "Growth"), CancellationToken.None);

        result.Name.Should().Be("Growth");
        result.RealizedPnL.Should().Be(0m);
        result.RealizedPnLCurrency.Should().Be(Currency.USD);
        _portfolios.Verify(r => r.AddAsync(It.IsAny<Portfolio>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UnknownUser_ThrowsNotFound()
    {
        _userBaseCurrency.Setup(p => p.GetBaseCurrencyAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Currency?)null);

        var act = () => _handler.Handle(new CreatePortfolioCommand(99, "X"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _portfolios.Verify(r => r.AddAsync(It.IsAny<Portfolio>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
