using System.Net;
using System.Net.Http.Json;
using BigSchool.Domain.SharedKernel.Enums;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Investments;

[Collection(IntegrationCollection.Name)]
public class PostPortfolioTests : PortfolioEndpointTestBase
{
    public PostPortfolioTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Post_CreatesPortfolio_InUserBase_AndPersists()
    {
        var (userId, email) = await SeedUserAsync(Currency.USD);
        var client = AuthenticatedClient(userId, email);

        var response = await client.PostAsJsonAsync("/api/v1/portfolios", new { name = "Growth" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<PortfolioResponse>>();
        env!.Errors.Should().BeEmpty();
        var dto = env.Data!;
        dto.IdPortfolio.Should().BeGreaterThan(0);
        dto.Name.Should().Be("Growth");
        dto.RealizedPnL.Should().Be(0m);
        dto.RealizedPnLCurrency.Should().Be("USD"); // base del usuario, enum string

        await using var conn = new MySqlConnector.MySqlConnection(Fixture.ConnectionString);
        var row = await Dapper.SqlMapper.QuerySingleAsync(conn,
            @"SELECT IdUser, Name, RealizedPnL, RealizedPnLCurrency, IdStatus
              FROM Portfolios WHERE IdPortfolio = @id;", new { id = dto.IdPortfolio });
        ((int)row.IdUser).Should().Be(userId);
        ((string)row.Name).Should().Be("Growth");
        ((decimal)row.RealizedPnL).Should().Be(0m);
        ((string)row.RealizedPnLCurrency).Should().Be("USD");
        ((short)row.IdStatus).Should().Be((short)EntityStatus.Active);
    }

    [Fact]
    public async Task Post_WithoutToken_Returns401()
    {
        var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/portfolios", new { name = "X" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_EmptyName_Returns400_WithValidationError()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);

        var response = await client.PostAsJsonAsync("/api/v1/portfolios", new { name = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        env!.Errors.Should().Contain(e => e.Code == "VALIDATION_ERROR" && e.Field == "Name");
    }
}
