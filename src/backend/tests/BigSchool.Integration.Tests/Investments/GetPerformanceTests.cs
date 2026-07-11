using System.Net;
using System.Net.Http.Json;
using BigSchool.Domain.SharedKernel.Enums;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Investments;

[Collection(IntegrationCollection.Name)]
public class GetPerformanceTests : PortfolioEndpointTestBase
{
    public GetPerformanceTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Performance_UnrealizedOnly_UsdHolding()
    {
        var (userId, email) = await SeedUserAsync(Currency.EUR);
        var client = AuthenticatedClient(userId, email);
        var portfolioId = await CreatePortfolioViaApiAsync(client, "Tech");
        await SeedExchangeRateAsync(Currency.USD, Currency.EUR, 0.90m, new DateOnly(2026, 1, 5));
        await SeedExchangeRateAsync(Currency.USD, Currency.EUR, 1.00m, new DateOnly(2026, 3, 2)); // fecha última valoración AAPL
        await AddHoldingViaApiAsync(client, portfolioId, CompanyAaplUsd, 10m, 195m, "2026-01-05");

        var response = await client.GetAsync($"/api/v1/portfolios/{portfolioId}/performance");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var perf = (await response.Content.ReadFromJsonAsync<ApiEnvelope<PerformanceResponse>>())!.Data!;
        perf.BaseCurrency.Should().Be("EUR");
        perf.MarketValue.Should().Be(2100m);   // 210 USD × 1.00 × 10
        perf.CostBasis.Should().Be(1755m);      // 175.50 × 10
        perf.UnrealizedPnL.Should().Be(345m);
        perf.RealizedPnL.Should().Be(0m);
        perf.TotalPnL.Should().Be(345m);
        perf.ReturnPct.Should().Be(19.66m);     // 345/1755*100
        perf.Holdings.Should().ContainSingle();
        perf.Holdings[0].Ticker.Should().Be("AAPL");
        perf.Holdings[0].UnrealizedPnLPct.Should().Be(19.66m);
    }

    [Fact]
    public async Task Performance_FullCycle_RealizedAndUnrealized()
    {
        var (userId, email) = await SeedUserAsync(Currency.EUR);
        var client = AuthenticatedClient(userId, email);
        var portfolioId = await CreatePortfolioViaApiAsync(client, "Mixto");

        // SAN EUR (rate 1). Compra 100 @ 4; vende 40 @ 8 → realizado 160. Quedan 60 abiertas.
        await AddHoldingViaApiAsync(client, portfolioId, CompanySanEur, 100m, 4.0m, "2026-01-05");
        var sell = await client.PostAsJsonAsync($"/api/v1/portfolios/{portfolioId}/sales", new
        {
            companyId = CompanySanEur, shares = 40m, sellPrice = 8.0m, sellDate = "2026-03-05"
        });
        sell.EnsureSuccessStatusCode();

        var response = await client.GetAsync($"/api/v1/portfolios/{portfolioId}/performance");
        var perf = (await response.Content.ReadFromJsonAsync<ApiEnvelope<PerformanceResponse>>())!.Data!;

        perf.RealizedPnL.Should().Be(160m);     // (8-4)*40
        // Última valoración SAN = 4.8 EUR (2026-03-02). Quedan 60 abiertas.
        perf.MarketValue.Should().Be(288m);     // 60 × 4.8
        perf.CostBasis.Should().Be(240m);       // 60 × 4
        perf.UnrealizedPnL.Should().Be(48m);    // 288 − 240
        perf.TotalPnL.Should().Be(208m);        // 160 + 48
        perf.ReturnPct.Should().Be(20.00m);     // 48/240*100
    }

    [Fact]
    public async Task Performance_ForeignPortfolio_Returns404()
    {
        var (ownerId, ownerEmail) = await SeedUserAsync();
        var portfolioId = await CreatePortfolioViaApiAsync(AuthenticatedClient(ownerId, ownerEmail));
        var (otherId, otherEmail) = await SeedUserAsync();

        var response = await AuthenticatedClient(otherId, otherEmail)
            .GetAsync($"/api/v1/portfolios/{portfolioId}/performance");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Performance_WithoutToken_Returns401()
    {
        var response = await Factory.CreateClient().GetAsync("/api/v1/portfolios/1/performance");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
