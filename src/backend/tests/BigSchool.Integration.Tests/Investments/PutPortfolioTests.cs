using System.Net;
using System.Net.Http.Json;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Investments;

[Collection(IntegrationCollection.Name)]
public class PutPortfolioTests : PortfolioEndpointTestBase
{
    public PutPortfolioTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Put_ValidName_RenamesPortfolio()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        var portfolioId = await CreatePortfolioViaApiAsync(client, "Original");

        var response = await client.PutAsJsonAsync($"/api/v1/portfolios/{portfolioId}", new { name = "Nueva cartera" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = (await (await client.GetAsync($"/api/v1/portfolios/{portfolioId}"))
            .Content.ReadFromJsonAsync<ApiEnvelope<PortfolioDetailResponse>>())!.Data!;
        detail.Name.Should().Be("Nueva cartera");
    }

    [Fact]
    public async Task Put_EmptyName_Returns400()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        var portfolioId = await CreatePortfolioViaApiAsync(client);

        var response = await client.PutAsJsonAsync($"/api/v1/portfolios/{portfolioId}", new { name = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_OtherUsersPortfolio_Returns404()
    {
        var (ownerId, ownerEmail) = await SeedUserAsync();
        var ownerClient = AuthenticatedClient(ownerId, ownerEmail);
        var portfolioId = await CreatePortfolioViaApiAsync(ownerClient);

        var (otherId, otherEmail) = await SeedUserAsync();
        var otherClient = AuthenticatedClient(otherId, otherEmail);

        var response = await otherClient.PutAsJsonAsync($"/api/v1/portfolios/{portfolioId}", new { name = "Robada" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Put_WithoutToken_Returns401()
    {
        var client = Factory.CreateClient();
        var response = await client.PutAsJsonAsync("/api/v1/portfolios/1", new { name = "X" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
