using BigSchool.Application.Auth.Commands.Register;
using BigSchool.Domain.Auth.Entities;
using BigSchool.Domain.Auth.Exceptions;
using BigSchool.Domain.SharedKernel.Enums;
using BigSchool.Domain.SharedKernel.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;
using BigSchool.Application.Auth.Interfaces.Repositories;
using BigSchool.Application.Auth.Interfaces.Services;

namespace BigSchool.Application.Tests.Commands.Auth;

public class RegisterCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IPasswordHasher> _hasherMock = new();
    private readonly Mock<IJwtService> _jwtMock = new();
    private readonly RegisterCommandHandler _handler;

    public RegisterCommandHandlerTests()
    {
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<bool>())).ReturnsAsync(1);
        _userRepoMock.Setup(r => r.UnitOfWork).Returns(unitOfWorkMock.Object);

        _hasherMock.Setup(h => h.HashPassword(It.IsAny<string>()))
            .Returns(("hashed", "salted"));
        _jwtMock.Setup(j => j.GenerateToken(It.IsAny<int>(), It.IsAny<string>()))
            .Returns(new JwtToken("token123", DateTime.UtcNow.AddHours(1)));

        _handler = new RegisterCommandHandler(_userRepoMock.Object, _hasherMock.Object, _jwtMock.Object);
    }

    [Fact]
    public async Task Handle_NewUser_ReturnsTokenAndCreatesUser()
    {
        _userRepoMock.Setup(r => r.ExistsWithEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new RegisterCommand("new@test.com", "Password1!", "John Doe", Currency.EUR);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.AccessToken.Should().Be("token123");
        result.Email.Should().Be("new@test.com");
        result.FullName.Should().Be("John Doe");
        _userRepoMock.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ExistingEmail_ThrowsEmailAlreadyExistsDomainException()
    {
        _userRepoMock.Setup(r => r.ExistsWithEmailAsync("existing@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new RegisterCommand("existing@test.com", "Password1!", "User", Currency.EUR);
        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<EmailAlreadyExistsDomainException>();
    }
}
