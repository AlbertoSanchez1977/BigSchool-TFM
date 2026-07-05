using BigSchool.Application.Finance.Commands.CreateSubCategory;
using BigSchool.Application.Finance.Interfaces.Repositories;
using BigSchool.Domain.Finance.Enums;
using BigSchool.Domain.Finance.Exceptions;
using BigSchool.Domain.SharedKernel.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace BigSchool.Application.Tests.Commands.Finance;

public class CreateSubCategoryCommandHandlerTests
{
    [Fact]
    public async Task Handle_DuplicateName_ThrowsDuplicateSubCategoryDomainException()
    {
        var repo = new Mock<ISubCategoryRepository>();
        repo.Setup(r => r.ExistsActiveAsync(1, MainCategory.Luxuries, "Cine", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var handler = new CreateSubCategoryCommandHandler(repo.Object);

        await FluentActions.Invoking(() => handler.Handle(new CreateSubCategoryCommand(1, MainCategory.Luxuries, "Cine"), CancellationToken.None))
            .Should().ThrowAsync<DuplicateSubCategoryDomainException>();
    }

    [Fact]
    public async Task Handle_NewSubCategory_CreatesAndSaves()
    {
        var repo = new Mock<ISubCategoryRepository>();
        repo.Setup(r => r.ExistsActiveAsync(It.IsAny<int?>(), It.IsAny<MainCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        repo.SetupGet(r => r.UnitOfWork).Returns(Mock.Of<IUnitOfWork>());
        var handler = new CreateSubCategoryCommandHandler(repo.Object);

        var dto = await handler.Handle(new CreateSubCategoryCommand(1, MainCategory.Luxuries, "Cine"), CancellationToken.None);

        dto.Name.Should().Be("Cine");
        repo.Verify(r => r.AddAsync(It.IsAny<BigSchool.Domain.Finance.Entities.SubCategory>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
