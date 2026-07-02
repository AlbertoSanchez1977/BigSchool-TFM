using BigSchool.Domain.Auth.Entities;
using BigSchool.Domain.SharedKernel.Enums;
using FluentAssertions;
using Xunit;

namespace BigSchool.Domain.Tests.Entities;

public class UserTests
{
    [Fact]
    public void Create_WithValidData_SetsPropertiesCorrectly()
    {
        var user = User.Create("test@example.com", "hashedpwd", "salted", "John Doe");

        user.Email.Should().Be("test@example.com");
        user.PasswordHash.Should().Be("hashedpwd");
        user.PasswordSalt.Should().Be("salted");
        user.FullName.Should().Be("John Doe");
        user.IdStatus.Should().Be(EntityStatus.Active);
        user.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        user.LastLoginDate.Should().BeNull();
        user.SubCategories.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void Create_WithInvalidEmail_ThrowsArgumentException(string? email)
    {
        var act = () => User.Create(email!, "hash", "salt", "Name");
        act.Should().Throw<ArgumentException>().WithParameterName("email");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void Create_WithInvalidFullName_ThrowsArgumentException(string? fullName)
    {
        var act = () => User.Create("test@example.com", "hash", "salt", fullName!);
        act.Should().Throw<ArgumentException>().WithParameterName("fullName");
    }

    [Fact]
    public void UpdateLastLogin_SetsDateAndUpdatedAt()
    {
        var user = User.Create("test@example.com", "hash", "salt", "Name");

        user.UpdateLastLogin();

        user.LastLoginDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        user.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Create_NormalizesEmailToLowerCase()
    {
        var user = User.Create("TEST@Example.COM", "hash", "salt", "Name");

        user.Email.Should().Be("test@example.com");
    }

    [Fact]
    public void Create_WithoutBaseCurrency_DefaultsToEur()
    {
        var user = User.Create("test@example.com", "hashedpwd", "salted", "John Doe");

        user.BaseCurrency.Should().Be(Currency.EUR);
    }

    [Fact]
    public void Create_WithExplicitBaseCurrency_SetsIt()
    {
        var user = User.Create("test@example.com", "hashedpwd", "salted", "John Doe", Currency.USD);

        user.BaseCurrency.Should().Be(Currency.USD);
    }
}
