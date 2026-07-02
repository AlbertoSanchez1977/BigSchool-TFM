using BigSchool.Application.SharedKernel.Configuration;
using BigSchool.Domain.SharedKernel.Enums;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using BigSchool.Application.SharedKernel.Interfaces;
using BigSchool.Infrastructure.SharedKernel.Services;

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
