using System.Net;
using System.Net.Http.Json;
using BigSchool.Domain.SharedKernel.Enums;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Investments;

[Collection(IntegrationCollection.Name)]
public class PostSaleTests : PortfolioEndpointTestBase
{
    public PostSaleTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Sell_FifoAcrossLots_GeneratesNDisposals_AndConsolidatesRealized()
    {
        var (userId, email) = await SeedUserAsync(Currency.EUR);
        var client = AuthenticatedClient(userId, email);
        var portfolioId = await CreatePortfolioViaApiAsync(client, "FIFO");

        // Dos lotes de SAN (EUR, rate 1) con fechas distintas → FIFO determinista sin FX.
        var lotA = await AddHoldingViaApiAsync(client, portfolioId, CompanySanEur, 100m, 4.0m, "2026-01-05", "A");
        var lotB = await AddHoldingViaApiAsync(client, portfolioId, CompanySanEur, 100m, 6.0m, "2026-02-05", "B");

        // Vender 150 @ 8 → consume A (100) y parte de B (50).
        var response = await client.PostAsJsonAsync($"/api/v1/portfolios/{portfolioId}/sales", new
        {
            companyId = CompanySanEur, shares = 150m, sellPrice = 8.0m, sellDate = "2026-03-05", notes = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = (await response.Content.ReadFromJsonAsync<ApiEnvelope<SellResultResponse>>())!.Data!;
        result.Disposals.Should().HaveCount(2);
        result.Disposals[0].IdHolding.Should().Be(lotA);
        result.Disposals[0].Shares.Should().Be(100m);
        result.Disposals[0].RealizedPnL.Should().Be(400m); // (8-4)*100
        result.Disposals[1].IdHolding.Should().Be(lotB);
        result.Disposals[1].Shares.Should().Be(50m);
        result.Disposals[1].RealizedPnL.Should().Be(100m); // (8-6)*50
        result.PortfolioRealizedPnL.Should().Be(500m);
        result.RealizedPnLCurrency.Should().Be("EUR");

        // Persistencia física: RealizedPnL de la cartera, disposals y open shares.
        (await ReadPortfolioRealizedPnLAsync(portfolioId)).Should().Be(500m);
        (await CountActiveDisposalsAsync(lotA)).Should().Be(1);
        (await CountActiveDisposalsAsync(lotB)).Should().Be(1);

        var detail = (await (await client.GetAsync($"/api/v1/portfolios/{portfolioId}"))
            .Content.ReadFromJsonAsync<ApiEnvelope<PortfolioDetailResponse>>())!.Data!;
        detail.Holdings.Single(h => h.IdHolding == lotA).OpenShares.Should().Be(0m);
        detail.Holdings.Single(h => h.IdHolding == lotB).OpenShares.Should().Be(50m);
    }

    [Fact]
    public async Task Sell_UsdLot_RealizedIncludesFxEffect()
    {
        var (userId, email) = await SeedUserAsync(Currency.EUR);
        var client = AuthenticatedClient(userId, email);
        var portfolioId = await CreatePortfolioViaApiAsync(client, "USD");

        await SeedExchangeRateAsync(Currency.USD, Currency.EUR, 0.90m, new DateOnly(2026, 1, 5)); // compra
        await SeedExchangeRateAsync(Currency.USD, Currency.EUR, 1.00m, new DateOnly(2026, 3, 5)); // venta
        var lot = await AddHoldingViaApiAsync(client, portfolioId, CompanyAaplUsd, 10m, 195m, "2026-01-05");

        var response = await client.PostAsJsonAsync($"/api/v1/portfolios/{portfolioId}/sales", new
        {
            companyId = CompanyAaplUsd, shares = 10m, sellPrice = 200m, sellDate = "2026-03-05", notes = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = (await response.Content.ReadFromJsonAsync<ApiEnvelope<SellResultResponse>>())!.Data!;
        var d = result.Disposals.Single();
        d.IdHolding.Should().Be(lot);
        d.SellOriginalAmount.Should().Be(200m);
        d.SellOriginalCurrency.Should().Be("USD");
        d.SellExchangeRate.Should().Be(1.00m);
        d.SellBaseAmount.Should().Be(200m);          // 200 * 1.00
        // coste base 175.50/u (195*0.9); venta base 200/u → realizado (200-175.50)*10 = 245
        d.RealizedPnL.Should().Be(245m);
        result.PortfolioRealizedPnL.Should().Be(245m);
        (await ReadPortfolioRealizedPnLAsync(portfolioId)).Should().Be(245m);
    }

    [Fact]
    public async Task Sell_MoreThanAvailable_Returns400_InsufficientShares()
    {
        var (userId, email) = await SeedUserAsync(Currency.EUR);
        var client = AuthenticatedClient(userId, email);
        var portfolioId = await CreatePortfolioViaApiAsync(client);
        await AddHoldingViaApiAsync(client, portfolioId, CompanySanEur, 100m, 4.0m, "2026-01-05");

        var response = await client.PostAsJsonAsync($"/api/v1/portfolios/{portfolioId}/sales", new
        {
            companyId = CompanySanEur, shares = 101m, sellPrice = 8.0m, sellDate = "2026-03-05"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        env!.Errors.Should().Contain(e => e.Code == "INSUFFICIENT_SHARES");
    }

    [Fact]
    public async Task Sell_FutureSellDate_Returns400()
    {
        var (userId, email) = await SeedUserAsync(Currency.EUR);
        var client = AuthenticatedClient(userId, email);
        var portfolioId = await CreatePortfolioViaApiAsync(client);
        await AddHoldingViaApiAsync(client, portfolioId, CompanySanEur, 100m, 4.0m, "2026-01-05");
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1).ToString("yyyy-MM-dd");

        var response = await client.PostAsJsonAsync($"/api/v1/portfolios/{portfolioId}/sales", new
        {
            companyId = CompanySanEur, shares = 10m, sellPrice = 8.0m, sellDate = futureDate
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        env!.Errors.Should().Contain(e => e.Code == "VALIDATION_ERROR" && e.Field == "SellDate");
    }

    [Fact]
    public async Task Sell_WithoutToken_Returns401()
    {
        var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/portfolios/1/sales",
            new { companyId = 3, shares = 1m, sellPrice = 8m, sellDate = "2026-03-05" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Sell_ForeignPortfolio_Returns404()
    {
        var (ownerId, ownerEmail) = await SeedUserAsync();
        var ownerClient = AuthenticatedClient(ownerId, ownerEmail);
        var portfolioId = await CreatePortfolioViaApiAsync(ownerClient);
        await AddHoldingViaApiAsync(ownerClient, portfolioId, CompanySanEur, 100m, 4.0m, "2026-01-05");

        var (otherId, otherEmail) = await SeedUserAsync();
        var response = await AuthenticatedClient(otherId, otherEmail).PostAsJsonAsync(
            $"/api/v1/portfolios/{portfolioId}/sales",
            new { companyId = CompanySanEur, shares = 10m, sellPrice = 8m, sellDate = "2026-03-05" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
