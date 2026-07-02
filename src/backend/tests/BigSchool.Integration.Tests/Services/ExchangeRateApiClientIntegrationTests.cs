using System.Text.Json;
using BigSchool.Application.SharedKernel.Configuration;
using BigSchool.Domain.SharedKernel.Enums;
using BigSchool.Infrastructure.Persistence;
using BigSchool.Infrastructure.Services;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace BigSchool.Integration.Tests.Services;

[Collection(IntegrationCollection.Name)]
public class ExchangeRateApiClientIntegrationTests : IAsyncLifetime
{
    private readonly MySqlDatabaseFixture _fixture;
    private WireMockServer _wireMock = null!;

    public ExchangeRateApiClientIntegrationTests(MySqlDatabaseFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        // La BD ya está migrada por el fixture de la suite; solo limpiamos datos mutables y arrancamos el fake.
        await _fixture.ResetAsync();
        _wireMock = WireMockServer.Start();
    }

    public Task DisposeAsync()
    {
        _wireMock.Stop();
        return Task.CompletedTask;
    }

    private IOptions<AppSettings> BuildSettings() => Options.Create(new AppSettings
    {
        ConnectionString = _fixture.ConnectionString,
        Jwt = new JwtSettings { Secret = "x", Issuer = "i", Audience = "a" },
        RagService = new RagServiceSettings { BaseUrl = "http://localhost" },
        ExchangeRate = new ExchangeRateSettings { BaseUrl = _wireMock.Url! }
    });

    private ExchangeRateApiClient CreateClient()
    {
        var settings = BuildSettings();
        var dbFactory = new DbConnectionMySqlFactory(settings);

        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient());

        return new ExchangeRateApiClient(dbFactory, httpFactory.Object, settings);
    }

    [Fact]
    public async Task GetRateAsync_CacheMissThenHit_CallsProviderOnce()
    {
        // Par no sembrado para forzar miss en la primera llamada.
        var date = new DateOnly(2026, 6, 10);
        var responseBody = JsonSerializer.Serialize(new
        {
            amount = 1.0,
            @base = "USD",
            date = date.ToString("yyyy-MM-dd"),
            rates = new { GBP = 0.85 }
        });

        _wireMock
            .Given(Request.Create().WithPath($"/{date:yyyy-MM-dd}").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(responseBody));

        var client = CreateClient();

        var first = await client.GetRateAsync(Currency.USD, Currency.GBP, date);   // miss -> provider
        var second = await client.GetRateAsync(Currency.USD, Currency.GBP, date);  // hit  -> cache

        first.Should().Be(0.85m);
        second.Should().Be(0.85m);
        _wireMock.LogEntries.Should().HaveCount(1); // el proveedor se llamó una sola vez
    }
}
