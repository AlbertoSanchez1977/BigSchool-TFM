using BigSchool.Integration.Tests.Fixtures;
using Dapper;
using FluentAssertions;
using MySqlConnector;
using Xunit;

namespace BigSchool.Integration.Tests.Persistence;

[Collection(IntegrationCollection.Name)]
public class ExchangeRateSeedTests
{
    private readonly MySqlDatabaseFixture _fixture;

    public ExchangeRateSeedTests(MySqlDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrations_ApplySeededExchangeRates()
    {
        // El fixture ya migró la BD una vez para toda la suite; aquí solo verificamos el seed.
        await using var conn = new MySqlConnection(_fixture.ConnectionString);
        var count = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM ExchangeRates WHERE Source = 'seed';");

        count.Should().BeGreaterThanOrEqualTo(4);
    }
}
