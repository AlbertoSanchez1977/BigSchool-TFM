using BigSchool.Application.Investments.Commands.CreateCompany;
using BigSchool.Domain.Investments.Entities;
using BigSchool.Domain.Investments.Enums;
using BigSchool.Domain.SharedKernel.Enums;
using BigSchool.Domain.Investments.Exceptions;
using BigSchool.Domain.SharedKernel.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;
using BigSchool.Application.Investments.Interfaces.Repositories;

namespace BigSchool.Application.Tests.Commands.Investments;
public class CreateCompanyCommandHandlerTests
{
    private readonly Mock<ICompanyRepository> _repo = new();
    private readonly CreateCompanyCommandHandler _handler;

    public CreateCompanyCommandHandlerTests()
    {
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<bool>())).ReturnsAsync(1);
        _repo.Setup(r => r.UnitOfWork).Returns(uow.Object);
        _handler = new CreateCompanyCommandHandler(_repo.Object);
    }

    [Fact]
    public async Task Handle_NewTicker_CreatesCompany()
    {
        _repo.Setup(r => r.GetByTickerAsync("AAPL", It.IsAny<CancellationToken>())).ReturnsAsync((Company?)null);

        var command = new CreateCompanyCommand("Apple Inc.", "AAPL", Sector.Technology, Market.NASDAQ, Currency.USD);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.Ticker.Should().Be("AAPL");
        result.Currency.Should().Be(Currency.USD);
        _repo.Verify(r => r.AddAsync(It.IsAny<Company>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicateTicker_ThrowsConflict()
    {
        _repo.Setup(r => r.GetByTickerAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Company.Create("Apple Inc.", "AAPL", null, null, Currency.USD));

        var command = new CreateCompanyCommand("Apple Inc.", "AAPL", null, null, Currency.USD);
        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<DuplicateTickerDomainException>();
        _repo.Verify(r => r.AddAsync(It.IsAny<Company>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
