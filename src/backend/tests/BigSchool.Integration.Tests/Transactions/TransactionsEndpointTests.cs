using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BigSchool.Application.Interfaces.Services;
using BigSchool.Domain.Entities;
using BigSchool.Domain.Enums;
using BigSchool.Infrastructure.Persistence;
using BigSchool.Integration.Tests.Fixtures;
using Dapper;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MySqlConnector;
using Xunit;

namespace BigSchool.Integration.Tests.Transactions;

[Collection(IntegrationCollection.Name)]
public class TransactionsEndpointTests : IAsyncLifetime
{
    private readonly MySqlDatabaseFixture _fixture;
    private BigSchoolWebAppFactory _factory = null!;

    public TransactionsEndpointTests(MySqlDatabaseFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _factory = new BigSchoolWebAppFactory(_fixture.ConnectionString);
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    // Siembra un usuario real en bigschool_test (necesario por la FK Transactions.IdUser).
    private async Task<(int Id, string Email)> SeedUserAsync(Currency baseCurrency = Currency.EUR)
    {
        var email = $"e2e-{Guid.NewGuid():N}@test.com";
        var options = new DbContextOptionsBuilder<BigSchoolDbContext>()
            .UseMySql(_fixture.ConnectionString, ServerVersion.AutoDetect(_fixture.ConnectionString))
            .Options;
        await using var ctx = new BigSchoolDbContext(options, Mock.Of<IMediator>());
        var user = User.Create(email, "hash", "salt", "E2E User", baseCurrency);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync(dispatchEvents: false);
        return (user.IdUser, email);
    }

    // Cliente con JWT REAL acuñado por IJwtService del factory → pasa por la validación JwtBearer real.
    private HttpClient AuthenticatedClient(int userId, string email)
    {
        var jwt = _factory.Services.GetRequiredService<IJwtService>().GenerateToken(userId, email);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt.AccessToken);
        return client;
    }

    [Fact]
    public async Task Post_ThenSummary_HappyPath_ConsolidatesInBase_AndPersists()
    {
        var (userId, email) = await SeedUserAsync(Currency.EUR);
        var client = AuthenticatedClient(userId, email);

        // Crea un ingreso (currency omitido → default a EUR, rate 1, sin red).
        var create = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            type = (int)TransactionType.Income,
            idMainCategory = (int)MainCategory.Salary,
            transactionDate = "2026-06-10",
            amount = 1500m
        });
        create.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verifica el summary por el endpoint real.
        var summary = await client.GetFromJsonAsync<ApiEnvelope<SummaryPayload>>("/api/v1/transactions/summary");
        summary!.Data!.TotalIncome.Should().Be(1500m);
        summary.Data.BaseCurrency.Should().Be("EUR");

        // Verificación FÍSICA en MySQL: la fila quedó persistida y no está eliminada.
        await using var conn = new MySqlConnection(_fixture.ConnectionString);
        var rows = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Transactions WHERE IdUser = @userId AND IdStatus <> @statusDeleted;",
            new { userId, statusDeleted = (short)EntityStatus.Deleted });
        rows.Should().Be(1);
    }

    [Fact]
    public async Task Get_Summary_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/transactions/summary");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // Tipos mínimos para deserializar la envoltura ApiResponse<T> (System.Text.Json en camelCase).
    private record ApiEnvelope<T>(T? Data);
    private record SummaryPayload(decimal TotalIncome, decimal TotalExpense, decimal Balance, string BaseCurrency);
}
