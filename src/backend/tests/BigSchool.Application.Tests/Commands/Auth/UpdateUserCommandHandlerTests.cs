using BigSchool.Application.Auth.Commands.UpdateUser;
using BigSchool.Application.Auth.Interfaces.Repositories;
using BigSchool.Application.Auth.Interfaces.Services;
using BigSchool.Domain.Auth.Entities;
using BigSchool.Domain.SharedKernel.Exceptions;
using BigSchool.Domain.SharedKernel.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace BigSchool.Application.Tests.Commands.Auth;

public class UpdateUserCommandHandlerTests
{
    private static User NewUser() => User.Create("ada@example.com", "h", "s", "Ada");

    [Fact]
    public async Task Handle_NullPassword_UpdatesFullNameWithoutRehashing()
    {
        var user = NewUser();
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        repo.SetupGet(r => r.UnitOfWork).Returns(Mock.Of<IUnitOfWork>());
        var hasher = new Mock<IPasswordHasher>();
        var handler = new UpdateUserCommandHandler(repo.Object, hasher.Object);

        var dto = await handler.Handle(new UpdateUserCommand(1, "Ada L.", null), CancellationToken.None);

        dto.FullName.Should().Be("Ada L.");
        hasher.Verify(h => h.HashPassword(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithPassword_RehashesPassword()
    {
        var user = NewUser();
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        repo.SetupGet(r => r.UnitOfWork).Returns(Mock.Of<IUnitOfWork>());
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.HashPassword("Secret123!")).Returns(("newHash", "newSalt"));
        var handler = new UpdateUserCommandHandler(repo.Object, hasher.Object);

        await handler.Handle(new UpdateUserCommand(1, "Ada", "Secret123!"), CancellationToken.None);

        hasher.Verify(h => h.HashPassword("Secret123!"), Times.Once);
        user.PasswordHash.Should().Be("newHash");
    }

    [Fact]
    public async Task Handle_NonExistentUser_ThrowsNotFoundException()
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        var handler = new UpdateUserCommandHandler(repo.Object, Mock.Of<IPasswordHasher>());

        await FluentActions.Invoking(() => handler.Handle(new UpdateUserCommand(99, "Ada", null), CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();
    }
}
