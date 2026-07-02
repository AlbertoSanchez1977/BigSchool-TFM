using System.IdentityModel.Tokens.Jwt;
using BigSchool.Application.SharedKernel.Configuration;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Xunit;
using BigSchool.Infrastructure.Auth.Services;

namespace BigSchool.Application.Tests.Services;

public class JwtServiceTests
{
    private readonly JwtService _jwtService;
    private readonly DataProtectionUserIdEncryptor _encryptor;

    public JwtServiceTests()
    {
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        _encryptor = new DataProtectionUserIdEncryptor(dataProtectionProvider);

        var settings = Options.Create(new AppSettings
        {
            ConnectionString = "unused",
            Jwt = new JwtSettings
            {
                Secret = "SuperSecretKeyForTestingPurposesOnly_32chars!!",
                Issuer = "BigSchool",
                Audience = "BigSchool",
                ExpirationMinutes = 60
            },
            RagService = new RagServiceSettings { BaseUrl = "http://localhost" },
            ExchangeRate = new ExchangeRateSettings { BaseUrl = "https://api.frankfurter.app" }
        });
        _jwtService = new JwtService(settings, _encryptor);
    }

    [Fact]
    public void GenerateToken_ReturnsValidJwt()
    {
        var token = _jwtService.GenerateToken(1, "test@example.com");

        token.AccessToken.Should().NotBeNullOrEmpty();
        token.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public void GenerateToken_SubClaimIsEncrypted_NotPlainInt()
    {
        var token = _jwtService.GenerateToken(42, "user@test.com");

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token.AccessToken);
        var sub = jwt.Claims.First(c => c.Type == "sub").Value;

        // El sub NO debe ser "42" en texto plano
        sub.Should().NotBe("42");
        // Pero debe ser decryptable a 42
        _encryptor.Decrypt(sub).Should().Be(42);
    }

    [Fact]
    public void ExtractUserId_WithValidToken_ReturnsUserId()
    {
        var token = _jwtService.GenerateToken(99, "user@test.com");

        var userId = _jwtService.ExtractUserId(token.AccessToken);

        userId.Should().Be(99);
    }

    [Fact]
    public void GenerateToken_ContainsEmailClaim()
    {
        var token = _jwtService.GenerateToken(1, "user@test.com");

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token.AccessToken);

        jwt.Claims.Should().Contain(c => c.Type == "email" && c.Value == "user@test.com");
        jwt.Issuer.Should().Be("BigSchool");
    }
}
