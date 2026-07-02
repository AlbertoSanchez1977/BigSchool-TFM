using System.Net.Http.Headers;
using System.Net.Http.Json;
using BigSchool.Domain.Auth.Entities;
using BigSchool.Domain.Investments.Enums;
using BigSchool.Domain.SharedKernel.Enums;
using BigSchool.Infrastructure.SharedKernel.Persistence;
using BigSchool.Integration.Tests.Fixtures;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using BigSchool.Application.Auth.Interfaces.Services;

namespace BigSchool.Integration.Tests.Investments;

/// <summary>Helpers específicos de los endpoints del catálogo de inversiones (Companies/Valuations).</summary>
public abstract class CompanyEndpointTestBase : IntegrationTestBase
{
    protected CompanyEndpointTestBase(MySqlDatabaseFixture fixture) : base(fixture) { }

    /// <summary>Empresas sembradas por la migración (IdCompany 1-4) que ResetAsync preserva.</summary>
    protected const int SeededCompaniesCount = 4;

    // Siembra un usuario (los endpoints son [Authorize] aunque el catálogo sea global).
    protected async Task<(int Id, string Email)> SeedUserAsync(Currency baseCurrency = Currency.EUR)
    {
        var email = $"inv-{Guid.NewGuid():N}@test.com";
        var options = new DbContextOptionsBuilder<BigSchoolDbContext>()
            .UseMySql(Fixture.ConnectionString, ServerVersion.AutoDetect(Fixture.ConnectionString))
            .Options;
        await using var ctx = new BigSchoolDbContext(options, Mock.Of<IMediator>());
        var user = User.Create(email, "hash", "salt", "Investor", baseCurrency);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync(dispatchEvents: false);
        return (user.IdUser, email);
    }

    protected HttpClient AuthenticatedClient(int userId, string email)
    {
        var jwt = Factory.Services.GetRequiredService<IJwtService>().GenerateToken(userId, email);
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt.AccessToken);
        return client;
    }

    // Crea una empresa vía API real y devuelve su id (para sembrar datos en GET/valuations).
    protected async Task<int> CreateCompanyViaApiAsync(HttpClient client, object body)
    {
        var resp = await client.PostAsJsonAsync("/api/v1/companies", body);
        resp.EnsureSuccessStatusCode();
        var env = await resp.Content.ReadFromJsonAsync<ApiEnvelope<CompanyResponse>>();
        return env!.Data!.IdCompany;
    }

    // CompanyDto (command): Currency string (JsonStringEnumConverter serializa el enum como "USD").
    protected record CompanyResponse(int IdCompany, string Name, string Ticker, string? Sector, string? Market, string Currency);

    // CompanyListItemDto (query): monedas string, fechas "yyyy-MM-dd", última cotización opcional.
    protected record CompanyListItemResponse(
        int IdCompany, string Name, string Ticker, string? Sector, string? Market,
        string Currency, decimal? LastPrice, string? LastValuationDate);

    // ValuationDto (command): Currency string, Date "yyyy-MM-dd".
    protected record ValuationResponse(int IdValuation, int IdCompany, decimal Price, string Currency, string Date, string? Source);

    // ValuationListItemDto (query): PriceCurrency string, Date "yyyy-MM-dd".
    protected record ValuationListItemResponse(int IdValuation, int IdCompany, decimal Price, string PriceCurrency, string Date, string? Source);
}
