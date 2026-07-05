using System.Net.Http.Headers;
using System.Net.Http.Json;
using BigSchool.Domain.Auth.Entities;
using BigSchool.Domain.SharedKernel.Entities;
using BigSchool.Domain.SharedKernel.Enums;
using BigSchool.Infrastructure.SharedKernel.Persistence;
using BigSchool.Integration.Tests.Fixtures;
using Dapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MySqlConnector;
using BigSchool.Application.Auth.Interfaces.Services;

namespace BigSchool.Integration.Tests.Finance;

/// <summary>Helpers específicos de los endpoints de transacciones.</summary>
public abstract class TransactionEndpointTestBase : IntegrationTestBase
{
    protected TransactionEndpointTestBase(MySqlDatabaseFixture fixture) : base(fixture) { }

    // Siembra un usuario real (FK Transactions.IdUser) directamente en BD.
    protected async Task<(int Id, string Email)> SeedUserAsync(Currency baseCurrency = Currency.EUR)
    {
        var email = $"e2e-{Guid.NewGuid():N}@test.com";
        var options = new DbContextOptionsBuilder<BigSchoolDbContext>()
            .UseMySql(Fixture.ConnectionString, ServerVersion.AutoDetect(Fixture.ConnectionString))
            .Options;
        await using var ctx = new BigSchoolDbContext(options, Mock.Of<IMediator>());
        var user = User.Create(email, "hash", "salt", "E2E User", baseCurrency);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync(dispatchEvents: false);
        return (user.IdUser, email);
    }

    // Siembra una tasa para una fecha exacta → conversión determinista (sin red).
    protected async Task SeedExchangeRateAsync(Currency from, Currency to, decimal rate, DateOnly date)
    {
        await using var conn = new MySqlConnection(Fixture.ConnectionString);
        await conn.ExecuteAsync(
            @"INSERT INTO ExchangeRates (FromCurrency, ToCurrency, Rate, RateDate, Source, FetchedAt)
              VALUES (@From, @To, @Rate, @Date, 'test', @Now);",
            new { From = from.ToString(), To = to.ToString(), Rate = rate, Date = date.ToDateTime(TimeOnly.MinValue), Now = DateTime.UtcNow });
    }

    // Cliente con JWT REAL del IJwtService del factory (pasa por la validación JwtBearer real).
    protected HttpClient AuthenticatedClient(int userId, string email)
    {
        var jwt = Factory.Services.GetRequiredService<IJwtService>().GenerateToken(userId, email);
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt.AccessToken);
        return client;
    }

    // Crea una transacción vía API real y devuelve su id (black-box para sembrar datos en GET/PUT/DELETE).
    protected async Task<int> CreateTransactionViaApiAsync(HttpClient client, object body)
    {
        var resp = await client.PostAsJsonAsync("/api/v1/transactions", body);
        resp.EnsureSuccessStatusCode();
        var env = await resp.Content.ReadFromJsonAsync<ApiEnvelope<TransactionResponse>>();
        return env!.Data!.IdTransaction;
    }

    protected async Task<int> CountActiveTransactionsAsync(int userId)
    {
        await using var conn = new MySqlConnection(Fixture.ConnectionString);
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Transactions WHERE IdUser = @userId AND IdStatus <> @deleted;",
            new { userId, deleted = (short)EntityStatus.Deleted });
    }

    protected async Task<short?> ReadStatusAsync(int idTransaction)
    {
        await using var conn = new MySqlConnection(Fixture.ConnectionString);
        return await conn.QuerySingleOrDefaultAsync<short?>(
            "SELECT IdStatus FROM Transactions WHERE IdTransaction = @id;", new { id = idTransaction });
    }

    protected async Task<int> CountAsync(string sql)
    {
        await using var conn = new MySqlConnection(Fixture.ConnectionString);
        return await conn.ExecuteScalarAsync<int>(sql);
    }

    // TransactionDto: Type/IdMainCategory STRING (JsonStringEnumConverter); fechas "yyyy-MM-dd".
    protected record TransactionResponse(
        int IdTransaction, string Type, string IdMainCategory, int? IdSubCategory,
        string? Description, string TransactionDate, decimal OriginalAmount, string OriginalCurrency,
        decimal ExchangeRate, decimal BaseAmount, string BaseCurrency, string RateDate);

    // TransactionListItemDto: Type short, IdMainCategory int, monedas string, fechas "yyyy-MM-dd".
    protected record TransactionListItemResponse(
        int IdTransaction, short Type, int IdMainCategory, int? IdSubCategory,
        string? Description, string TransactionDate, decimal OriginalAmount, string OriginalCurrency,
        decimal ExchangeRate, decimal BaseAmount, string BaseCurrency, string RateDate);

    protected record SummaryPayload(decimal TotalIncome, decimal TotalExpense, decimal Balance, string BaseCurrency);
    protected record MonthlyChartPayload(int Year, int Month, decimal Income, decimal Expense);
}
