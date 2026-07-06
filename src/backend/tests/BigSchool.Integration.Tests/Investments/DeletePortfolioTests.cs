using System.Net;
using System.Net.Http.Json;
using BigSchool.Domain.SharedKernel.Enums;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Investments;

[Collection(IntegrationCollection.Name)]
public class DeletePortfolioTests : PortfolioEndpointTestBase
{
    public DeletePortfolioTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Delete_WithOpenPosition_Returns409_OpenPositions()
    {
        var (userId, email) = await SeedUserAsync(Currency.EUR);
        var client = AuthenticatedClient(userId, email);
        var portfolioId = await CreatePortfolioViaApiAsync(client);
        await AddHoldingViaApiAsync(client, portfolioId, CompanySanEur, 10m, 100m, "2026-01-01");

        var response = await client.DeleteAsync($"/api/v1/portfolios/{portfolioId}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        env!.Errors.Should().Contain(e => e.Code == "PORTFOLIO_HAS_OPEN_POSITIONS");
    }

    [Fact]
    public async Task Delete_RecentSale_Returns409_FiscalGrace()
    {
        var (userId, email) = await SeedUserAsync(Currency.EUR);
        var client = AuthenticatedClient(userId, email);
        var portfolioId = await CreatePortfolioViaApiAsync(client);
        await AddHoldingViaApiAsync(client, portfolioId, CompanySanEur, 10m, 100m, "2026-01-01");
        var recentSaleDate = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(-1);
        (await client.PostAsJsonAsync($"/api/v1/portfolios/{portfolioId}/sales", new
        {
            companyId = CompanySanEur, shares = 10m, sellPrice = 150m,
            sellDate = recentSaleDate.ToString("yyyy-MM-dd"), notes = (string?)null
        })).EnsureSuccessStatusCode();

        var response = await client.DeleteAsync($"/api/v1/portfolios/{portfolioId}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        env!.Errors.Should().Contain(e => e.Code == "PORTFOLIO_WITHIN_FISCAL_GRACE");
    }

    [Fact]
    public async Task Delete_OldSale_Returns200_AndDisappearsFromListings()
    {
        var (userId, email) = await SeedUserAsync(Currency.EUR);
        var client = AuthenticatedClient(userId, email);
        var portfolioId = await CreatePortfolioViaApiAsync(client);
        var oldSaleDate = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-5).AddDays(-1);
        await AddHoldingViaApiAsync(client, portfolioId, CompanySanEur, 10m, 100m, oldSaleDate.AddYears(-1).ToString("yyyy-MM-dd"));
        (await client.PostAsJsonAsync($"/api/v1/portfolios/{portfolioId}/sales", new
        {
            companyId = CompanySanEur, shares = 10m, sellPrice = 150m,
            sellDate = oldSaleDate.ToString("yyyy-MM-dd"), notes = (string?)null
        })).EnsureSuccessStatusCode();

        var response = await client.DeleteAsync($"/api/v1/portfolios/{portfolioId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = (await (await client.GetAsync("/api/v1/portfolios"))
            .Content.ReadFromJsonAsync<ApiEnvelope<List<PortfolioListItemResponse>>>())!.Data!;
        list.Should().NotContain(p => p.IdPortfolio == portfolioId);
    }

    [Fact]
    public async Task Delete_OtherUsersPortfolio_Returns404()
    {
        var (ownerId, ownerEmail) = await SeedUserAsync();
        var ownerClient = AuthenticatedClient(ownerId, ownerEmail);
        var portfolioId = await CreatePortfolioViaApiAsync(ownerClient);

        var (otherId, otherEmail) = await SeedUserAsync();
        var otherClient = AuthenticatedClient(otherId, otherEmail);

        var response = await otherClient.DeleteAsync($"/api/v1/portfolios/{portfolioId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_WithoutToken_Returns401()
    {
        var client = Factory.CreateClient();
        var response = await client.DeleteAsync("/api/v1/portfolios/1");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
