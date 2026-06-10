using BigSchool.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace BigSchool.Application.Tests.Services;

public class PasswordHasherTests
{
    private readonly Argon2PasswordHasher _hasher = new();

    [Fact]
    public void HashPassword_ReturnsNonEmptyHashAndSalt()
    {
        var (hash, salt) = _hasher.HashPassword("MyP@ssw0rd!");

        hash.Should().NotBeNullOrEmpty();
        salt.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void VerifyPassword_WithCorrectPassword_ReturnsTrue()
    {
        var (hash, salt) = _hasher.HashPassword("MyP@ssw0rd!");

        _hasher.VerifyPassword("MyP@ssw0rd!", hash, salt).Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_WithWrongPassword_ReturnsFalse()
    {
        var (hash, salt) = _hasher.HashPassword("MyP@ssw0rd!");

        _hasher.VerifyPassword("WrongPassword", hash, salt).Should().BeFalse();
    }

    [Fact]
    public void HashPassword_ProducesDifferentSaltsEachTime()
    {
        var (_, salt1) = _hasher.HashPassword("same");
        var (_, salt2) = _hasher.HashPassword("same");

        salt1.Should().NotBe(salt2);
    }
}
