using BigSchool.Application.Commands.Investments.CreatePortfolio;
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Domain.Auth.Entities;
using BigSchool.Domain.Investments.Entities;
using BigSchool.Domain.SharedKernel.Enums;
using BigSchool.Domain.SharedKernel.Exceptions;
using BigSchool.Domain.SharedKernel.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace BigSchool.Application.Tests.Commands.Investments;

public class CreatePortfolioCommandHandlerTests
{
    private readonly Mock<IPortfolioRepository> _portfolios = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly CreatePortfolioCommandHandler _handler;

    public CreatePortfolioCommandHandlerTests()
    {
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<bool>())).ReturnsAsync(1);
        _portfolios.Setup(r => r.UnitOfWork).Returns(uow.Object);
        _handler = new CreatePortfolioCommandHandler(_portfolios.Object, _users.Object);
    }

    [Fact]
    public async Task Handle_CreatesPortfolio_WithRealizedPnLZeroInUserBase()
    {
        var user = User.Create("e2e@test.com", "h", "s", "User", Currency.USD);
        _users.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await _handler.Handle(new CreatePortfolioCommand(7, "Growth"), CancellationToken.None);

        result.Name.Should().Be("Growth");
        result.RealizedPnL.Should().Be(0m);
        result.RealizedPnLCurrency.Should().Be(Currency.USD);
        _portfolios.Verify(r => r.AddAsync(It.IsAny<Portfolio>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UnknownUser_ThrowsNotFound()
    {
        _users.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var act = () => _handler.Handle(new CreatePortfolioCommand(99, "X"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _portfolios.Verify(r => r.AddAsync(It.IsAny<Portfolio>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
