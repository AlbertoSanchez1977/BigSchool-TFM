using BigSchool.Application.Commands.Auth.Login;
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Application.Interfaces.Services;
using BigSchool.Domain.Entities;
using BigSchool.Domain.Exceptions;
using BigSchool.Domain.SharedKernel.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace BigSchool.Application.Tests.Commands.Auth;

public class LoginCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IPasswordHasher> _hasherMock = new();
    private readonly Mock<IJwtService> _jwtMock = new();
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<bool>())).ReturnsAsync(1);
        _userRepoMock.Setup(r => r.UnitOfWork).Returns(unitOfWorkMock.Object);

        _jwtMock.Setup(j => j.GenerateToken(It.IsAny<int>(), It.IsAny<string>()))
            .Returns(new JwtToken("logintoken", DateTime.UtcNow.AddHours(1)));

        _handler = new LoginCommandHandler(_userRepoMock.Object, _hasherMock.Object, _jwtMock.Object);
    }

    [Fact]
    public async Task Handle_ValidCredentials_ReturnsToken()
    {
        var user = User.Create("user@test.com", "hashed", "salted", "Test User");
        _userRepoMock.Setup(r => r.GetByEmailAsync("user@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _hasherMock.Setup(h => h.VerifyPassword("correct", "hashed", "salted"))
            .Returns(true);

        var command = new LoginCommand("user@test.com", "correct");
        var result = await _handler.Handle(command, CancellationToken.None);

        result.AccessToken.Should().Be("logintoken");
        result.Email.Should().Be("user@test.com");
    }

    [Fact]
    public async Task Handle_WrongPassword_ThrowsInvalidCredentialsDomainException()
    {
        var user = User.Create("user@test.com", "hashed", "salted", "Test User");
        _userRepoMock.Setup(r => r.GetByEmailAsync("user@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _hasherMock.Setup(h => h.VerifyPassword("wrong", "hashed", "salted"))
            .Returns(false);

        var command = new LoginCommand("user@test.com", "wrong");
        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidCredentialsDomainException>();
    }

    [Fact]
    public async Task Handle_NonExistentUser_ThrowsInvalidCredentialsDomainException()
    {
        _userRepoMock.Setup(r => r.GetByEmailAsync("ghost@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = new LoginCommand("ghost@test.com", "any");
        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidCredentialsDomainException>();
    }
}
