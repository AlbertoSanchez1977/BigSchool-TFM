using BigSchool.Domain.Auth.Entities;
using FluentAssertions;
using Xunit;

namespace BigSchool.Domain.Tests.Entities.Auth;

public class UserProfileTests
{
    private static User NewUser() => User.Create("ada@example.com", "h", "s", "Ada");

    [Fact]
    public void UpdateProfile_ValidFullName_UpdatesFullNameAndUpdatedAt()
    {
        var u = NewUser();
        u.UpdateProfile("Ada L.");
        u.FullName.Should().Be("Ada L.");
        u.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdateProfile_EmptyFullName_ThrowsArgumentException()
    {
        var u = NewUser();
        FluentActions.Invoking(() => u.UpdateProfile(" ")).Should().Throw<System.ArgumentException>();
    }

    [Fact]
    public void ChangePassword_ValidHashAndSalt_UpdatesHashSaltAndUpdatedAt()
    {
        var u = NewUser();
        u.ChangePassword("newHash", "newSalt");
        u.PasswordHash.Should().Be("newHash");
        u.PasswordSalt.Should().Be("newSalt");
        u.UpdatedAt.Should().NotBeNull();
    }
}
