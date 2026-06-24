using System.Net;
using System.Net.Http.Json;
using BigSchool.Domain.Enums;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Investments;

[Collection(IntegrationCollection.Name)]
public class PutHoldingTests : PortfolioEndpointTestBase
{
    public PutHoldingTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Put_UpdatesNotes_AndPersists()
    {
        var (userId, email) = await SeedUserAsync(Currency.EUR);
        var client = AuthenticatedClient(userId, email);
        var portfolioId = await CreatePortfolioViaApiAsync(client);
        var holdingId = await AddHoldingViaApiAsync(client, portfolioId, CompanySanEur, 100m, 4.5m, "2026-01-05", "old");

        var response = await client.PutAsJsonAsync(
            $"/api/v1/portfolios/{portfolioId}/holdings/{holdingId}", new { notes = "actualizado" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = (await response.Content.ReadFromJsonAsync<ApiEnvelope<HoldingResponse>>())!.Data!;
        dto.Notes.Should().Be("actualizado");

        await using var conn = new MySqlConnector.MySqlConnection(Fixture.ConnectionString);
        var notes = await Dapper.SqlMapper.QuerySingleAsync<string>(conn,
            "SELECT Notes FROM Holdings WHERE IdHolding = @id;", new { id = holdingId });
        notes.Should().Be("actualizado");
    }

    [Fact]
    public async Task Put_ForeignPortfolio_Returns404()
    {
        var (ownerId, ownerEmail) = await SeedUserAsync();
        var ownerClient = AuthenticatedClient(ownerId, ownerEmail);
        var portfolioId = await CreatePortfolioViaApiAsync(ownerClient);
        var holdingId = await AddHoldingViaApiAsync(ownerClient, portfolioId, CompanySanEur, 10m, 4.5m, "2026-01-05");

        var (otherId, otherEmail) = await SeedUserAsync();
        var response = await AuthenticatedClient(otherId, otherEmail).PutAsJsonAsync(
            $"/api/v1/portfolios/{portfolioId}/holdings/{holdingId}", new { notes = "x" });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Put_WithoutToken_Returns401()
    {
        var response = await Factory.CreateClient().PutAsJsonAsync(
            "/api/v1/portfolios/1/holdings/1", new { notes = "x" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
