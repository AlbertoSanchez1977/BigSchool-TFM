using BigSchool.Application.Finanzas.Commands.DeleteSubCategory;
using BigSchool.Application.Finanzas.Interfaces.Repositories;
using BigSchool.Domain.Finanzas.Entities;
using BigSchool.Domain.Finanzas.Enums;
using BigSchool.Domain.SharedKernel.Exceptions;
using BigSchool.Domain.SharedKernel.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace BigSchool.Application.Tests.Commands.Finanzas;

public class DeleteSubCategoryCommandHandlerTests
{
    private static Mock<ISubCategoryRepository> RepoReturning(SubCategory? sub)
    {
        var repo = new Mock<ISubCategoryRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(sub);
        repo.SetupGet(r => r.UnitOfWork).Returns(Mock.Of<IUnitOfWork>());
        return repo;
    }

    [Fact]
    public async Task Handle_NonExistentSubCategory_ThrowsNotFoundException()
    {
        var handler = new DeleteSubCategoryCommandHandler(RepoReturning(null).Object);
        await FluentActions.Invoking(() => handler.Handle(new DeleteSubCategoryCommand(1, 99), CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_GlobalSubCategory_ThrowsNotFoundException()
    {
        var global = SubCategory.Create(MainCategory.Other, "X", idUser: null);
        var handler = new DeleteSubCategoryCommandHandler(RepoReturning(global).Object);
        await FluentActions.Invoking(() => handler.Handle(new DeleteSubCategoryCommand(1, 1), CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_OtherUsersSubCategory_ThrowsNotFoundException()
    {
        var other = SubCategory.Create(MainCategory.Other, "X", idUser: 2);
        var handler = new DeleteSubCategoryCommandHandler(RepoReturning(other).Object);
        await FluentActions.Invoking(() => handler.Handle(new DeleteSubCategoryCommand(1, 5), CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_OwnSubCategory_SoftDeletes()
    {
        var mine = SubCategory.Create(MainCategory.Other, "X", idUser: 1);
        var handler = new DeleteSubCategoryCommandHandler(RepoReturning(mine).Object);
        await handler.Handle(new DeleteSubCategoryCommand(1, 3), CancellationToken.None);
        mine.IdStatus.Should().Be(BigSchool.Domain.SharedKernel.Enums.EntityStatus.Deleted);
    }
}
