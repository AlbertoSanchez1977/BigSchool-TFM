using System.Net;
using System.Net.Http.Json;
using BigSchool.Domain.SharedKernel.Enums;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Investments;

[Collection(IntegrationCollection.Name)]
public class PostHoldingTests : PortfolioEndpointTestBase
{
    public PostHoldingTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Post_UsdHolding_FreezesConvertedCost_AndPersists()
    {
        var (userId, email) = await SeedUserAsync(Currency.EUR);
        var client = AuthenticatedClient(userId, email);
        var portfolioId = await CreatePortfolioViaApiAsync(client);
        // Tasa exacta a la fecha de compra (sin red).
        await SeedExchangeRateAsync(Currency.USD, Currency.EUR, 0.90m, new DateOnly(2026, 1, 5));

        var response = await client.PostAsJsonAsync($"/api/v1/portfolios/{portfolioId}/holdings", new
        {
            idCompany = CompanyAaplUsd,
            shares = 10m,
            buyPrice = 195m,
            buyDate = "2026-01-05",
            notes = "primer lote"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = (await response.Content.ReadFromJsonAsync<ApiEnvelope<HoldingResponse>>())!.Data!;
        dto.IdHolding.Should().BeGreaterThan(0);
        dto.IdCompany.Should().Be(CompanyAaplUsd);
        dto.Shares.Should().Be(10m);
        dto.BuyOriginalAmount.Should().Be(195m);
        dto.BuyOriginalCurrency.Should().Be("USD");
        dto.BuyExchangeRate.Should().Be(0.90m);
        dto.BuyBaseAmount.Should().Be(175.50m); // 195 * 0.9
        dto.BuyBaseCurrency.Should().Be("EUR");
        dto.BuyDate.Should().Be("2026-01-05");
        dto.Notes.Should().Be("primer lote");

        await using var conn = new MySqlConnector.MySqlConnection(Fixture.ConnectionString);
        var row = await Dapper.SqlMapper.QuerySingleAsync(conn,
            @"SELECT IdCompany, Shares, BuyOriginalAmount, BuyOriginalCurrency, BuyExchangeRate,
                     BuyBaseAmount, BuyBaseCurrency, IdStatus
              FROM Holdings WHERE IdHolding = @id;", new { id = dto.IdHolding });
        ((int)row.IdCompany).Should().Be(CompanyAaplUsd);
        ((decimal)row.Shares).Should().Be(10m);
        ((decimal)row.BuyBaseAmount).Should().Be(175.50m);
        ((string)row.BuyOriginalCurrency).Should().Be("USD");
        ((short)row.IdStatus).Should().Be((short)EntityStatus.Active);
    }

    [Fact]
    public async Task Post_EurHolding_UsesRateOne()
    {
        var (userId, email) = await SeedUserAsync(Currency.EUR);
        var client = AuthenticatedClient(userId, email);
        var portfolioId = await CreatePortfolioViaApiAsync(client);

        var response = await client.PostAsJsonAsync($"/api/v1/portfolios/{portfolioId}/holdings", new
        {
            idCompany = CompanySanEur, shares = 100m, buyPrice = 4.5m, buyDate = "2026-01-05", notes = (string?)null
        });

        var dto = (await response.Content.ReadFromJsonAsync<ApiEnvelope<HoldingResponse>>())!.Data!;
        dto.BuyExchangeRate.Should().Be(1m);
        dto.BuyBaseAmount.Should().Be(4.5m);
        dto.BuyBaseCurrency.Should().Be("EUR");
    }

    [Fact]
    public async Task Post_WithoutToken_Returns401()
    {
        var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/portfolios/1/holdings",
            new { idCompany = 1, shares = 1m, buyPrice = 1m, buyDate = "2026-01-05" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_SharesZero_Returns400()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        var portfolioId = await CreatePortfolioViaApiAsync(client);

        var response = await client.PostAsJsonAsync($"/api/v1/portfolios/{portfolioId}/holdings",
            new { idCompany = CompanySanEur, shares = 0m, buyPrice = 4.5m, buyDate = "2026-01-05" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        env!.Errors.Should().Contain(e => e.Code == "VALIDATION_ERROR" && e.Field == "Shares");
    }

    [Fact]
    public async Task Post_ForeignPortfolio_Returns404()
    {
        var (ownerId, ownerEmail) = await SeedUserAsync();
        var ownerClient = AuthenticatedClient(ownerId, ownerEmail);
        var portfolioId = await CreatePortfolioViaApiAsync(ownerClient);

        var (otherId, otherEmail) = await SeedUserAsync();
        var otherClient = AuthenticatedClient(otherId, otherEmail);

        var response = await otherClient.PostAsJsonAsync($"/api/v1/portfolios/{portfolioId}/holdings",
            new { idCompany = CompanySanEur, shares = 10m, buyPrice = 4.5m, buyDate = "2026-01-05" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        env!.Errors.Should().Contain(e => e.Code == "ENTITY_NOT_FOUND");
    }
}
