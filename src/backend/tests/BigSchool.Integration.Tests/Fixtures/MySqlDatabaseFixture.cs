using BigSchool.Infrastructure.Persistence;
using Dapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using MySqlConnector;
using Xunit;

namespace BigSchool.Integration.Tests.Fixtures;

/// <summary>
/// Fixture compartido por TODA la suite de integración: un único ciclo crear/migrar/destruir por ejecución.
/// Reusa el servidor MySQL de docker-compose (localhost:3306) sobre una BD dedicada 'bigschool_test'
/// que EF migra desde cero (no usamos 'bigschool' porque init.sql ya creó esas tablas y chocaría).
/// </summary>
public sealed class MySqlDatabaseFixture : IAsyncLifetime
{
    private const string TestDatabase = "bigschool_test";

    // Conexión al SERVIDOR (sin BD concreta) para CREATE/DROP DATABASE. Configurable por env var
    // para alinear credenciales con el .env de compose sin hardcodearlas en el repo.
    private static string ServerConnectionString =>
        Environment.GetEnvironmentVariable("BIGSCHOOL_TEST_MYSQL")
        ?? "Server=localhost;Port=3306;User ID=root;Password=bigschool_root;";

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        // (Re)crear la BD de test desde cero — idempotente aunque un run previo se cortara sin DisposeAsync.
        await using (var admin = new MySqlConnection(ServerConnectionString))
        {
            await admin.OpenAsync();
            await admin.ExecuteAsync($"DROP DATABASE IF EXISTS `{TestDatabase}`;");
            await admin.ExecuteAsync($"CREATE DATABASE `{TestDatabase}`;");
        }

        ConnectionString = $"{ServerConnectionString.TrimEnd(';')};Database={TestDatabase};";

        // Aplicar TODAS las migraciones una sola vez para toda la suite.
        var options = new DbContextOptionsBuilder<BigSchoolDbContext>()
            .UseMySql(ConnectionString, ServerVersion.AutoDetect(ConnectionString))
            .Options;
        await using var ctx = new BigSchoolDbContext(options, Mock.Of<IMediator>());
        await ctx.Database.MigrateAsync();
    }

    // xUnit ejecuta esto UNA vez tras el último test de la colección (corras 1, 2 o todos).
    public async Task DisposeAsync()
    {
        await using var admin = new MySqlConnection(ServerConnectionString);
        await admin.OpenAsync();
        await admin.ExecuteAsync($"DROP DATABASE IF EXISTS `{TestDatabase}`;");
    }

    /// <summary>Reset entre tests: conserva esquema + seed global (SubCategories globales, ExchangeRates 'seed'), limpia datos mutables.</summary>
    public async Task ResetAsync()
    {
        await using var conn = new MySqlConnection(ConnectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync("SET FOREIGN_KEY_CHECKS = 0;");
        await conn.ExecuteAsync("DELETE FROM Disposals;");
        await conn.ExecuteAsync("DELETE FROM Holdings;");
        await conn.ExecuteAsync("DELETE FROM Portfolios;");
        await conn.ExecuteAsync("TRUNCATE TABLE Transactions;");
        await conn.ExecuteAsync("DELETE FROM SubCategories WHERE IdUser IS NOT NULL;");
        await conn.ExecuteAsync("DELETE FROM Users;");
        await conn.ExecuteAsync("DELETE FROM ExchangeRates WHERE Source <> 'seed';");
        // Conservar el catálogo seedeado (Companies 1-4, Valuations 1-8); limpiar lo creado por tests.
        await conn.ExecuteAsync("DELETE FROM Valuations WHERE IdValuation > 8;");
        await conn.ExecuteAsync("DELETE FROM Companies WHERE IdCompany > 4;");
        await conn.ExecuteAsync("SET FOREIGN_KEY_CHECKS = 1;");
    }
}
