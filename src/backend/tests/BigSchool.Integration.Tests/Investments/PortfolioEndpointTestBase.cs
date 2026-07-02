using System.Net.Http.Headers;
using System.Net.Http.Json;
using BigSchool.Application.Interfaces.Services;
using BigSchool.Domain.Auth.Entities;
using BigSchool.Domain.SharedKernel.Enums;
using BigSchool.Infrastructure.Persistence;
using BigSchool.Integration.Tests.Fixtures;
using Dapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MySqlConnector;

namespace BigSchool.Integration.Tests.Investments;

/// <summary>Helpers específicos de los endpoints de carteras. Usa el catálogo seedeado del Plan 3A.</summary>
public abstract class PortfolioEndpointTestBase : IntegrationTestBase
{
    // Catálogo seedeado (IDs fijos).
    protected const int CompanyAaplUsd = 1;
    protected const int CompanyMsftUsd = 2;
    protected const int CompanySanEur = 3;
    protected const int CompanyShelGbp = 4;

    protected PortfolioEndpointTestBase(MySqlDatabaseFixture fixture) : base(fixture) { }

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

    protected async Task SeedExchangeRateAsync(Currency from, Currency to, decimal rate, DateOnly date)
    {
        await using var conn = new MySqlConnection(Fixture.ConnectionString);
        await conn.ExecuteAsync(
            @"INSERT INTO ExchangeRates (FromCurrency, ToCurrency, Rate, RateDate, Source, FetchedAt)
              VALUES (@From, @To, @Rate, @Date, 'test', @Now);",
            new { From = from.ToString(), To = to.ToString(), Rate = rate, Date = date.ToDateTime(TimeOnly.MinValue), Now = DateTime.UtcNow });
    }

    protected HttpClient AuthenticatedClient(int userId, string email)
    {
        var jwt = Factory.Services.GetRequiredService<IJwtService>().GenerateToken(userId, email);
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt.AccessToken);
        return client;
    }

    protected async Task<int> CreatePortfolioViaApiAsync(HttpClient client, string name = "Cartera")
    {
        var resp = await client.PostAsJsonAsync("/api/v1/portfolios", new { name });
        resp.EnsureSuccessStatusCode();
        var env = await resp.Content.ReadFromJsonAsync<ApiEnvelope<PortfolioResponse>>();
        return env!.Data!.IdPortfolio;
    }

    protected async Task<int> AddHoldingViaApiAsync(HttpClient client, int portfolioId,
        int idCompany, decimal shares, decimal buyPrice, string buyDate, string? notes = null)
    {
        var resp = await client.PostAsJsonAsync($"/api/v1/portfolios/{portfolioId}/holdings",
            new { idCompany, shares, buyPrice, buyDate, notes });
        resp.EnsureSuccessStatusCode();
        var env = await resp.Content.ReadFromJsonAsync<ApiEnvelope<HoldingResponse>>();
        return env!.Data!.IdHolding;
    }

    protected async Task<decimal> ReadPortfolioRealizedPnLAsync(int idPortfolio)
    {
        await using var conn = new MySqlConnection(Fixture.ConnectionString);
        return await conn.ExecuteScalarAsync<decimal>(
            "SELECT RealizedPnL FROM Portfolios WHERE IdPortfolio = @id;", new { id = idPortfolio });
    }

    protected async Task<short?> ReadHoldingStatusAsync(int idHolding)
    {
        await using var conn = new MySqlConnection(Fixture.ConnectionString);
        return await conn.QuerySingleOrDefaultAsync<short?>(
            "SELECT IdStatus FROM Holdings WHERE IdHolding = @id;", new { id = idHolding });
    }

    protected async Task<int> CountActiveDisposalsAsync(int idHolding)
    {
        await using var conn = new MySqlConnection(Fixture.ConnectionString);
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Disposals WHERE IdHolding = @id AND IdStatus <> @deleted;",
            new { id = idHolding, deleted = (short)EntityStatus.Deleted });
    }

    // ---- Tipos de deserialización (command DTOs: enums string vía JsonStringEnumConverter; fechas "yyyy-MM-dd") ----
    protected record PortfolioResponse(int IdPortfolio, string Name, decimal RealizedPnL, string RealizedPnLCurrency);

    protected record HoldingResponse(
        int IdHolding, int IdCompany, decimal Shares,
        decimal BuyOriginalAmount, string BuyOriginalCurrency, decimal BuyExchangeRate,
        decimal BuyBaseAmount, string BuyBaseCurrency, string BuyRateDate, string BuyDate, string? Notes);

    protected record DisposalResponse(
        int IdDisposal, int IdHolding, decimal Shares,
        decimal SellOriginalAmount, string SellOriginalCurrency, decimal SellExchangeRate,
        decimal SellBaseAmount, string SellBaseCurrency, string SellRateDate, string SellDate,
        decimal RealizedPnL, string RealizedPnLCurrency, string? Notes);

    protected record SellResultResponse(
        List<DisposalResponse> Disposals, decimal PortfolioRealizedPnL, string RealizedPnLCurrency);

    // ---- Query DTOs (monedas string ya nativas; fechas "yyyy-MM-dd") ----
    protected record PortfolioListItemResponse(
        int IdPortfolio, string Name, decimal RealizedPnL, string RealizedPnLCurrency,
        decimal MarketValue, decimal CostBasis, decimal UnrealizedPnL, decimal TotalPnL);

    protected record HoldingListItemResponse(
        int IdHolding, int IdCompany, string Ticker, string CompanyCurrency,
        decimal Shares, decimal OpenShares,
        decimal BuyOriginalAmount, string BuyOriginalCurrency, decimal BuyExchangeRate,
        decimal BuyBaseAmount, string BuyBaseCurrency, string BuyRateDate, string BuyDate, string? Notes,
        decimal MarketValue, decimal CostBasis, decimal UnrealizedPnL);

    protected record PortfolioDetailResponse(
        int IdPortfolio, string Name, decimal RealizedPnL, string RealizedPnLCurrency,
        List<HoldingListItemResponse> Holdings);

    protected record HoldingPerformanceResponse(
        int IdHolding, int IdCompany, string Ticker, decimal OpenShares,
        decimal CostBasis, decimal MarketValue, decimal UnrealizedPnL, decimal UnrealizedPnLPct);

    protected record PerformanceResponse(
        int IdPortfolio, string Name, string BaseCurrency,
        decimal MarketValue, decimal CostBasis, decimal UnrealizedPnL,
        decimal RealizedPnL, decimal TotalPnL, decimal ReturnPct,
        List<HoldingPerformanceResponse> Holdings);
}
