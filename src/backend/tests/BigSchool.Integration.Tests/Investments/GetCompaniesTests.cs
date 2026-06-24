using System.Net;
using System.Net.Http.Json;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Investments;

[Collection(IntegrationCollection.Name)]
public class GetCompaniesTests : CompanyEndpointTestBase
{
    public GetCompaniesTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Get_ReturnsSeededCompanies_PlusCreated()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        var ticker = $"G{Guid.NewGuid():N}".Substring(0, 8).ToUpperInvariant();
        var createdId = await CreateCompanyViaApiAsync(client, new
        {
            name = "Created Co.", ticker, sector = (string?)null, market = (string?)null, currency = "EUR"
        });

        var response = await client.GetAsync("/api/v1/companies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<CompanyListItemResponse>>>();
        env!.Data!.Should().HaveCount(SeededCompaniesCount + 1);
        env.Data!.Should().Contain(c => c.IdCompany == createdId && c.Ticker == ticker);
        // La empresa AAPL semilla trae su última cotización (2026-03-02 = 210).
        var aapl = env.Data!.Single(c => c.Ticker == "AAPL");
        aapl.LastPrice.Should().Be(210.0000m);
        aapl.LastValuationDate.Should().Be("2026-03-02");
    }

    [Fact]
    public async Task Get_FilterByMarket_ReturnsOnlyThatMarket()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);

        // Seed: AAPL y MSFT son NASDAQ.
        var response = await client.GetAsync("/api/v1/companies?market=NASDAQ");

        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<CompanyListItemResponse>>>();
        env!.Data!.Should().OnlyContain(c => c.Market == "NASDAQ");
        env.Data!.Should().Contain(c => c.Ticker == "AAPL");
        env.Data!.Should().Contain(c => c.Ticker == "MSFT");
    }

    [Fact]
    public async Task Get_WithoutToken_Returns401()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/v1/companies");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
