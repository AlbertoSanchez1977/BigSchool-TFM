# Backend — Inversiones: valuations por periodo, holdings Original, portfolio rename/delete + summary Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cerrar cuatro huecos de Investments: (#10) campos `*Original` (moneda de la empresa) en `HoldingPerformanceDto`; (#9) `GET /companies/{id}/valuations/series?period=` (serie de precio + summary, anclada a la última valoración); (#11/D) `PUT`/`DELETE /portfolios/{id}` (rename siempre; delete con guards de posiciones abiertas y gracia fiscal de 5 años); (#11/E) `GET /portfolios/summary` (agregado de todas las carteras del usuario en moneda base).

**Architecture:** Se reutiliza `PortfolioSqlFragments.HOLDING_VALUATION` (ya expone `OpenShares`, `BuyOriginalAmount`, `BuyOriginalCurrency`, `LastPrice`, `CompanyCurrency`): #10 y #11/E solo **exponen/agregan** columnas sin tocar el cálculo base. Rename/Delete son métodos de dominio en `Portfolio` (delete recibe `today` → dominio puro); las reglas de borrado lanzan **excepciones de dominio nuevas → 409**. La serie #9 es Dapper puro sobre `Valuations` (catálogo global, moneda de la empresa, sin conversión) con el periodo modelado como **enum** (`ValuationPeriod`, valor = nº de meses). Series/summary **no** se paginan.

**Tech Stack:** .NET 8, C#, DDD + CQRS (MediatR), EF Core + MySQL 8, Dapper, FluentValidation, xUnit + FluentAssertions + Moq + `WebApplicationFactory`.

---

## Convenciones y contexto (LEER ANTES DE EMPEZAR)

> **BASE:** develop con estructura modular (018) + paginación (019). Rama `feature/007-010-backend-features`; cada tarea = rama `feature/023-inversiones-taskN` + PR.

Estado verificado en develop:
- `PortfolioSqlFragments.HOLDING_VALUATION` (`internal static`, `BigSchool.Application.Investments.Queries`): el SELECT externo `x` ya lista `x.OpenShares`, `x.BuyOriginalAmount`, `x.BuyOriginalCurrency`, `x.LastPrice`, `x.CompanyCurrency`, y calcula `MarketValue`/`CostBasis`/`UnrealizedPnL` (en base). Params: `@StatusDeleted`, `@BaseCurrency`.
- `GetPortfolioPerformanceQueryHandler` usa `IUserBaseCurrencyProvider.GetBaseCurrencyAsync(idUser, ct)` (`BigSchool.Application.SharedKernel.Interfaces.Services`, devuelve `Currency?`) + `OPEN_HOLDINGS_QUERY` (SELECT sobre `HOLDING_VALUATION`, `WHERE hv.OpenShares > 0`) + `HoldingRow` privado → `HoldingPerformanceDto`.
- `HoldingPerformanceDto(int IdHolding, int IdCompany, string Ticker, decimal OpenShares, decimal CostBasis, decimal MarketValue, decimal UnrealizedPnL, decimal UnrealizedPnLPct)` — `BigSchool.Application.Investments.DTOs`.
- `Portfolio` (AR): `Name`, `RealizedPnL` (Money), `IdUser`, `IdStatus`, `UpdatedAt`, `_holdings`/`Holdings`. `Holding` expone `OpenShares`, `IdStatus`, `Disposals` (`Disposal` tiene `SellDate`, `IdStatus`). Enums de Investments en `BigSchool.Domain.Investments.Enums` (Market, Sector).
- `IPortfolioRepository` ya tiene `GetByIdWithHoldingsAsync(id, ct)` (Include `Holdings.ThenInclude(Disposals)`, query-filtered a activas) + `GetByIdAsync`/`AddAsync`/`UnitOfWork`.
- Excepciones de dominio: patrón `: DomainException` con `base("CODE", "mensaje")` (ver `InsufficientSharesDomainException`, code `INSUFFICIENT_SHARES`). El `ExceptionHandlingMiddleware` mapea por excepción a HTTP (InsufficientShares→400, EmailAlreadyExists→409).
- `PortfoliosController` (`/api/v1/portfolios`, `[Authorize]`, `IMediator`+`IUserIdEncryptor`, `UserId`) tiene GET (paginado), POST, GET`{id}`, GET`{id}/performance`, holdings, sales. `CompaniesController` (`/api/v1/companies`, `[Authorize]`) tiene GET/GET`{id}`/POST/GET`{id}/valuations`/POST`{id}/valuations`.
- `IDbConnectionFactory`, `EntityStatus`, `NotFoundException`, `Currency` — ubicaciones de specs previas. Dapper lee `DATE`→`DateOnly` vía `DateOnlyTypeHandler` (registrado). Bases de test `PortfolioEndpointTestBase` (`PerformanceResponse`/`HoldingPerformanceResponse`, `CreatePortfolioViaApiAsync`, `AddHoldingViaApiAsync`, `SeedExchangeRateAsync`, `CompanyAaplUsd=1`…) y `CompanyEndpointTestBase` (`CreateCompanyViaApiAsync`).

Recetas: **BUILD** (`dotnet build`, cwd `src/backend`); **UNIT** (`dotnet test tests/BigSchool.Domain.Tests`, `.Application.Tests`); **INTEGRATION** (MySQL `infra/docker-compose.yml`); **FULL** = todo.

---

## Task 1: #10 — Holdings `*Original` en performance

Aditivo: exponer en `HOLDING_VALUATION` los valores en moneda de la empresa (sin `Rate`) y propagarlos por `OPEN_HOLDINGS_QUERY` → `HoldingPerformanceDto`. No afecta a `GetPortfolios` (solo suma columnas base).

**Files:**
- Modify: `src/BigSchool.Application/Investments/Queries/PortfolioSqlFragments.cs`
- Modify: `src/BigSchool.Application/Investments/DTOs/HoldingPerformanceDto.cs`
- Modify: `src/BigSchool.Application/Investments/Queries/GetPortfolioPerformance/GetPortfolioPerformanceQueryHandler.cs`
- Modify: `tests/BigSchool.Integration.Tests/Investments/PortfolioEndpointTestBase.cs` (`HoldingPerformanceResponse`)
- Test: `tests/BigSchool.Integration.Tests/Investments/GetPerformanceOriginalTests.cs`

- [ ] **Step 1: Añadir columnas `*Original` al fragmento**

En `PortfolioSqlFragments.HOLDING_VALUATION`, en el SELECT externo (el que empieza `SELECT x.IdHolding, ...`), añade tras `UnrealizedPnL`:
```sql
    ,
    x.BuyOriginalCurrency                                                   AS BuyOriginalCurrency,
    ROUND(x.OpenShares * x.BuyOriginalAmount, 2)                            AS CostBasisOriginal,
    ROUND(x.OpenShares * COALESCE(x.LastPrice, 0), 2)                       AS MarketValueOriginal,
    ROUND(x.OpenShares * COALESCE(x.LastPrice, 0) - x.OpenShares * x.BuyOriginalAmount, 2) AS UnrealizedPnLOriginal
```
> `x.BuyOriginalCurrency` ya viaja en `x`; el alias explícito la deja disponible como columna del resultado. `GetPortfolios` selecciona por nombre (`SUM(hv.MarketValue/...)`) → ignora las nuevas.

- [ ] **Step 2: Extender el DTO**

`HoldingPerformanceDto.cs`:
```csharp
namespace BigSchool.Application.Investments.DTOs;

public record HoldingPerformanceDto(
    int IdHolding, int IdCompany, string Ticker,
    decimal OpenShares, decimal CostBasis, decimal MarketValue,
    decimal UnrealizedPnL, decimal UnrealizedPnLPct,
    string BuyOriginalCurrency, decimal CostBasisOriginal,
    decimal MarketValueOriginal, decimal UnrealizedPnLOriginal);
```

- [ ] **Step 3: Propagar en el handler**

En `GetPortfolioPerformanceQueryHandler`:
- Amplía `OPEN_HOLDINGS_QUERY` para seleccionar las 4 nuevas columnas:
```csharp
    private const string OPEN_HOLDINGS_QUERY = @"
SELECT hv.IdHolding, hv.IdCompany, hv.Ticker, hv.OpenShares,
       hv.CostBasis, hv.MarketValue, hv.UnrealizedPnL,
       hv.BuyOriginalCurrency, hv.CostBasisOriginal, hv.MarketValueOriginal, hv.UnrealizedPnLOriginal
FROM (" + PortfolioSqlFragments.HOLDING_VALUATION + @") hv
WHERE hv.IdPortfolio = @IdPortfolio AND hv.OpenShares > 0
ORDER BY hv.BuyDate, hv.IdHolding;";
```
- Amplía `HoldingRow` y la proyección:
```csharp
    private sealed record HoldingRow(int IdHolding, int IdCompany, string Ticker, decimal OpenShares,
        decimal CostBasis, decimal MarketValue, decimal UnrealizedPnL,
        string BuyOriginalCurrency, decimal CostBasisOriginal, decimal MarketValueOriginal, decimal UnrealizedPnLOriginal);
```
```csharp
        var holdings = rows.Select(r => new HoldingPerformanceDto(
            r.IdHolding, r.IdCompany, r.Ticker, r.OpenShares, r.CostBasis, r.MarketValue, r.UnrealizedPnL,
            r.CostBasis > 0m ? Math.Round(r.UnrealizedPnL / r.CostBasis * 100m, 2) : 0m,
            r.BuyOriginalCurrency, r.CostBasisOriginal, r.MarketValueOriginal, r.UnrealizedPnLOriginal)).ToList();
```
> Los totales de cartera (`marketValue`/`costBasis`/`unrealized`/`returnPct`) **no cambian** (siguen sumando los campos base).

- [ ] **Step 4: E2E**

En `PortfolioEndpointTestBase`, añade los 4 campos a `HoldingPerformanceResponse`:
```csharp
    protected record HoldingPerformanceResponse(
        int IdHolding, int IdCompany, string Ticker, decimal OpenShares,
        decimal CostBasis, decimal MarketValue, decimal UnrealizedPnL, decimal UnrealizedPnLPct,
        string BuyOriginalCurrency, decimal CostBasisOriginal, decimal MarketValueOriginal, decimal UnrealizedPnLOriginal);
```
`GetPerformanceOriginalTests.cs` (holding USD, moneda empresa; verifica `*Original` sin `Rate`):
```csharp
    [Fact]
    public async Task Performance_TraeCamposOriginal_EnMonedaEmpresa()
    {
        var (userId, email) = await SeedUserAsync(Currency.EUR);
        var client = AuthenticatedClient(userId, email);
        var portfolioId = await CreatePortfolioViaApiAsync(client, "Tech");
        await SeedExchangeRateAsync(Currency.USD, Currency.EUR, 0.90m, new DateOnly(2026, 1, 5));
        await SeedExchangeRateAsync(Currency.USD, Currency.EUR, 1.00m, new DateOnly(2026, 3, 2)); // última valoración AAPL
        await AddHoldingViaApiAsync(client, portfolioId, CompanyAaplUsd, 10m, 195m, "2026-01-05"); // AAPL USD

        var perf = (await (await client.GetAsync($"/api/v1/portfolios/{portfolioId}/performance"))
            .Content.ReadFromJsonAsync<ApiEnvelope<PerformanceResponse>>())!.Data!;
        var h = perf.Holdings.Single();

        h.BuyOriginalCurrency.Should().Be("USD");
        // CostBasisOriginal = OpenShares(10) × BuyOriginal(195.50) = 1955 (sin Rate)
        h.CostBasisOriginal.Should().Be(1955m);
        // MarketValueOriginal = 10 × LastPrice(210 USD) = 2100
        h.MarketValueOriginal.Should().Be(2100m);
        h.UnrealizedPnLOriginal.Should().Be(145m);
        // Los totales base NO cambian (sanity)
        perf.MarketValue.Should().Be(2100m); // rate 1.00 en la última valoración
    }
```
> Ajusta las cifras al seed real de AAPL (última valoración/última fecha). El objetivo es que `*Original` use **precio en USD sin `Rate`** y que los totales base sigan igual.

Run: `dotnet test tests/BigSchool.Integration.Tests --filter "GetPerformanceOriginal|GetPerformance|GetPortfolios"` → PASS (los E2E de performance/portfolios existentes siguen verdes).

- [ ] **Step 5: Verde + Commit**
```bash
git add -A && git commit -m "feat(investments): campos *Original en HoldingPerformanceDto (moneda de la empresa)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 2: #9 — `GET /companies/{id}/valuations/series?period=` (Dapper)

Serie de precio de la empresa (moneda de la empresa) en una ventana anclada a la **última** valoración, + summary. El periodo es un **enum** (`ValuationPeriod`) cuyo valor subyacente **es el nº de meses** — sin magic values y compartible con el frontend (como `Currency`).

**Files:**
- Create: `src/BigSchool.Domain/Investments/Enums/ValuationPeriod.cs`
- Create: `src/BigSchool.Application/Investments/DTOs/ValuationSeriesDto.cs`
- Create: `src/BigSchool.Application/Investments/Queries/GetCompanyValuationSeries/{GetCompanyValuationSeriesQuery,GetCompanyValuationSeriesQueryHandler,GetCompanyValuationSeriesQueryValidator}.cs`
- Modify: `src/BigSchool.WebApi/Controllers/Investments/CompaniesController.cs`
- Test: `tests/BigSchool.Application.Tests/Investments/ValuationSeriesPeriodValidatorTests.cs`
- Test: `tests/BigSchool.Integration.Tests/Investments/GetValuationSeriesTests.cs`

- [ ] **Step 1: Enum `ValuationPeriod` (valor = nº de meses) + DTOs**

`ValuationPeriod.cs`:
```csharp
namespace BigSchool.Domain.Investments.Enums;

/// <summary>Ventana de la serie de cotización. El valor subyacente ES el nº de meses (evita magic values; compartido con el frontend).</summary>
public enum ValuationPeriod
{
    ThreeMonths = 3,
    SixMonths = 6,
    OneYear = 12,
    ThreeYears = 36,
    FiveYears = 60
}
```
`ValuationSeriesDto.cs`:
```csharp
namespace BigSchool.Application.Investments.DTOs;

public record ValuationPointDto(DateOnly Date, decimal Price);

public record ValuationSeriesSummaryDto(decimal First, decimal Last, decimal Min, decimal Max, decimal ChangePct);

public record ValuationSeriesDto(string Currency, IReadOnlyList<ValuationPointDto> Points, ValuationSeriesSummaryDto Summary);
```

- [ ] **Step 2: Query + Validator (`IsInEnum`)**

`GetCompanyValuationSeriesQuery.cs`:
```csharp
using BigSchool.Application.Investments.DTOs;
using BigSchool.Domain.Investments.Enums;
using MediatR;

namespace BigSchool.Application.Investments.Queries.GetCompanyValuationSeries;

public record GetCompanyValuationSeriesQuery(int IdCompany, ValuationPeriod Period) : IRequest<ValuationSeriesDto>;
```
`GetCompanyValuationSeriesQueryValidator.cs`:
```csharp
using FluentValidation;

namespace BigSchool.Application.Investments.Queries.GetCompanyValuationSeries;

public class GetCompanyValuationSeriesQueryValidator : AbstractValidator<GetCompanyValuationSeriesQuery>
{
    public GetCompanyValuationSeriesQueryValidator()
        => RuleFor(x => x.Period).IsInEnum()
            .WithMessage("El periodo debe ser uno de: ThreeMonths, SixMonths, OneYear, ThreeYears, FiveYears.");
}
```

- [ ] **Step 3: Handler (`(int)Period` = meses; `First()`/`Last()`)**

`GetCompanyValuationSeriesQueryHandler.cs`:
```csharp
using BigSchool.Application.Investments.DTOs;
using BigSchool.Application.SharedKernel.Interfaces;
using BigSchool.Domain.SharedKernel.Enums;
using Dapper;
using MediatR;

namespace BigSchool.Application.Investments.Queries.GetCompanyValuationSeries;

public class GetCompanyValuationSeriesQueryHandler : IRequestHandler<GetCompanyValuationSeriesQuery, ValuationSeriesDto>
{
    private readonly IDbConnectionFactory _dbFactory;

    public GetCompanyValuationSeriesQueryHandler(IDbConnectionFactory dbFactory) => _dbFactory = dbFactory;

    private const string LASTDATE_QUERY = @"SELECT Date FROM Valuations
        WHERE IdCompany = @IdCompany AND IdStatus <> @StatusDeleted
        ORDER BY Date DESC, IdValuation DESC LIMIT 1;";

    private const string SERIES_QUERY = @"SELECT Date, Price, PriceCurrency
        FROM Valuations
        WHERE IdCompany = @IdCompany AND IdStatus <> @StatusDeleted
          AND Date >= @From AND Date <= @Last
        ORDER BY Date ASC, IdValuation ASC;";

    private sealed record Row(DateOnly Date, decimal Price, string PriceCurrency);

    public async Task<ValuationSeriesDto> Handle(GetCompanyValuationSeriesQuery request, CancellationToken cancellationToken)
    {
        var empty = new ValuationSeriesDto("", new List<ValuationPointDto>(), new ValuationSeriesSummaryDto(0, 0, 0, 0, 0));

        var p = new DynamicParameters();
        p.Add("@IdCompany", request.IdCompany);
        p.Add("@StatusDeleted", EntityStatus.Deleted);

        using var conn = _dbFactory.CreateConnection();
        var lastDate = await conn.QuerySingleOrDefaultAsync<DateOnly?>(LASTDATE_QUERY, p);
        if (lastDate is null) return empty; // empresa sin valoraciones → serie vacía

        var months = (int)request.Period; // el valor del enum ES el nº de meses
        p.Add("@Last", lastDate.Value);
        p.Add("@From", lastDate.Value.AddMonths(-months));

        var rows = (await conn.QueryAsync<Row>(SERIES_QUERY, p)).ToList();
        if (rows.Count == 0) return empty;

        var prices = rows.Select(r => r.Price).ToList();
        var first = prices.First();
        var last = prices.Last();
        var changePct = first == 0m ? 0m : Math.Round((last - first) / first * 100m, 2);

        var points = rows.Select(r => new ValuationPointDto(r.Date, r.Price)).ToList();
        var summary = new ValuationSeriesSummaryDto(first, last, prices.Min(), prices.Max(), changePct);
        return new ValuationSeriesDto(rows.First().PriceCurrency, points, summary);
    }
}
```

- [ ] **Step 4: Endpoint en `CompaniesController`**

Con `using BigSchool.Domain.Investments.Enums;` (+ usings de `GetCompanyValuationSeries`/`ValuationSeriesDto`):
```csharp
    [HttpGet("{id:int}/valuations/series")]
    [ProducesResponseType(typeof(ApiResponse<ValuationSeriesDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ValuationsSeries(int id, [FromQuery] ValuationPeriod period = ValuationPeriod.OneYear)
    {
        var result = await _mediator.Send(new GetCompanyValuationSeriesQuery(id, period));
        return Ok(ApiResponse<ValuationSeriesDto>.Success(result));
    }
```
> El binding de query enlaza por nombre (`?period=OneYear`) o valor (`?period=12`). Un nombre desconocido (`?period=2y`) falla el binding → 400 automático ([ApiController]); un entero fuera de rango (`?period=99`) enlaza pero lo rechaza `IsInEnum` → 400.

- [ ] **Step 5: Unit test del validator**

`ValuationSeriesPeriodValidatorTests.cs`:
```csharp
using BigSchool.Application.Investments.Queries.GetCompanyValuationSeries;
using BigSchool.Domain.Investments.Enums;
using FluentAssertions;
using Xunit;

namespace BigSchool.Application.Tests.Investments;

public class ValuationSeriesPeriodValidatorTests
{
    private readonly GetCompanyValuationSeriesQueryValidator _v = new();

    [Theory]
    [InlineData(ValuationPeriod.ThreeMonths)]
    [InlineData(ValuationPeriod.OneYear)]
    [InlineData(ValuationPeriod.FiveYears)]
    public void Periodos_validos_pasan(ValuationPeriod p)
        => _v.Validate(new GetCompanyValuationSeriesQuery(1, p)).IsValid.Should().BeTrue();

    [Fact]
    public void Periodo_fuera_de_enum_falla()
        => _v.Validate(new GetCompanyValuationSeriesQuery(1, (ValuationPeriod)99)).IsValid.Should().BeFalse();
}
```

- [ ] **Step 6: E2E**

`GetValuationSeriesTests.cs`:
```csharp
    [Fact]
    public async Task Series_PeriodoInvalido_400()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        (await client.GetAsync("/api/v1/companies/1/valuations/series?period=99")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Series_OneYear_PuntosEnVentana_Ordenados_SummaryCoherente()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        var ticker = $"S{Guid.NewGuid():N}".Substring(0, 8).ToUpperInvariant();
        var id = await CreateCompanyViaApiAsync(client, new { name = "Serie Co.", ticker, currency = "USD" });
        await client.PostAsJsonAsync($"/api/v1/companies/{id}/valuations", new { price = 100m, date = "2025-06-10", source = (string?)null });
        await client.PostAsJsonAsync($"/api/v1/companies/{id}/valuations", new { price = 150m, date = "2026-03-10", source = (string?)null });

        var s = (await (await client.GetAsync($"/api/v1/companies/{id}/valuations/series?period=OneYear"))
            .Content.ReadFromJsonAsync<ApiEnvelope<ValuationSeriesResponse>>())!.Data!;
        s.Currency.Should().Be("USD");
        s.Points.Should().HaveCount(2);
        s.Points[0].Date.Should().Be("2025-06-10"); // ventana: [2026-03-10 - 12m, 2026-03-10]
        s.Summary.First.Should().Be(100m);
        s.Summary.Last.Should().Be(150m);
        s.Summary.ChangePct.Should().Be(50m);
    }

    private record ValuationPointResponse(string Date, decimal Price);
    private record ValuationSeriesSummaryResponse(decimal First, decimal Last, decimal Min, decimal Max, decimal ChangePct);
    private record ValuationSeriesResponse(string Currency, List<ValuationPointResponse> Points, ValuationSeriesSummaryResponse Summary);
```
> Usa `CompanyEndpointTestBase` (tiene `CreateCompanyViaApiAsync`). La ventana ancla a la **última** valoración (2026-03-10), así que la de 2025-06-10 (≈9 meses antes) entra en `OneYear`.

Run: `dotnet test tests/BigSchool.Integration.Tests --filter GetValuationSeries` → PASS.

- [ ] **Step 7: Verde + Commit**
```bash
git add -A && git commit -m "feat(investments): GET /companies/{id}/valuations/series?period (serie + summary, periodo enum)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 3: #11/D — Rename / Delete de cartera (guards en dominio)

**Files:**
- Create: `src/BigSchool.Domain/Investments/Exceptions/{PortfolioHasOpenPositionsDomainException,PortfolioWithinFiscalGracePeriodDomainException}.cs`
- Modify: `src/BigSchool.Domain/Investments/Entities/Portfolio.cs` (`Rename`, `Delete`)
- Modify: `src/BigSchool.WebApi/Middleware/ExceptionHandlingMiddleware.cs` (mapear las 2 excepciones → 409)
- Create: `src/BigSchool.Application/Investments/Commands/RenamePortfolio/{RenamePortfolioCommand,RenamePortfolioCommandHandler,RenamePortfolioCommandValidator}.cs`
- Create: `src/BigSchool.Application/Investments/Commands/DeletePortfolio/{DeletePortfolioCommand,DeletePortfolioCommandHandler}.cs`
- Modify: `src/BigSchool.WebApi/Controllers/Investments/PortfoliosController.cs`
- Test: `tests/BigSchool.Domain.Tests/Investments/PortfolioRenameDeleteTests.cs`
- Test: `tests/BigSchool.Application.Tests/Investments/RenamePortfolioCommandValidatorTests.cs`
- Test: `tests/BigSchool.Integration.Tests/Investments/PortfolioRenameDeleteTests.cs`

- [ ] **Step 1: Excepciones de dominio (→ 409)**

`PortfolioHasOpenPositionsDomainException.cs`:
```csharp
using BigSchool.Domain.SharedKernel.Exceptions;

namespace BigSchool.Domain.Investments.Exceptions;

public class PortfolioHasOpenPositionsDomainException : DomainException
{
    public PortfolioHasOpenPositionsDomainException(int idPortfolio)
        : base("PORTFOLIO_HAS_OPEN_POSITIONS",
            $"La cartera {idPortfolio} tiene posiciones abiertas y no puede borrarse.") { }
}
```
`PortfolioWithinFiscalGracePeriodDomainException.cs`:
```csharp
using BigSchool.Domain.SharedKernel.Exceptions;

namespace BigSchool.Domain.Investments.Exceptions;

public class PortfolioWithinFiscalGracePeriodDomainException : DomainException
{
    public PortfolioWithinFiscalGracePeriodDomainException(int idPortfolio, DateOnly lastSaleDate)
        : base("PORTFOLIO_WITHIN_FISCAL_GRACE",
            $"La cartera {idPortfolio} no puede borrarse hasta 5 años desde la última venta ({lastSaleDate:yyyy-MM-dd}).") { }
}
```

- [ ] **Step 2: `Portfolio.Rename` + `Portfolio.Delete(today)`**

Añade a `Portfolio` (con `using BigSchool.Domain.Investments.Exceptions;` ya presente):
```csharp
    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre de la cartera es obligatorio.", nameof(name));
        Name = name.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Soft-delete con guards. `today` lo pasa Application (dominio puro, sin reloj).</summary>
    public void Delete(DateOnly today)
    {
        if (_holdings.Any(h => h.IdStatus != EntityStatus.Deleted && h.OpenShares > 0m))
            throw new PortfolioHasOpenPositionsDomainException(IdPortfolio);

        DateOnly? lastSale = _holdings
            .Where(h => h.IdStatus != EntityStatus.Deleted)
            .SelectMany(h => h.Disposals)
            .Where(d => d.IdStatus != EntityStatus.Deleted)
            .Select(d => (DateOnly?)d.SellDate)
            .Max();

        if (lastSale is not null && lastSale.Value > today.AddYears(-5))
            throw new PortfolioWithinFiscalGracePeriodDomainException(IdPortfolio, lastSale.Value);

        IdStatus = EntityStatus.Deleted;
        UpdatedAt = DateTime.UtcNow;
    }
```

- [ ] **Step 3: Mapear las excepciones en el middleware**

En `ExceptionHandlingMiddleware`, añade el mapeo de `PortfolioHasOpenPositionsDomainException` y `PortfolioWithinFiscalGracePeriodDomainException` → **409 Conflict** (sigue el patrón de `EmailAlreadyExistsDomainException`; el `Code`/`Message` del `DomainException` viaja en `ApiError`). Verifica cómo mapea hoy (por tipo concreto o por lista) y añade estas dos.

- [ ] **Step 4: Commands + handlers**

`RenamePortfolioCommand.cs`:
```csharp
using MediatR;
namespace BigSchool.Application.Investments.Commands.RenamePortfolio;
public record RenamePortfolioCommand(int IdPortfolio, int IdUser, string Name) : IRequest;
```
`RenamePortfolioCommandValidator.cs`:
```csharp
using FluentValidation;
namespace BigSchool.Application.Investments.Commands.RenamePortfolio;
public class RenamePortfolioCommandValidator : AbstractValidator<RenamePortfolioCommand>
{
    public RenamePortfolioCommandValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
}
```
`RenamePortfolioCommandHandler.cs`:
```csharp
using BigSchool.Application.Investments.Interfaces.Repositories;
using BigSchool.Domain.SharedKernel.Exceptions;
using MediatR;

namespace BigSchool.Application.Investments.Commands.RenamePortfolio;

public class RenamePortfolioCommandHandler : IRequestHandler<RenamePortfolioCommand>
{
    private readonly IPortfolioRepository _portfolios;
    public RenamePortfolioCommandHandler(IPortfolioRepository portfolios) => _portfolios = portfolios;

    public async Task Handle(RenamePortfolioCommand request, CancellationToken cancellationToken)
    {
        var p = await _portfolios.GetByIdWithHoldingsAsync(request.IdPortfolio, cancellationToken);
        if (p is null || p.IdUser != request.IdUser)
            throw new NotFoundException("Cartera no encontrada.");
        p.Rename(request.Name);
        await _portfolios.UnitOfWork.SaveChangesAsync();
    }
}
```
`DeletePortfolioCommand.cs`:
```csharp
using MediatR;
namespace BigSchool.Application.Investments.Commands.DeletePortfolio;
public record DeletePortfolioCommand(int IdPortfolio, int IdUser) : IRequest;
```
`DeletePortfolioCommandHandler.cs`:
```csharp
using BigSchool.Application.Investments.Interfaces.Repositories;
using BigSchool.Domain.SharedKernel.Exceptions;
using MediatR;

namespace BigSchool.Application.Investments.Commands.DeletePortfolio;

public class DeletePortfolioCommandHandler : IRequestHandler<DeletePortfolioCommand>
{
    private readonly IPortfolioRepository _portfolios;
    public DeletePortfolioCommandHandler(IPortfolioRepository portfolios) => _portfolios = portfolios;

    public async Task Handle(DeletePortfolioCommand request, CancellationToken cancellationToken)
    {
        var p = await _portfolios.GetByIdWithHoldingsAsync(request.IdPortfolio, cancellationToken);
        if (p is null || p.IdUser != request.IdUser)
            throw new NotFoundException("Cartera no encontrada.");
        p.Delete(DateOnly.FromDateTime(DateTime.UtcNow)); // guards → 409 si aplica
        await _portfolios.UnitOfWork.SaveChangesAsync();
    }
}
```

- [ ] **Step 5: Endpoints en `PortfoliosController`**

```csharp
    public record RenamePortfolioRequest(string Name);

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Rename(int id, [FromBody] RenamePortfolioRequest body)
    {
        await _mediator.Send(new BigSchool.Application.Investments.Commands.RenamePortfolio.RenamePortfolioCommand(id, UserId, body.Name));
        return Ok(ApiResponse.Success());
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        await _mediator.Send(new BigSchool.Application.Investments.Commands.DeletePortfolio.DeletePortfolioCommand(id, UserId));
        return Ok(ApiResponse.Success());
    }
```

- [ ] **Step 6: Unit tests de dominio (3 caminos del Delete)**

`PortfolioRenameDeleteTests.cs` (dominio): construye una cartera vía `Portfolio.Create` + `AddHolding`/`SellShares` para montar los escenarios:
```csharp
    [Fact] public void Rename_valido_cambia_nombre() { /* Create → Rename("X") → Name == "X" */ }

    [Fact]
    public void Delete_conPosicionAbierta_lanza_HasOpenPositions()
    { /* AddHolding (sin vender) → OpenShares>0 → Delete(today) lanza PortfolioHasOpenPositionsDomainException */ }

    [Fact]
    public void Delete_ventaReciente_lanza_FiscalGrace()
    { /* AddHolding + SellShares total con sellDate reciente → Delete(hoy) lanza PortfolioWithinFiscalGracePeriodDomainException */ }

    [Fact]
    public void Delete_ventaAntigua_o_sinVentas_soft_borra()
    { /* cerrada con última venta ≥5 años (sellDate antigua) → Delete(hoy) → IdStatus == Deleted */ }
```
> Usa `Portfolio.Create(1, "P", Currency.EUR)`, `AddHolding(...)`, `SellShares(...)` con `rate=1`, `rateDate`/`buyDate`/`sellDate` controladas. Para "venta antigua" pasa `sellDate` > 5 años atrás; el `today` es `DateOnly.FromDateTime(DateTime.UtcNow)`.

`RenamePortfolioCommandValidatorTests.cs`: Name vacío/ >100 → inválido.

- [ ] **Step 7: E2E**

`PortfolioRenameDeleteTests.cs` (integración, `PortfolioEndpointTestBase`):
```csharp
    [Fact] public async Task Put_Renombra() { /* create → PUT {name:"Nueva"} 200 → GET detalle Name=="Nueva" */ }

    [Fact]
    public async Task Delete_ConPosicionAbierta_409_OpenPositions()
    { /* create + AddHolding → DELETE → 409, errors[0].code == "PORTFOLIO_HAS_OPEN_POSITIONS" */ }

    [Fact]
    public async Task Delete_VentaReciente_409_FiscalGrace()
    { /* create + AddHolding + Sell total con sellDate reciente → DELETE → 409 "PORTFOLIO_WITHIN_FISCAL_GRACE" */ }

    [Fact]
    public async Task Delete_VentaAntigua_200_DesapareceDeListados()
    { /* create + AddHolding + Sell total con sellDate > 5 años atrás → DELETE 200 → GET /portfolios no la lista */ }
```
> Para "venta antigua" siembra la `ExchangeRate` a esa `sellDate` antigua (determinismo multimoneda). Verifica el `Code` en `errors[0].code` del envelope.

Run: `dotnet test tests/BigSchool.Integration.Tests --filter PortfolioRenameDelete` → PASS.

- [ ] **Step 8: Verde + Commit**

Run: FULL. Luego:
```bash
git add -A && git commit -m "feat(investments): PUT/DELETE /portfolios/{id} (rename + delete con guards fiscales)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 4: #11/E — `GET /portfolios/summary` (agregado del usuario)

**Files:**
- Create: `src/BigSchool.Application/Investments/DTOs/InvestmentsSummaryDto.cs`
- Create: `src/BigSchool.Application/Investments/Queries/GetInvestmentsSummary/{GetInvestmentsSummaryQuery,GetInvestmentsSummaryQueryHandler}.cs`
- Modify: `src/BigSchool.WebApi/Controllers/Investments/PortfoliosController.cs`
- Test: `tests/BigSchool.Integration.Tests/Investments/GetInvestmentsSummaryTests.cs`

- [ ] **Step 1: DTO + Query + Handler**

`InvestmentsSummaryDto.cs`:
```csharp
namespace BigSchool.Application.Investments.DTOs;

public record InvestmentsSummaryDto(
    string BaseCurrency, decimal MarketValue, decimal CostBasis, decimal UnrealizedPnL,
    decimal RealizedPnL, decimal TotalPnL, decimal ReturnPct, int PortfolioCount);
```
`GetInvestmentsSummaryQuery.cs`:
```csharp
using BigSchool.Application.Investments.DTOs;
using MediatR;

namespace BigSchool.Application.Investments.Queries.GetInvestmentsSummary;

public record GetInvestmentsSummaryQuery(int IdUser) : IRequest<InvestmentsSummaryDto>;
```
`GetInvestmentsSummaryQueryHandler.cs`:
```csharp
using BigSchool.Application.Investments.DTOs;
using BigSchool.Application.Investments.Queries;
using BigSchool.Application.SharedKernel.Interfaces;
using BigSchool.Application.SharedKernel.Interfaces.Services;
using BigSchool.Domain.SharedKernel.Enums;
using Dapper;
using MediatR;

namespace BigSchool.Application.Investments.Queries.GetInvestmentsSummary;

public class GetInvestmentsSummaryQueryHandler : IRequestHandler<GetInvestmentsSummaryQuery, InvestmentsSummaryDto>
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly IUserBaseCurrencyProvider _userBaseCurrencyProvider;

    public GetInvestmentsSummaryQueryHandler(IDbConnectionFactory dbFactory, IUserBaseCurrencyProvider userBaseCurrencyProvider)
    {
        _dbFactory = dbFactory;
        _userBaseCurrencyProvider = userBaseCurrencyProvider;
    }

    private const string SUMMARY_QUERY = @"
SELECT COALESCE(SUM(hv.MarketValue),0)   AS MarketValue,
       COALESCE(SUM(hv.CostBasis),0)     AS CostBasis,
       COALESCE(SUM(hv.UnrealizedPnL),0) AS UnrealizedPnL
FROM (" + PortfolioSqlFragments.HOLDING_VALUATION + @") hv
JOIN Portfolios p ON p.IdPortfolio = hv.IdPortfolio
WHERE p.IdUser = @IdUser AND p.IdStatus <> @StatusDeleted AND hv.OpenShares > 0;
SELECT COALESCE(SUM(RealizedPnL),0) AS RealizedPnL, COUNT(*) AS PortfolioCount
FROM Portfolios WHERE IdUser = @IdUser AND IdStatus <> @StatusDeleted;";

    private sealed record AggRow(decimal MarketValue, decimal CostBasis, decimal UnrealizedPnL);
    private sealed record RealizedRow(decimal RealizedPnL, int PortfolioCount);

    public async Task<InvestmentsSummaryDto> Handle(GetInvestmentsSummaryQuery request, CancellationToken cancellationToken)
    {
        var userBaseCurrency = await _userBaseCurrencyProvider.GetBaseCurrencyAsync(request.IdUser, cancellationToken);
        var baseCurrency = (userBaseCurrency ?? Currency.EUR).ToString();

        var p = new DynamicParameters();
        p.Add("@IdUser", request.IdUser);
        p.Add("@StatusDeleted", EntityStatus.Deleted);
        p.Add("@BaseCurrency", baseCurrency);

        using var conn = _dbFactory.CreateConnection();
        using var multi = await conn.QueryMultipleAsync(SUMMARY_QUERY, p);
        var agg = await multi.ReadSingleAsync<AggRow>();
        var realized = await multi.ReadSingleAsync<RealizedRow>();

        var total = realized.RealizedPnL + agg.UnrealizedPnL;
        var returnPct = agg.CostBasis > 0m ? Math.Round(agg.UnrealizedPnL / agg.CostBasis * 100m, 2) : 0m;

        return new InvestmentsSummaryDto(baseCurrency, agg.MarketValue, agg.CostBasis, agg.UnrealizedPnL,
            realized.RealizedPnL, total, returnPct, realized.PortfolioCount);
    }
}
```

- [ ] **Step 2: Endpoint**

En `PortfoliosController` (`summary` no colisiona con `{id:int}`):
```csharp
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponse<InvestmentsSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Summary()
    {
        var result = await _mediator.Send(new BigSchool.Application.Investments.Queries.GetInvestmentsSummary.GetInvestmentsSummaryQuery(UserId));
        return Ok(ApiResponse<InvestmentsSummaryDto>.Success(result));
    }
```

- [ ] **Step 3: E2E**

`GetInvestmentsSummaryTests.cs`: crea 2 carteras con holdings, verifica que `summary` coincide con la suma de los `performance`; sin carteras → todo 0, `PortfolioCount=0`.
```csharp
    [Fact]
    public async Task Summary_AgregaVariasCarteras()
    {
        var (userId, email) = await SeedUserAsync(Currency.EUR);
        var client = AuthenticatedClient(userId, email);
        await SeedExchangeRateAsync(Currency.USD, Currency.EUR, 1.00m, new DateOnly(2026, 3, 2));
        var p1 = await CreatePortfolioViaApiAsync(client, "A");
        var p2 = await CreatePortfolioViaApiAsync(client, "B");
        await AddHoldingViaApiAsync(client, p1, CompanyAaplUsd, 10m, 195m, "2026-01-05");
        await AddHoldingViaApiAsync(client, p2, CompanyAaplUsd, 5m, 195m, "2026-01-05");

        var perf1 = (await (await client.GetAsync($"/api/v1/portfolios/{p1}/performance")).Content.ReadFromJsonAsync<ApiEnvelope<PerformanceResponse>>())!.Data!;
        var perf2 = (await (await client.GetAsync($"/api/v1/portfolios/{p2}/performance")).Content.ReadFromJsonAsync<ApiEnvelope<PerformanceResponse>>())!.Data!;
        var sum = (await (await client.GetAsync("/api/v1/portfolios/summary")).Content.ReadFromJsonAsync<ApiEnvelope<InvestmentsSummaryResponse>>())!.Data!;

        sum.PortfolioCount.Should().Be(2);
        sum.MarketValue.Should().Be(perf1.MarketValue + perf2.MarketValue);
        sum.CostBasis.Should().Be(perf1.CostBasis + perf2.CostBasis);
        sum.BaseCurrency.Should().Be("EUR");
    }

    private record InvestmentsSummaryResponse(string BaseCurrency, decimal MarketValue, decimal CostBasis, decimal UnrealizedPnL, decimal RealizedPnL, decimal TotalPnL, decimal ReturnPct, int PortfolioCount);
```

Run: `dotnet test tests/BigSchool.Integration.Tests --filter GetInvestmentsSummary` → PASS.

- [ ] **Step 4: Verde + Commit**

Run: FULL. Luego:
```bash
git add -A && git commit -m "feat(investments): GET /portfolios/summary (agregado de inversiones del usuario)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Verificación final (DoD — spec 010 §10)

- [ ] **Unit Domain**: `Portfolio.Rename` valida; `Portfolio.Delete` 3 caminos (posición abierta → `PortfolioHasOpenPositions…`; venta <5 años → `PortfolioWithinFiscalGracePeriod…`; venta ≥5 años/sin ventas → soft-delete).
- [ ] **Unit Application**: validador de rename; validador de `period` (#9, `IsInEnum`); summary/ChangePct con serie vacía.
- [ ] **E2E**: `performance` con `*Original` correctos (moneda empresa) y totales base intactos; `valuations/series?period=OneYear` puntos en ventana anclada + ordenados + summary; `period` inválido → 400; `PUT` renombra; `DELETE` posición abierta → 409 `PORTFOLIO_HAS_OPEN_POSITIONS`, venta reciente → 409 `PORTFOLIO_WITHIN_FISCAL_GRACE`, venta antigua/sin ventas → 200 + desaparece; `summary` = suma de performances. E2E existentes de performance/portfolios/valuations siguen verdes.
- [ ] **FULL** verde.

## Self-Review (cobertura de la spec 010)

| Sección | Tarea(s) |
|---|---|
| §4 #10 holdings `*Original` | 1 |
| §5 #9 valuations series por periodo (periodo = enum `ValuationPeriod`) | 2 |
| §6 #11/D rename/delete (dominio + excepciones + middleware) | 3 |
| §7 #11/E summary global | 4 |
| §10 Verificación | "Verificación final" |
