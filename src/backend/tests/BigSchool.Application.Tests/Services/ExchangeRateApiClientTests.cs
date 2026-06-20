using BigSchool.Application.Configuration;
using BigSchool.Application.Interfaces;
using BigSchool.Domain.Enums;
using BigSchool.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace BigSchool.Application.Tests.Services;

public class ExchangeRateApiClientTests
{
    private static IOptions<AppSettings> Settings() => Options.Create(new AppSettings
    {
        ConnectionString = "ignored",
        Jwt = new JwtSettings { Secret = "x", Issuer = "i", Audience = "a" },
        RagService = new RagServiceSettings { BaseUrl = "http://localhost" },
        ExchangeRate = new ExchangeRateSettings { BaseUrl = "https://api.frankfurter.app" }
    });

    [Fact]
    public async Task GetRateAsync_SameCurrency_ReturnsOne_WithoutDbOrHttp()
    {
        var dbFactory = new Mock<IDbConnectionFactory>(MockBehavior.Strict);   // ninguna llamada permitida
        var httpFactory = new Mock<IHttpClientFactory>(MockBehavior.Strict);   // ninguna llamada permitida

        var client = new ExchangeRateApiClient(dbFactory.Object, httpFactory.Object, Settings());

        var rate = await client.GetRateAsync(Currency.EUR, Currency.EUR, new DateOnly(2026, 6, 17));

        rate.Should().Be(1m);
        dbFactory.VerifyNoOtherCalls();
        httpFactory.VerifyNoOtherCalls();
    }
}
