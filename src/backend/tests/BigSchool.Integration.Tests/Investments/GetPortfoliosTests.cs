using System.Net;
using System.Net.Http.Json;
using BigSchool.Domain.Enums;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Investments;

[Collection(IntegrationCollection.Name)]
public class GetPortfoliosTests : PortfolioEndpointTestBase
{
    public GetPortfoliosTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Get_ReturnsSummary_WithMarketValueConvertedToBase()
    {
        var (userId, email) = await SeedUserAsync(Currency.EUR);
        var client = AuthenticatedClient(userId, email);
        var portfolioId = await CreatePortfolioViaApiAsync(client, "Tech");

        await SeedExchangeRateAsync(Currency.USD, Currency.EUR, 0.90m, new DateOnly(2026, 1, 5)); // compra
        await SeedExchangeRateAsync(Currency.USD, Currency.EUR, 1.00m, new DateOnly(2026, 3, 2)); // última valoración AAPL
        await AddHoldingViaApiAsync(client, portfolioId, CompanyAaplUsd, 10m, 195m, "2026-01-05");

        var response = await client.GetAsync("/api/v1/portfolios");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = (await response.Content.ReadFromJsonAsync<ApiEnvelope<List<PortfolioListItemResponse>>>())!.Data!;
        var p = list.Single(x => x.IdPortfolio == portfolioId);
        p.Name.Should().Be("Tech");
        p.RealizedPnL.Should().Be(0m);
        // Última valoración AAPL = 210 USD (2026-03-02) × rate 1.00 × 10 = 2100
        p.MarketValue.Should().Be(2100m);
        p.CostBasis.Should().Be(1755m);      // 175.50 × 10
        p.UnrealizedPnL.Should().Be(345m);   // 2100 − 1755
        p.TotalPnL.Should().Be(345m);        // realized 0 + unrealized
    }

    [Fact]
    public async Task Get_WithoutToken_Returns401()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/v1/portfolios");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
