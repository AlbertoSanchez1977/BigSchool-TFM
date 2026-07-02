using System.Net;
using System.Net.Http.Json;
using BigSchool.Domain.SharedKernel.Enums;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Investments;

[Collection(IntegrationCollection.Name)]
public class GetPortfolioByIdTests : PortfolioEndpointTestBase
{
    public GetPortfolioByIdTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetById_ReturnsDetail_WithHoldings()
    {
        var (userId, email) = await SeedUserAsync(Currency.EUR);
        var client = AuthenticatedClient(userId, email);
        var portfolioId = await CreatePortfolioViaApiAsync(client, "Mixto");
        await AddHoldingViaApiAsync(client, portfolioId, CompanySanEur, 100m, 4.5m, "2026-01-05", "lote SAN");

        var response = await client.GetAsync($"/api/v1/portfolios/{portfolioId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = (await response.Content.ReadFromJsonAsync<ApiEnvelope<PortfolioDetailResponse>>())!.Data!;
        detail.IdPortfolio.Should().Be(portfolioId);
        detail.Name.Should().Be("Mixto");
        detail.Holdings.Should().ContainSingle();
        var h = detail.Holdings[0];
        h.Ticker.Should().Be("SAN");
        h.CompanyCurrency.Should().Be("EUR");
        h.Shares.Should().Be(100m);
        h.OpenShares.Should().Be(100m);
        h.BuyBaseAmount.Should().Be(4.5m);
        h.Notes.Should().Be("lote SAN");
    }

    [Fact]
    public async Task GetById_ForeignPortfolio_Returns404()
    {
        var (ownerId, ownerEmail) = await SeedUserAsync();
        var portfolioId = await CreatePortfolioViaApiAsync(AuthenticatedClient(ownerId, ownerEmail));

        var (otherId, otherEmail) = await SeedUserAsync();
        var response = await AuthenticatedClient(otherId, otherEmail).GetAsync($"/api/v1/portfolios/{portfolioId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_WithoutToken_Returns401()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/v1/portfolios/1");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
