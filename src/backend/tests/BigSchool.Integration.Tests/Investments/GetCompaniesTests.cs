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

    [Fact]
    public async Task Get_Paginado_DevuelvePaginaYMeta_SinSolape()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        await CreateCompanyViaApiAsync(client, new { name = "Extra A", ticker = "PGA1", currency = "EUR" });
        await CreateCompanyViaApiAsync(client, new { name = "Extra B", ticker = "PGB1", currency = "EUR" });
        var total = SeededCompaniesCount + 2;

        var r1 = await client.GetAsync("/api/v1/companies?page=1&pageSize=2");
        r1.StatusCode.Should().Be(HttpStatusCode.OK);
        var e1 = await r1.Content.ReadFromJsonAsync<ApiEnvelope<List<CompanyListItemResponse>>>();
        e1!.Data!.Should().HaveCount(2);
        e1.Meta!.Page.Should().Be(1);
        e1.Meta.PageSize.Should().Be(2);
        e1.Meta.TotalCount.Should().Be(total);

        var r2 = await client.GetAsync("/api/v1/companies?page=2&pageSize=2");
        var e2 = await r2.Content.ReadFromJsonAsync<ApiEnvelope<List<CompanyListItemResponse>>>();
        e2!.Data!.Should().HaveCount(2);
        e1.Data!.Select(c => c.IdCompany).Should().NotIntersectWith(e2.Data!.Select(c => c.IdCompany));
    }

    [Fact]
    public async Task Get_PageSizeSobreMax_SeCapAa100()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);

        var resp = await client.GetAsync("/api/v1/companies?pageSize=500");

        var env = await resp.Content.ReadFromJsonAsync<ApiEnvelope<List<CompanyListItemResponse>>>();
        env!.Meta!.PageSize.Should().Be(100);
        env.Meta.TotalCount.Should().Be(SeededCompaniesCount);
    }
}
