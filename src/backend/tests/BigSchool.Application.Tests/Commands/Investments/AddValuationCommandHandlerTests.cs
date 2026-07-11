using BigSchool.Application.Investments.Commands.AddValuation;
using BigSchool.Domain.Investments.Entities;
using BigSchool.Domain.SharedKernel.Enums;
using BigSchool.Domain.SharedKernel.Exceptions;
using BigSchool.Domain.SharedKernel.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;
using BigSchool.Application.Investments.Interfaces.Repositories;

namespace BigSchool.Application.Tests.Commands.Investments;

public class AddValuationCommandHandlerTests
{
    private readonly Mock<ICompanyRepository> _repo = new();
    private readonly AddValuationCommandHandler _handler;

    public AddValuationCommandHandlerTests()
    {
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<bool>())).ReturnsAsync(1);
        _repo.Setup(r => r.UnitOfWork).Returns(uow.Object);
        _handler = new AddValuationCommandHandler(_repo.Object);
    }

    [Fact]
    public async Task Handle_ExistingCompany_AddsValuationInCompanyCurrency()
    {
        _repo.Setup(r => r.GetByIdWithValuationsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Company.Create("Apple Inc.", "AAPL", null, null, Currency.USD));

        var command = new AddValuationCommand(1, 195.50m, new DateOnly(2026, 1, 2), "manual");
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IdCompany.Should().Be(1);
        result.Price.Should().Be(195.50m);
        result.Currency.Should().Be(Currency.USD);   // moneda de la empresa
        result.Date.Should().Be(new DateOnly(2026, 1, 2));
    }

    [Fact]
    public async Task Handle_UnknownCompany_ThrowsNotFound()
    {
        _repo.Setup(r => r.GetByIdWithValuationsAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((Company?)null);

        var command = new AddValuationCommand(99, 195.50m, new DateOnly(2026, 1, 2), null);
        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
