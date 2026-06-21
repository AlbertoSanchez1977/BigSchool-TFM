using System.Net;
using System.Net.Http.Json;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Investments;

[Collection(IntegrationCollection.Name)]
public class CreateCompanyTests : CompanyEndpointTestBase
{
    public CreateCompanyTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Post_NewCompany_ReturnsDto_AndPersists()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        var ticker = $"T{Guid.NewGuid():N}".Substring(0, 8).ToUpperInvariant();

        var response = await client.PostAsJsonAsync("/api/v1/companies", new
        {
            name = "Nvidia Corp.",
            ticker,
            sector = "Technology",
            market = "NASDAQ",
            currency = "USD"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<CompanyResponse>>();
        env!.Errors.Should().BeEmpty();
        var dto = env.Data!;
        dto.IdCompany.Should().BeGreaterThan(SeededCompaniesCount);
        dto.Name.Should().Be("Nvidia Corp.");
        dto.Ticker.Should().Be(ticker);
        dto.Sector.Should().Be("Technology");
        dto.Market.Should().Be("NASDAQ");
        dto.Currency.Should().Be("USD");

        await using var conn = new MySqlConnector.MySqlConnection(Fixture.ConnectionString);
        var row = await Dapper.SqlMapper.QuerySingleAsync(conn,
            "SELECT Name, Ticker, Currency, IdStatus FROM Companies WHERE IdCompany = @id;",
            new { id = dto.IdCompany });
        ((string)row.Name).Should().Be("Nvidia Corp.");
        ((string)row.Ticker).Should().Be(ticker);
        ((string)row.Currency).Should().Be("USD");
        ((short)row.IdStatus).Should().Be((short)BigSchool.Domain.Enums.EntityStatus.Active);
    }

    [Fact]
    public async Task Post_DuplicateTicker_Returns409()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);

        // El ticker AAPL existe en el seed (IdCompany 1).
        var response = await client.PostAsJsonAsync("/api/v1/companies", new
        {
            name = "Apple Duplicada", ticker = "AAPL", sector = (string?)null, market = (string?)null, currency = "USD"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        env!.Errors.Should().Contain(e => e.Code == "DUPLICATE_TICKER");
    }

    [Theory]
    [InlineData("", "VALIDTKR", "USD")]                 // name vacío
    [InlineData("Empresa", "", "USD")]                  // ticker vacío
    [InlineData("Empresa", "TOOOOOLONGTICKER", "USD")]  // ticker > 10
    public async Task Post_InvalidPayload_Returns400(string name, string ticker, string currency)
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);

        var response = await client.PostAsJsonAsync("/api/v1/companies", new { name, ticker, sector = (string?)null, market = (string?)null, currency });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        env!.Errors.Should().Contain(e => e.Code == "VALIDATION_ERROR");
    }

    [Fact]
    public async Task Post_WithoutToken_Returns401()
    {
        var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/companies", new
        {
            name = "X", ticker = "X", sector = (string?)null, market = (string?)null, currency = "USD"
        });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
