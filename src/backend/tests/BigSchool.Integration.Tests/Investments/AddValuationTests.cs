using System.Net;
using System.Net.Http.Json;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Investments;

[Collection(IntegrationCollection.Name)]
public class AddValuationTests : CompanyEndpointTestBase
{
    public AddValuationTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    private async Task<int> CreateUsdCompanyAsync(HttpClient client)
    {
        var ticker = $"V{Guid.NewGuid():N}".Substring(0, 8).ToUpperInvariant();
        return await CreateCompanyViaApiAsync(client, new
        {
            name = "Test Co.", ticker, sector = (string?)null, market = (string?)null, currency = "USD"
        });
    }

    [Fact]
    public async Task Post_Valuation_ReturnsDtoInCompanyCurrency_AndPersists()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        var companyId = await CreateUsdCompanyAsync(client);

        var response = await client.PostAsJsonAsync($"/api/v1/companies/{companyId}/valuations", new
        {
            price = 195.5000m, date = "2026-02-15", source = "manual"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = (await response.Content.ReadFromJsonAsync<ApiEnvelope<ValuationResponse>>())!.Data!;
        dto.IdValuation.Should().BeGreaterThan(0);
        dto.IdCompany.Should().Be(companyId);
        dto.Price.Should().Be(195.5000m);
        dto.Currency.Should().Be("USD");          // hereda la moneda de la empresa
        dto.Date.Should().Be("2026-02-15");
        dto.Source.Should().Be("manual");

        await using var conn = new MySqlConnector.MySqlConnection(Fixture.ConnectionString);
        var row = await Dapper.SqlMapper.QuerySingleAsync(conn,
            "SELECT Price, PriceCurrency, IdCompany FROM Valuations WHERE IdValuation = @id;",
            new { id = dto.IdValuation });
        ((decimal)row.Price).Should().Be(195.5000m);
        ((string)row.PriceCurrency).Should().Be("USD");
        ((int)row.IdCompany).Should().Be(companyId);
    }

    [Fact]
    public async Task Post_Valuation_DuplicateDate_Returns409()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        var companyId = await CreateUsdCompanyAsync(client);
        await client.PostAsJsonAsync($"/api/v1/companies/{companyId}/valuations", new { price = 100m, date = "2026-02-15", source = (string?)null });

        var response = await client.PostAsJsonAsync($"/api/v1/companies/{companyId}/valuations", new { price = 110m, date = "2026-02-15", source = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        env!.Errors.Should().Contain(e => e.Code == "DUPLICATE_VALUATION");
    }

    [Fact]
    public async Task Post_Valuation_NonExistentCompany_Returns404()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);

        var response = await client.PostAsJsonAsync("/api/v1/companies/999999/valuations", new { price = 100m, date = "2026-02-15", source = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        env!.Errors.Should().Contain(e => e.Code == "ENTITY_NOT_FOUND");
    }

    [Fact]
    public async Task Post_Valuation_NonPositivePrice_Returns400()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        var companyId = await CreateUsdCompanyAsync(client);

        var response = await client.PostAsJsonAsync($"/api/v1/companies/{companyId}/valuations", new { price = 0m, date = "2026-02-15", source = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        env!.Errors.Should().Contain(e => e.Code == "VALIDATION_ERROR" && e.Field == "Price");
    }

    [Fact]
    public async Task Post_Valuation_FutureDate_Returns400()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        var companyId = await CreateUsdCompanyAsync(client);
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1).ToString("yyyy-MM-dd");

        var response = await client.PostAsJsonAsync($"/api/v1/companies/{companyId}/valuations",
            new { price = 100m, date = futureDate, source = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        env!.Errors.Should().Contain(e => e.Code == "VALIDATION_ERROR" && e.Field == "Date");
    }

    [Fact]
    public async Task Post_Valuation_WithoutToken_Returns401()
    {
        var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/companies/1/valuations", new { price = 100m, date = "2026-02-15", source = (string?)null });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
