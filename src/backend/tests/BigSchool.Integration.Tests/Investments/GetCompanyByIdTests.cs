using System.Net;
using System.Net.Http.Json;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Investments;

[Collection(IntegrationCollection.Name)]
public class GetCompanyByIdTests : CompanyEndpointTestBase
{
    public GetCompanyByIdTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetById_ExistingCompany_ReturnsDetail_WithLastValuation()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        var ticker = $"D{Guid.NewGuid():N}".Substring(0, 8).ToUpperInvariant();
        var id = await CreateCompanyViaApiAsync(client, new
        {
            name = "Detail Co.", ticker, sector = "Energy", market = "LSE", currency = "GBP"
        });
        await client.PostAsJsonAsync($"/api/v1/companies/{id}/valuations", new { price = 10m, date = "2026-01-10", source = (string?)null });
        await client.PostAsJsonAsync($"/api/v1/companies/{id}/valuations", new { price = 12m, date = "2026-03-10", source = (string?)null });

        var response = await client.GetAsync($"/api/v1/companies/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = (await response.Content.ReadFromJsonAsync<ApiEnvelope<CompanyListItemResponse>>())!.Data!;
        dto.IdCompany.Should().Be(id);
        dto.Ticker.Should().Be(ticker);
        dto.Currency.Should().Be("GBP");
        dto.LastPrice.Should().Be(12.0000m);              // la más reciente
        dto.LastValuationDate.Should().Be("2026-03-10");
    }

    [Fact]
    public async Task GetById_NonExistent_Returns404()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);

        var response = await client.GetAsync("/api/v1/companies/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        env!.Errors.Should().Contain(e => e.Code == "ENTITY_NOT_FOUND");
    }

    [Fact]
    public async Task GetById_WithoutToken_Returns401()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/v1/companies/1");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
