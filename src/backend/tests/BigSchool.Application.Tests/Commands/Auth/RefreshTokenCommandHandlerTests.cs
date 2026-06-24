using BigSchool.Application.Commands.Auth.Refresh;
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Application.Interfaces.Services;
using BigSchool.Domain.Entities;
using FluentAssertions;
using Moq;
using Xunit;

namespace BigSchool.Application.Tests.Commands.Auth;

public class RefreshTokenCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IJwtService> _jwtMock = new();
    private readonly RefreshTokenCommandHandler _handler;

    public RefreshTokenCommandHandlerTests()
    {
        _handler = new RefreshTokenCommandHandler(_userRepoMock.Object, _jwtMock.Object);
    }

    [Fact]
    public async Task Handle_ValidToken_ReturnsNewToken()
    {
        var user = User.Create("user@test.com", "hashed", "salted", "Test User");
        _jwtMock.Setup(j => j.ExtractUserId("oldtoken")).Returns(7);
        _userRepoMock.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _jwtMock.Setup(j => j.GenerateToken(It.IsAny<int>(), "user@test.com"))
            .Returns(new JwtToken("newtoken", DateTime.UtcNow.AddHours(1)));

        var result = await _handler.Handle(new RefreshTokenCommand("oldtoken"), CancellationToken.None);

        result.AccessToken.Should().Be("newtoken");
        result.Email.Should().Be("user@test.com");
        result.FullName.Should().Be("Test User");
    }

    [Fact]
    public async Task Handle_TokenWithoutUserId_ThrowsUnauthorized()
    {
        _jwtMock.Setup(j => j.ExtractUserId("badtoken")).Returns((int?)null);

        var act = () => _handler.Handle(new RefreshTokenCommand("badtoken"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_UserNotFoundOrDeleted_ThrowsUnauthorized()
    {
        _jwtMock.Setup(j => j.ExtractUserId("oldtoken")).Returns(99);
        _userRepoMock.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var act = () => _handler.Handle(new RefreshTokenCommand("oldtoken"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
