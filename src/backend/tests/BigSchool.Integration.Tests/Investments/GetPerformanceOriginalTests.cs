using BigSchool.Domain.SharedKernel.Enums;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using System.Net.Http.Json;
using Xunit;

namespace BigSchool.Integration.Tests.Investments;

[Collection(IntegrationCollection.Name)]
public class GetPerformanceOriginalTests : PortfolioEndpointTestBase
{
    public GetPerformanceOriginalTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Performance_ReturnsOriginalFields_InCompanyCurrency()
    {
        var (userId, email) = await SeedUserAsync(Currency.EUR);
        var client = AuthenticatedClient(userId, email);
        var portfolioId = await CreatePortfolioViaApiAsync(client, "Tech");
        await SeedExchangeRateAsync(Currency.USD, Currency.EUR, 0.90m, new DateOnly(2026, 1, 5));
        await SeedExchangeRateAsync(Currency.USD, Currency.EUR, 1.00m, new DateOnly(2026, 3, 2)); // última valoración AAPL
        await AddHoldingViaApiAsync(client, portfolioId, CompanyAaplUsd, 10m, 195m, "2026-01-05"); // AAPL USD

        var perf = (await (await client.GetAsync($"/api/v1/portfolios/{portfolioId}/performance"))
            .Content.ReadFromJsonAsync<ApiEnvelope<PerformanceResponse>>())!.Data!;
        var h = perf.Holdings.Single();

        h.BuyOriginalCurrency.Should().Be("USD");
        // CostBasisOriginal = OpenShares(10) × BuyOriginal(195) = 1950 (sin Rate)
        h.CostBasisOriginal.Should().Be(1950m);
        // MarketValueOriginal = 10 × LastPrice(210 USD, seed AAPL 2026-03-02) = 2100
        h.MarketValueOriginal.Should().Be(2100m);
        h.UnrealizedPnLOriginal.Should().Be(150m);
        // Los totales base NO cambian (sanity): rate 1.00 en la última valoración → mismo valor que Original.
        perf.MarketValue.Should().Be(2100m);
    }
}
