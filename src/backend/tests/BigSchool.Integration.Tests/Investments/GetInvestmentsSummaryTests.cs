using System.Net;
using System.Net.Http.Json;
using BigSchool.Domain.SharedKernel.Enums;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Investments;

[Collection(IntegrationCollection.Name)]
public class GetInvestmentsSummaryTests : PortfolioEndpointTestBase
{
    public GetInvestmentsSummaryTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Summary_AggregatesMultiplePortfolios()
    {
        var (userId, email) = await SeedUserAsync(Currency.EUR);
        var client = AuthenticatedClient(userId, email);
        await SeedExchangeRateAsync(Currency.USD, Currency.EUR, 1.00m, new DateOnly(2026, 3, 2));
        var p1 = await CreatePortfolioViaApiAsync(client, "A");
        var p2 = await CreatePortfolioViaApiAsync(client, "B");
        await AddHoldingViaApiAsync(client, p1, CompanyAaplUsd, 10m, 195m, "2026-01-05");
        await AddHoldingViaApiAsync(client, p2, CompanyAaplUsd, 5m, 195m, "2026-01-05");

        var perf1 = (await (await client.GetAsync($"/api/v1/portfolios/{p1}/performance")).Content.ReadFromJsonAsync<ApiEnvelope<PerformanceResponse>>())!.Data!;
        var perf2 = (await (await client.GetAsync($"/api/v1/portfolios/{p2}/performance")).Content.ReadFromJsonAsync<ApiEnvelope<PerformanceResponse>>())!.Data!;
        var sum = (await (await client.GetAsync("/api/v1/portfolios/summary")).Content.ReadFromJsonAsync<ApiEnvelope<InvestmentsSummaryResponse>>())!.Data!;

        sum.PortfolioCount.Should().Be(2);
        sum.MarketValue.Should().Be(perf1.MarketValue + perf2.MarketValue);
        sum.CostBasis.Should().Be(perf1.CostBasis + perf2.CostBasis);
        sum.BaseCurrency.Should().Be("EUR");
    }

    [Fact]
    public async Task Summary_NoPortfolios_ReturnsAllZeros()
    {
        var (userId, email) = await SeedUserAsync(Currency.EUR);
        var client = AuthenticatedClient(userId, email);

        var response = await client.GetAsync("/api/v1/portfolios/summary");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var sum = (await response.Content.ReadFromJsonAsync<ApiEnvelope<InvestmentsSummaryResponse>>())!.Data!;
        sum.PortfolioCount.Should().Be(0);
        sum.MarketValue.Should().Be(0m);
        sum.CostBasis.Should().Be(0m);
        sum.RealizedPnL.Should().Be(0m);
        sum.BaseCurrency.Should().Be("EUR");
    }

    [Fact]
    public async Task Summary_WithoutToken_Returns401()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/v1/portfolios/summary");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private record InvestmentsSummaryResponse(string BaseCurrency, decimal MarketValue, decimal CostBasis, decimal UnrealizedPnL, decimal RealizedPnL, decimal TotalPnL, decimal ReturnPct, int PortfolioCount);
}
