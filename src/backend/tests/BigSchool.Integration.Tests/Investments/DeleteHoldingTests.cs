using System.Net;
using System.Net.Http.Json;
using BigSchool.Domain.SharedKernel.Enums;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Investments;

[Collection(IntegrationCollection.Name)]
public class DeleteHoldingTests : PortfolioEndpointTestBase
{
    public DeleteHoldingTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Delete_SoftDeletesHolding_ReversesRealized_AndHidesFromReads()
    {
        var (userId, email) = await SeedUserAsync(Currency.EUR);
        var client = AuthenticatedClient(userId, email);
        var portfolioId = await CreatePortfolioViaApiAsync(client);
        var holdingId = await AddHoldingViaApiAsync(client, portfolioId, CompanySanEur, 100m, 4.0m, "2026-01-05");
        var sell = await client.PostAsJsonAsync($"/api/v1/portfolios/{portfolioId}/sales", new
        {
            companyId = CompanySanEur, shares = 40m, sellPrice = 8.0m, sellDate = "2026-03-05"
        });
        sell.EnsureSuccessStatusCode();
        (await ReadPortfolioRealizedPnLAsync(portfolioId)).Should().Be(160m);

        var response = await client.DeleteAsync($"/api/v1/portfolios/{portfolioId}/holdings/{holdingId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        // Soft-delete: la fila persiste con IdStatus=Deleted.
        ((short)(await ReadHoldingStatusAsync(holdingId))!).Should().Be((short)EntityStatus.Deleted);
        // Realizado revertido.
        (await ReadPortfolioRealizedPnLAsync(portfolioId)).Should().Be(0m);
        // Las lecturas posteriores la ocultan.
        var detail = (await (await client.GetAsync($"/api/v1/portfolios/{portfolioId}"))
            .Content.ReadFromJsonAsync<ApiEnvelope<PortfolioDetailResponse>>())!.Data!;
        detail.Holdings.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_ForeignPortfolio_Returns404()
    {
        var (ownerId, ownerEmail) = await SeedUserAsync();
        var ownerClient = AuthenticatedClient(ownerId, ownerEmail);
        var portfolioId = await CreatePortfolioViaApiAsync(ownerClient);
        var holdingId = await AddHoldingViaApiAsync(ownerClient, portfolioId, CompanySanEur, 10m, 4.5m, "2026-01-05");

        var (otherId, otherEmail) = await SeedUserAsync();
        var response = await AuthenticatedClient(otherId, otherEmail)
            .DeleteAsync($"/api/v1/portfolios/{portfolioId}/holdings/{holdingId}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_WithoutToken_Returns401()
    {
        var response = await Factory.CreateClient().DeleteAsync("/api/v1/portfolios/1/holdings/1");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
