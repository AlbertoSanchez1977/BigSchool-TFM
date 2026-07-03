# Backend — Paginación de listados Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Generalizar el contrato de paginación ya vigente en `GET /transactions` (`?page&pageSize` + `meta.totalCount`) al resto de listados tabulares — **Companies, Portfolios, Valuations** — reutilizando `PagedResult<T>` y consolidando la normalización page/pageSize en un helper único de SharedKernel.

**Architecture:** Cada listado pasa de devolver `IReadOnlyList<TItem>` a `PagedResult<TItem>`. El handler Dapper ejecuta **COUNT + página en un solo `QueryMultipleAsync`** (espejo exacto de `GetTransactionsQueryHandler`): `SELECT COUNT(*)` con el mismo WHERE pero **sin** joins/GROUP BY de enriquecimiento + `SELECT … LIMIT @PageSize OFFSET @Offset` con `ORDER BY` de desempate único. El controller añade `[FromQuery] page/pageSize` (defaults 1/20) y construye `MetaData`. Sin cambios en DTOs de item ni en lógica de negocio; contrato idéntico al de Transactions. Holdings, series y summaries **no** se paginan (spec 006 §5, §8).

**Tech Stack:** .NET 8, C#, CQRS (MediatR), Dapper + MySQL 8, xUnit + FluentAssertions + `WebApplicationFactory` (E2E sobre `bigschool_test`).

---

## Prerrequisito y convenciones (LEER ANTES DE EMPEZAR)

> **DEPENDENCIA:** Este plan se ejecuta **sobre la estructura modular de la Spec 0 (plan `018-…-backend-modular-monolith.md`)**, que debe estar **mergeada** antes de empezar. Todas las rutas y namespaces de abajo son los **post-018** (modulares). Si el plan 018 aún no está integrado, no ejecutes este.

Namespaces relevantes tras el plan 018 (regla: namespace = carpeta):
- `PagedResult<T>`, `ApiResponse`, `MetaData`, `ApiError`, **`Pagination`** (nuevo) → `BigSchool.Application.SharedKernel.Common`
- `IDbConnectionFactory`, `IRepository` → `BigSchool.Application.SharedKernel.Interfaces`
- `IUserRepository` → `BigSchool.Application.Auth.Interfaces`
- Enums `Sector`/`Market` → `BigSchool.Domain.Investments.Enums`; `Currency`/`EntityStatus` → `BigSchool.Domain.SharedKernel.Enums`
- DTOs de Investments (`CompanyListItemDto`, `PortfolioListItemDto`, `ValuationListItemDto`) → `BigSchool.Application.Investments.DTOs`
- Queries de Investments → `BigSchool.Application.Investments.Queries.{Carpeta}`; `PortfolioSqlFragments` → `BigSchool.Application.Investments.Queries`
- Controllers de Investments → `BigSchool.WebApi.Controllers.Investments`
- `GetTransactionsQuery(Handler)` → `BigSchool.Application.Finanzas.Queries.Transactions.GetTransactions`

### Recetas de verificación

- **BUILD**: cwd `src/backend` → `dotnet build` → *Expected:* `Build succeeded. 0 Error(s)`.
- **UNIT**: `dotnet test tests/BigSchool.Application.Tests` → *Expected:* `Passed!`. No requiere BD.
- **INTEGRATION** (requiere MySQL de `infra/docker-compose.yml`, servicio `mysql`, `localhost:3306`; `BIGSCHOOL_TEST_MYSQL` si difieren credenciales): `dotnet test tests/BigSchool.Integration.Tests` → *Expected:* `Passed!`.
- **FULL**: BUILD + UNIT + INTEGRATION.

### Contrato de referencia (ya implementado — no lo toques salvo el dedup de la Tarea 1)

`GetTransactionsQueryHandler`: COUNT + página con `QueryMultipleAsync`; `MetaData { Page, PageSize, TotalCount }` (TotalPages calculado). `TransactionsController.Get` construye la meta y devuelve `ApiResponse<IReadOnlyList<TItem>>.Success(result.Items, meta)`. **Replica ese patrón exacto** en cada listado.

### Ramas y PRs

Cada tarea = una rama `feature/019-paginacion-taskN` (desde `develop` actualizado o la tarea previa) = un PR pequeño con checkpoint humano.

---

## Task 1: Helper de paginación en SharedKernel + dedup en Transactions

Consolida `NormalizePage`/`NormalizePageSize` (hoy dentro de `GetTransactionsQuery`) en un helper único de SharedKernel con constantes, y refactoriza `GetTransactions` para usarlo. Fundación reutilizada por las tareas 2–4.

**Files:**
- Create: `src/BigSchool.Application/SharedKernel/Common/Pagination.cs`
- Modify: `src/BigSchool.Application/Finanzas/Queries/Transactions/GetTransactions/GetTransactionsQuery.cs` (quitar los métodos estáticos)
- Modify: `src/BigSchool.Application/Finanzas/Queries/Transactions/GetTransactions/GetTransactionsQueryHandler.cs` (usar `Pagination`)
- Test: `tests/BigSchool.Application.Tests/SharedKernel/PaginationTests.cs`

- [x] **Step 1: Escribir el test del helper (falla)**

`PaginationTests.cs`:
```csharp
using BigSchool.Application.SharedKernel.Common;
using FluentAssertions;
using Xunit;

namespace BigSchool.Application.Tests.SharedKernel;

public class PaginationTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(1, 1)]
    [InlineData(3, 3)]
    public void NormalizePage_fuerza_minimo_1(int input, int expected)
        => Pagination.NormalizePage(input).Should().Be(expected);

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(50, 50)]
    [InlineData(100, 100)]
    [InlineData(101, 100)]
    [InlineData(500, 100)]
    public void NormalizePageSize_aplica_default_y_tope(int input, int expected)
        => Pagination.NormalizePageSize(input).Should().Be(expected);
}
```

Run: `dotnet test tests/BigSchool.Application.Tests --filter PaginationTests` → *Expected:* FAIL (no existe `Pagination`).

- [x] **Step 2: Implementar el helper**

`Pagination.cs`:
```csharp
namespace BigSchool.Application.SharedKernel.Common;

/// <summary>Política única de paginación (dedup de la que vivía en GetTransactionsQuery).</summary>
public static class Pagination
{
    public const int DEFAULT_PAGE_SIZE = 20;
    public const int MAX_PAGE_SIZE = 100;

    public static int NormalizePage(int page) => page < 1 ? 1 : page;

    public static int NormalizePageSize(int pageSize) => pageSize switch
    {
        <= 0 => DEFAULT_PAGE_SIZE,
        > MAX_PAGE_SIZE => MAX_PAGE_SIZE,
        _ => pageSize
    };
}
```

Run: `dotnet test tests/BigSchool.Application.Tests --filter PaginationTests` → *Expected:* PASS.

- [x] **Step 3: Refactorizar `GetTransactionsQuery` (quitar la normalización local)**

Deja el record sin los métodos estáticos:
```csharp
using BigSchool.Application.Finanzas.DTOs;
using BigSchool.Application.SharedKernel.Common;
using BigSchool.Domain.Finanzas.Enums;
using MediatR;

namespace BigSchool.Application.Finanzas.Queries.Transactions.GetTransactions;

public record GetTransactionsQuery(
    int IdUser,
    TransactionType? Type,
    MainCategory? IdMainCategory,
    DateOnly? From,
    DateOnly? To,
    int Page,
    int PageSize) : IRequest<PagedResult<TransactionListItemDto>>;
```
> Confirma los `using` reales del fichero tras el plan 018 (DTOs y enums de Finanzas). Lo esencial: **eliminar** `NormalizePage`/`NormalizePageSize` del record.

- [x] **Step 4: Actualizar `GetTransactionsQueryHandler` para usar `Pagination`**

Sustituye las dos líneas de normalización:
```csharp
        var page = Pagination.NormalizePage(request.Page);
        var pageSize = Pagination.NormalizePageSize(request.PageSize);
```
(antes eran `GetTransactionsQuery.NormalizePage(request.Page)` / `…NormalizePageSize(...)`). Añade `using BigSchool.Application.SharedKernel.Common;` si no está. El resto del handler no cambia.

- [x] **Step 5: Verificar**

Run: FULL. *Expected:* verde. Los E2E de Transactions (`GetTransactionsTests`) siguen pasando sin cambios (contrato idéntico).

- [x] **Step 6: Commit**

```bash
git add -A
git commit -m "refactor(backend): helper Pagination en SharedKernel + dedup en GetTransactions

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 2: Paginar `GET /companies`

El `COUNT(*)` va sobre `Companies` con los filtros `Sector/Market`+status (sin el `LEFT JOIN` de última valoración); la página conserva el join + `ORDER BY c.Name, c.IdCompany`.

**Files:**
- Modify: `src/BigSchool.Application/Investments/Queries/GetCompanies/GetCompaniesQuery.cs`
- Modify: `src/BigSchool.Application/Investments/Queries/GetCompanies/GetCompaniesQueryHandler.cs`
- Modify: `src/BigSchool.WebApi/Controllers/Investments/CompaniesController.cs` (método `Get`)
- Test: `tests/BigSchool.Integration.Tests/Investments/GetCompaniesTests.cs` (añadir casos de paginación)

- [x] **Step 1: Query record → `PagedResult` + page/pageSize**

`GetCompaniesQuery.cs`:
```csharp
using BigSchool.Application.Investments.DTOs;
using BigSchool.Application.SharedKernel.Common;
using BigSchool.Domain.Investments.Enums;
using MediatR;

namespace BigSchool.Application.Investments.Queries.GetCompanies;

public record GetCompaniesQuery(Sector? Sector, Market? Market, int Page, int PageSize)
    : IRequest<PagedResult<CompanyListItemDto>>;
```

- [x] **Step 2: Handler → COUNT + página en un `QueryMultipleAsync`**

`GetCompaniesQueryHandler.cs`:
```csharp
using BigSchool.Application.Investments.DTOs;
using BigSchool.Application.SharedKernel.Common;
using BigSchool.Application.SharedKernel.Interfaces;
using BigSchool.Domain.SharedKernel.Enums;
using Dapper;
using MediatR;

namespace BigSchool.Application.Investments.Queries.GetCompanies;

public class GetCompaniesQueryHandler : IRequestHandler<GetCompaniesQuery, PagedResult<CompanyListItemDto>>
{
    private readonly IDbConnectionFactory _dbFactory;

    public GetCompaniesQueryHandler(IDbConnectionFactory dbFactory) => _dbFactory = dbFactory;

    private const string COMPANIES_WHERE = @"WHERE c.IdStatus <> @StatusDeleted
                                              AND (@Sector IS NULL OR c.Sector = @Sector)
                                              AND (@Market IS NULL OR c.Market = @Market)";

    private const string GETCOMPANIES_QUERY = @"SELECT COUNT(*) FROM Companies c " + COMPANIES_WHERE + @";
            SELECT c.IdCompany, c.Name, c.Ticker, c.Sector, c.Market, c.Currency,
                   v.Price AS LastPrice, v.Date AS LastValuationDate
            FROM Companies c
            LEFT JOIN Valuations v ON v.IdValuation = (
                SELECT v2.IdValuation FROM Valuations v2
                WHERE v2.IdCompany = c.IdCompany AND v2.IdStatus <> @StatusDeleted
                ORDER BY v2.Date DESC, v2.IdValuation DESC LIMIT 1)
            " + COMPANIES_WHERE + @"
            ORDER BY c.Name, c.IdCompany
            LIMIT @PageSize OFFSET @Offset;";

    public async Task<PagedResult<CompanyListItemDto>> Handle(GetCompaniesQuery request, CancellationToken cancellationToken)
    {
        var page = Pagination.NormalizePage(request.Page);
        var pageSize = Pagination.NormalizePageSize(request.PageSize);

        var parameters = new DynamicParameters();
        parameters.Add("@StatusDeleted", EntityStatus.Deleted);
        parameters.Add("@Sector", request.Sector?.ToString());
        parameters.Add("@Market", request.Market?.ToString());
        parameters.Add("@PageSize", pageSize);
        parameters.Add("@Offset", (page - 1) * pageSize);

        using var conn = _dbFactory.CreateConnection();
        using var multi = await conn.QueryMultipleAsync(GETCOMPANIES_QUERY, parameters);
        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<CompanyListItemDto>()).ToList();

        return new PagedResult<CompanyListItemDto>(items, page, pageSize, total);
    }
}
```

- [x] **Step 3: Controller — añadir page/pageSize + meta**

En `CompaniesController.cs`, reemplaza el método `Get` por:
```csharp
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CompanyListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(
        [FromQuery] Sector? sector, [FromQuery] Market? market,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetCompaniesQuery(sector, market, page, pageSize));
        var meta = new MetaData { Page = result.Page, PageSize = result.PageSize, TotalCount = result.TotalCount };
        return Ok(ApiResponse<IReadOnlyList<CompanyListItemDto>>.Success(result.Items, meta));
    }
```
> `MetaData` está en `BigSchool.Application.SharedKernel.Common` (ya en los `using` del controller vía `ApiResponse`). El `GetById` y `GetValuations` (la valuations se pagina en la Tarea 4) no se tocan aquí.

- [x] **Step 4: E2E — añadir casos de paginación a `GetCompaniesTests`**

Añade estos métodos a `GetCompaniesTests` (usa los helpers de `CompanyEndpointTestBase`; `SeededCompaniesCount = 4`):
```csharp
    [Fact]
    public async Task Get_Paginado_DevuelvePaginaYMeta_SinSolape()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        await CreateCompanyViaApiAsync(client, new { name = "Extra A", ticker = "PGA1", currency = "EUR" });
        await CreateCompanyViaApiAsync(client, new { name = "Extra B", ticker = "PGB1", currency = "EUR" });
        var total = SeededCompaniesCount + 2;

        var r1 = await client.GetAsync("/api/v1/companies?page=1&pageSize=2");
        r1.StatusCode.Should().Be(HttpStatusCode.OK);
        var e1 = await r1.Content.ReadFromJsonAsync<ApiEnvelope<List<CompanyListItemResponse>>>();
        e1!.Data!.Should().HaveCount(2);
        e1.Meta!.Page.Should().Be(1);
        e1.Meta.PageSize.Should().Be(2);
        e1.Meta.TotalCount.Should().Be(total);

        var r2 = await client.GetAsync("/api/v1/companies?page=2&pageSize=2");
        var e2 = await r2.Content.ReadFromJsonAsync<ApiEnvelope<List<CompanyListItemResponse>>>();
        e2!.Data!.Should().HaveCount(2);
        e1.Data!.Select(c => c.IdCompany).Should().NotIntersectWith(e2.Data!.Select(c => c.IdCompany));
    }

    [Fact]
    public async Task Get_PageSizeSobreMax_SeCapAa100()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);

        var resp = await client.GetAsync("/api/v1/companies?pageSize=500");

        var env = await resp.Content.ReadFromJsonAsync<ApiEnvelope<List<CompanyListItemResponse>>>();
        env!.Meta!.PageSize.Should().Be(100);
        env.Meta.TotalCount.Should().Be(SeededCompaniesCount);
    }
```
> Añade `using System.Linq;` si el `NotIntersectWith`/`Select` lo requiere (FluentAssertions ya lo trae normalmente). El test existente `Get_...` de este fichero sigue verde (default pageSize 20 ≥ 4 seed).

Run: `dotnet test tests/BigSchool.Integration.Tests --filter GetCompaniesTests` → *Expected:* PASS.

- [x] **Step 5: Verificar**

Run: FULL. *Expected:* verde.

- [x] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(backend): paginar GET /companies (page/pageSize + meta.totalCount)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 3: Paginar `GET /portfolios`

El `COUNT(*)` cuenta **carteras** (`Portfolios` con `IdUser`+status), **no** filas del `GROUP BY`. La página conserva el `GROUP BY` + `HOLDING_VALUATION` + `ORDER BY p.IdPortfolio` + `LIMIT/OFFSET`.

**Files:**
- Modify: `src/BigSchool.Application/Investments/Queries/GetPortfolios/GetPortfoliosQuery.cs`
- Modify: `src/BigSchool.Application/Investments/Queries/GetPortfolios/GetPortfoliosQueryHandler.cs`
- Modify: `src/BigSchool.WebApi/Controllers/Investments/PortfoliosController.cs` (método `Get`)
- Test: `tests/BigSchool.Integration.Tests/Investments/GetPortfoliosTests.cs`

- [ ] **Step 1: Query record → `PagedResult` + page/pageSize**

`GetPortfoliosQuery.cs`:
```csharp
using BigSchool.Application.Investments.DTOs;
using BigSchool.Application.SharedKernel.Common;
using MediatR;

namespace BigSchool.Application.Investments.Queries.GetPortfolios;

public record GetPortfoliosQuery(int IdUser, int Page, int PageSize)
    : IRequest<PagedResult<PortfolioListItemDto>>;
```

- [ ] **Step 2: Handler → COUNT de carteras + página agrupada**

`GetPortfoliosQueryHandler.cs`:
```csharp
using BigSchool.Application.Auth.Interfaces;
using BigSchool.Application.Investments.DTOs;
using BigSchool.Application.SharedKernel.Common;
using BigSchool.Application.SharedKernel.Interfaces;
using BigSchool.Domain.SharedKernel.Enums;
using Dapper;
using MediatR;

namespace BigSchool.Application.Investments.Queries.GetPortfolios;

public class GetPortfoliosQueryHandler : IRequestHandler<GetPortfoliosQuery, PagedResult<PortfolioListItemDto>>
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly IUserRepository _userRepository;

    public GetPortfoliosQueryHandler(IDbConnectionFactory dbFactory, IUserRepository userRepository)
    {
        _dbFactory = dbFactory;
        _userRepository = userRepository;
    }

    private const string GETPORTFOLIOS_QUERY = @"
SELECT COUNT(*) FROM Portfolios WHERE IdUser = @IdUser AND IdStatus <> @StatusDeleted;
SELECT p.IdPortfolio, p.Name, p.RealizedPnL, p.RealizedPnLCurrency,
       COALESCE(SUM(hv.MarketValue), 0)   AS MarketValue,
       COALESCE(SUM(hv.CostBasis), 0)     AS CostBasis,
       COALESCE(SUM(hv.UnrealizedPnL), 0) AS UnrealizedPnL,
       (p.RealizedPnL + COALESCE(SUM(hv.UnrealizedPnL), 0)) AS TotalPnL
FROM Portfolios p
LEFT JOIN (" + PortfolioSqlFragments.HOLDING_VALUATION + @") hv
       ON hv.IdPortfolio = p.IdPortfolio AND hv.OpenShares > 0
WHERE p.IdUser = @IdUser AND p.IdStatus <> @StatusDeleted
GROUP BY p.IdPortfolio, p.Name, p.RealizedPnL, p.RealizedPnLCurrency
ORDER BY p.IdPortfolio
LIMIT @PageSize OFFSET @Offset;";

    public async Task<PagedResult<PortfolioListItemDto>> Handle(GetPortfoliosQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.IdUser, cancellationToken);
        var baseCurrency = (user?.BaseCurrency ?? Currency.EUR).ToString();
        var page = Pagination.NormalizePage(request.Page);
        var pageSize = Pagination.NormalizePageSize(request.PageSize);

        var parameters = new DynamicParameters();
        parameters.Add("@IdUser", request.IdUser);
        parameters.Add("@StatusDeleted", EntityStatus.Deleted);
        parameters.Add("@BaseCurrency", baseCurrency);
        parameters.Add("@PageSize", pageSize);
        parameters.Add("@Offset", (page - 1) * pageSize);

        using var conn = _dbFactory.CreateConnection();
        using var multi = await conn.QueryMultipleAsync(GETPORTFOLIOS_QUERY, parameters);
        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<PortfolioListItemDto>()).ToList();

        return new PagedResult<PortfolioListItemDto>(items, page, pageSize, total);
    }
}
```
> `PortfolioSqlFragments` está en el namespace **padre** `BigSchool.Application.Investments.Queries` → visible sin `using` extra desde `…Queries.GetPortfolios`. Si el compilador no lo resuelve, añade `using BigSchool.Application.Investments.Queries;`.

- [ ] **Step 3: Controller — añadir page/pageSize + meta**

En `PortfoliosController.cs`, reemplaza el método `Get`:
```csharp
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PortfolioListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetPortfoliosQuery(UserId, page, pageSize));
        var meta = new MetaData { Page = result.Page, PageSize = result.PageSize, TotalCount = result.TotalCount };
        return Ok(ApiResponse<IReadOnlyList<PortfolioListItemDto>>.Success(result.Items, meta));
    }
```

- [ ] **Step 4: E2E — paginación + `totalCount` cuenta carteras (no holdings)**

Añade a `GetPortfoliosTests` (usa `PortfolioEndpointTestBase`):
```csharp
    [Fact]
    public async Task Get_Paginado_TotalCountCuentaCarteras_NoHoldings()
    {
        var (userId, email) = await SeedUserAsync(Currency.EUR);
        var client = AuthenticatedClient(userId, email);
        await SeedExchangeRateAsync(Currency.USD, Currency.EUR, 0.90m, new DateOnly(2026, 1, 5));

        // Cartera con 2 holdings + 2 carteras vacías → 3 carteras en total.
        var withHoldings = await CreatePortfolioViaApiAsync(client, "Con Holdings");
        await AddHoldingViaApiAsync(client, withHoldings, CompanyAaplUsd, 10m, 195m, "2026-01-05");
        await AddHoldingViaApiAsync(client, withHoldings, CompanyMsftUsd, 5m, 300m, "2026-01-05");
        await CreatePortfolioViaApiAsync(client, "Vacia 1");
        await CreatePortfolioViaApiAsync(client, "Vacia 2");

        var r1 = await client.GetAsync("/api/v1/portfolios?page=1&pageSize=2");
        var e1 = await r1.Content.ReadFromJsonAsync<ApiEnvelope<List<PortfolioListItemResponse>>>();
        e1!.Data!.Should().HaveCount(2);
        e1.Meta!.TotalCount.Should().Be(3);   // 3 carteras, NO 4 (no cuenta los holdings)
        e1.Meta.TotalPages.Should().Be(2);

        var r2 = await client.GetAsync("/api/v1/portfolios?page=2&pageSize=2");
        var e2 = await r2.Content.ReadFromJsonAsync<ApiEnvelope<List<PortfolioListItemResponse>>>();
        e2!.Data!.Should().HaveCount(1);
        e1.Data!.Select(p => p.IdPortfolio).Should().NotIntersectWith(e2.Data!.Select(p => p.IdPortfolio));
    }

    [Fact]
    public async Task Get_SinCarteras_DevuelveListaVacia_TotalCount0()
    {
        var (userId, email) = await SeedUserAsync(Currency.EUR);
        var client = AuthenticatedClient(userId, email);

        var resp = await client.GetAsync("/api/v1/portfolios?page=1&pageSize=20");

        var env = await resp.Content.ReadFromJsonAsync<ApiEnvelope<List<PortfolioListItemResponse>>>();
        env!.Data!.Should().BeEmpty();
        env.Meta!.TotalCount.Should().Be(0);
    }
```
> El test existente `Get_ReturnsSummary_WithMarketValueConvertedToBase` sigue verde (default pageSize 20). El aislamiento entre tests lo garantiza `ResetAsync` (limpia Portfolios/Holdings del usuario).

Run: `dotnet test tests/BigSchool.Integration.Tests --filter GetPortfoliosTests` → *Expected:* PASS.

- [ ] **Step 5: Verificar**

Run: FULL. *Expected:* verde.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(backend): paginar GET /portfolios (totalCount cuenta carteras, no holdings)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 4: Paginar `GET /companies/{id}/valuations`

`COUNT(*)` sobre `Valuations` con `IdCompany`+status; página con el `ORDER BY Date DESC, IdValuation DESC` actual + `LIMIT/OFFSET`.

**Files:**
- Modify: `src/BigSchool.Application/Investments/Queries/GetCompanyValuations/GetCompanyValuationsQuery.cs`
- Modify: `src/BigSchool.Application/Investments/Queries/GetCompanyValuations/GetCompanyValuationsQueryHandler.cs`
- Modify: `src/BigSchool.WebApi/Controllers/Investments/CompaniesController.cs` (método `GetValuations`)
- Test: `tests/BigSchool.Integration.Tests/Investments/GetCompanyValuationsTests.cs`

- [ ] **Step 1: Query record → `PagedResult` + page/pageSize**

`GetCompanyValuationsQuery.cs`:
```csharp
using BigSchool.Application.Investments.DTOs;
using BigSchool.Application.SharedKernel.Common;
using MediatR;

namespace BigSchool.Application.Investments.Queries.GetCompanyValuations;

public record GetCompanyValuationsQuery(int IdCompany, int Page, int PageSize)
    : IRequest<PagedResult<ValuationListItemDto>>;
```

- [ ] **Step 2: Handler → COUNT + página**

`GetCompanyValuationsQueryHandler.cs`:
```csharp
using BigSchool.Application.Investments.DTOs;
using BigSchool.Application.SharedKernel.Common;
using BigSchool.Application.SharedKernel.Interfaces;
using BigSchool.Domain.SharedKernel.Enums;
using Dapper;
using MediatR;

namespace BigSchool.Application.Investments.Queries.GetCompanyValuations;

public class GetCompanyValuationsQueryHandler : IRequestHandler<GetCompanyValuationsQuery, PagedResult<ValuationListItemDto>>
{
    private readonly IDbConnectionFactory _dbFactory;

    public GetCompanyValuationsQueryHandler(IDbConnectionFactory dbFactory) => _dbFactory = dbFactory;

    private const string VALUATIONS_WHERE = @"WHERE IdCompany = @IdCompany AND IdStatus <> @StatusDeleted";

    private const string GETCOMPANYVALUATIONS_QUERY = @"SELECT COUNT(*) FROM Valuations " + VALUATIONS_WHERE + @";
            SELECT IdValuation, IdCompany, Price, PriceCurrency, Date, Source
            FROM Valuations " + VALUATIONS_WHERE + @"
            ORDER BY Date DESC, IdValuation DESC
            LIMIT @PageSize OFFSET @Offset;";

    public async Task<PagedResult<ValuationListItemDto>> Handle(GetCompanyValuationsQuery request, CancellationToken cancellationToken)
    {
        var page = Pagination.NormalizePage(request.Page);
        var pageSize = Pagination.NormalizePageSize(request.PageSize);

        var parameters = new DynamicParameters();
        parameters.Add("@IdCompany", request.IdCompany);
        parameters.Add("@StatusDeleted", EntityStatus.Deleted);
        parameters.Add("@PageSize", pageSize);
        parameters.Add("@Offset", (page - 1) * pageSize);

        using var conn = _dbFactory.CreateConnection();
        using var multi = await conn.QueryMultipleAsync(GETCOMPANYVALUATIONS_QUERY, parameters);
        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<ValuationListItemDto>()).ToList();

        return new PagedResult<ValuationListItemDto>(items, page, pageSize, total);
    }
}
```

- [ ] **Step 3: Controller — añadir page/pageSize + meta**

En `CompaniesController.cs`, reemplaza el método `GetValuations`:
```csharp
    [HttpGet("{id:int}/valuations")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ValuationListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetValuations(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetCompanyValuationsQuery(id, page, pageSize));
        var meta = new MetaData { Page = result.Page, PageSize = result.PageSize, TotalCount = result.TotalCount };
        return Ok(ApiResponse<IReadOnlyList<ValuationListItemDto>>.Success(result.Items, meta));
    }
```

- [ ] **Step 4: E2E — añadir casos de paginación a `GetCompanyValuationsTests`**

Añade a `GetCompanyValuationsTests` (usa `CompanyEndpointTestBase`):
```csharp
    [Fact]
    public async Task GetValuations_Paginado_DevuelvePaginaYMeta()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        var ticker = $"P{Guid.NewGuid():N}".Substring(0, 8).ToUpperInvariant();
        var id = await CreateCompanyViaApiAsync(client, new { name = "Paged Co.", ticker, currency = "USD" });
        // 3 valoraciones
        await client.PostAsJsonAsync($"/api/v1/companies/{id}/valuations", new { price = 100m, date = "2026-01-10", source = (string?)null });
        await client.PostAsJsonAsync($"/api/v1/companies/{id}/valuations", new { price = 110m, date = "2026-02-10", source = (string?)null });
        await client.PostAsJsonAsync($"/api/v1/companies/{id}/valuations", new { price = 120m, date = "2026-03-10", source = (string?)null });

        var r1 = await client.GetAsync($"/api/v1/companies/{id}/valuations?page=1&pageSize=2");
        var e1 = await r1.Content.ReadFromJsonAsync<ApiEnvelope<List<ValuationListItemResponse>>>();
        e1!.Data!.Should().HaveCount(2);
        e1.Meta!.TotalCount.Should().Be(3);
        e1.Meta.PageSize.Should().Be(2);
        e1.Data![0].Date.Should().Be("2026-03-10");   // orden fecha desc estable

        var r2 = await client.GetAsync($"/api/v1/companies/{id}/valuations?page=2&pageSize=2");
        var e2 = await r2.Content.ReadFromJsonAsync<ApiEnvelope<List<ValuationListItemResponse>>>();
        e2!.Data!.Should().HaveCount(1);
        e1.Data!.Select(v => v.IdValuation).Should().NotIntersectWith(e2.Data!.Select(v => v.IdValuation));
    }
```
> Los tests existentes de este fichero siguen verdes (default pageSize 20 ≥ 2; empresa inexistente → lista vacía + `totalCount` 0).

Run: `dotnet test tests/BigSchool.Integration.Tests --filter GetCompanyValuationsTests` → *Expected:* PASS.

- [ ] **Step 5: Verificar**

Run: FULL. *Expected:* verde.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(backend): paginar GET /companies/{id}/valuations (page/pageSize + meta.totalCount)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Verificación final (Definition of Done — spec 006 §10)

- [ ] `Companies`, `Portfolios`, `Valuations` exponen `?page&pageSize` y `meta { page, pageSize, totalCount, totalPages }`, contrato idéntico a Transactions.
- [ ] `Pagination.NormalizePage/NormalizePageSize` en SharedKernel (unit test verde); `GetTransactionsQuery` ya no duplica la lógica.
- [ ] E2E por listado: primera página con `pageSize` ítems, `totalCount` correcto, segunda página sin solape (paginado determinista por `ORDER BY` con desempate); `pageSize` fuera de rango capado a 100; listado vacío → `data: []`, `totalCount: 0`.
- [ ] **Portfolios**: `totalCount` cuenta **carteras**, no filas del `GROUP BY` (test dedicado con cartera con holdings).
- [ ] `GET /transactions` y `GET /portfolios/{id}` (detalle con holdings) **no cambian**; holdings, series (`monthly-chart`) y summaries **no** se paginan.
- [ ] Toda la suite existente (unit + E2E) sigue verde; los tests previos de estos listados pasan sin cambios (defaults 1/20).

---

## Self-Review (cobertura de la spec 006)

| Sección spec 006 | Tarea(s) |
|---|---|
| §3 Contrato de paginación (reutilizado) | 1 (helper) + 2–4 (uso) |
| §4 Endpoints afectados (Companies/Portfolios/Valuations) + ORDER BY desempate | 2, 3, 4 |
| §5 Holdings NO se paginan | (no-op explícito; `GET /portfolios/{id}` no se toca) |
| §6 Patrón de implementación (COUNT+página, QueryMultipleAsync) | 2, 3, 4 |
| §7 Consolidación NormalizePage/PageSize en SharedKernel | 1 |
| §8 Qué NO se pagina (series/summaries) | (no se tocan esos endpoints) |
| §10 Verificación (unit + E2E, totalCount carteras) | "Verificación final" + E2E por tarea |
