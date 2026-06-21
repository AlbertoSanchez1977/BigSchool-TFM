using System.Net;
using System.Net.Http.Json;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Investments;

[Collection(IntegrationCollection.Name)]
public class GetCompanyValuationsTests : CompanyEndpointTestBase
{
    public GetCompanyValuationsTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetValuations_ReturnsHistory_OrderedByDateDesc()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        var ticker = $"H{Guid.NewGuid():N}".Substring(0, 8).ToUpperInvariant();
        var id = await CreateCompanyViaApiAsync(client, new
        {
            name = "History Co.", ticker, sector = (string?)null, market = (string?)null, currency = "USD"
        });
        await client.PostAsJsonAsync($"/api/v1/companies/{id}/valuations", new { price = 100m, date = "2026-01-10", source = (string?)null });
        await client.PostAsJsonAsync($"/api/v1/companies/{id}/valuations", new { price = 120m, date = "2026-03-10", source = (string?)null });

        var response = await client.GetAsync($"/api/v1/companies/{id}/valuations");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = (await response.Content.ReadFromJsonAsync<ApiEnvelope<List<ValuationListItemResponse>>>())!.Data!;
        list.Should().HaveCount(2);
        list[0].Date.Should().Be("2026-03-10");          // orden fecha desc
        list[0].Price.Should().Be(120.0000m);
        list[0].PriceCurrency.Should().Be("USD");
        list.Should().OnlyContain(v => v.IdCompany == id);
    }

    [Fact]
    public async Task GetValuations_NonExistentCompany_ReturnsEmptyList()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);

        var response = await client.GetAsync("/api/v1/companies/999999/valuations");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = (await response.Content.ReadFromJsonAsync<ApiEnvelope<List<ValuationListItemResponse>>>())!.Data!;
        list.Should().BeEmpty();
    }

    [Fact]
    public async Task GetValuations_WithoutToken_Returns401()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/v1/companies/1/valuations");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
