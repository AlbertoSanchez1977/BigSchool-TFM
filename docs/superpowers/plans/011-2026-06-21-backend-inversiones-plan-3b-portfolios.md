# Plan 3B — BC Inversiones: Carteras (Portfolio + Holding + Disposal, FIFO) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementar el agregado `Portfolio` (AR por-usuario) con `Holding` (lote de compra) y `Disposal` (venta), ventas FIFO a nivel (Portfolio, Company), `RealizedPnL` persistido, y CQRS/endpoints/performance multimoneda, consumiendo el catálogo `Company`/`Valuation` del Plan 3A.

**Architecture:** Clean Architecture + CQRS + DDD. Agregado de 3 niveles `Portfolio → Holding → Disposal`. Commands con EF Core (mutación SIEMPRE a través del AR); queries con Dapper consolidando en la moneda base del usuario (conversión de la última `Valuation` con fallback al último tipo conocido ≤ fecha). `MoneyConversion` snapshot congelado a `BuyDate`/`SellDate` (reutiliza el patrón de `Transaction`). FIFO obligatorio (IRPF) en el dominio.

**Tech Stack:** .NET 8, EF Core + MySQL 8, Dapper, MediatR, FluentValidation, Autofac (auto-registro), xUnit + FluentAssertions + Moq, WebApplicationFactory E2E.

**Spec de referencia:** `docs/superpowers/specs/002-2026-06-21-backend-inversiones-design.md` (sections 3.2, 4, 5, 6, 7, 8, 9, 11).

---

## Convenciones del proyecto (LEER ANTES DE EMPEZAR)

Estas reglas vienen del código real del Plan 2/3A y de feedback del usuario. **Respetarlas al pie de la letra.**

1. **Organización en carpeta `Investments/`** (igual que 3A): commands en `Application/Commands/Investments/<Caso>/`, queries en `Application/Queries/Investments/<Caso>/`, DTOs en `Application/DTOs/Investments/`, tests en `tests/BigSchool.Application.Tests/Commands/Investments/` y `.../Queries/Investments/`.
2. **Constructores de entidades** (canónico `User.cs`/`Valuation.cs`):
   - AR (`Portfolio`): `protected Ctor()` (EF) + `private Ctor(todos los campos)` + `public static Create(...)` que valida.
   - Entidades **NO-AR** (`Holding`, `Disposal`): `protected Ctor()` (EF) + `private Ctor(todos los campos)` + **`internal static Create(...)`** (alta solo a través del AR). **No** object initializers en factories.
3. **DDD — acceso y testing por el AR (CRÍTICO, feedback del usuario):**
   - **NUNCA usar `InternalsVisibleTo`.** Las hijas/nietas (`Holding`, `Disposal`) se acceden **siempre** a través del AR `Portfolio`.
   - Sus factories/mutadores (`Create`, `RecordDisposal`, `Delete`, `UpdateNotes`) son `internal`: los llama el `Portfolio` dentro del assembly Domain.
   - **Los tests NO construyen `Holding`/`Disposal` directamente.** Ejercitan el dominio por la API pública del AR (`portfolio.AddHolding(...)`, `portfolio.SellShares(...)`, `portfolio.UpdateHolding(...)`, `portfolio.DeleteHolding(...)`) y verifican el estado por los getters públicos (`portfolio.Holdings`, `holding.Disposals`, `OpenShares`, …).
   - Consecuencia: **no hay `HoldingTests`/`DisposalTests`.** Toda la lógica de hijas/nietas (FIFO, OpenShares, RealizedPnL, cascada de soft-delete) se cubre en `PortfolioTests` (Task 3). Las Tasks 1 y 2 (Disposal, Holding) son **solo implementación**; su verificación es que el solution compila.
4. **EF Configurations** estructuradas en métodos privados, en este orden: `ConfigureProperties` → `ConfigureRelationships` → `ConfigureIndexes` → `ConfigureFilters`, más `builder.Ignore(x => x.DomainEvents)`. (Patrón de `ValuationConfiguration`.)
5. **Magic numbers de longitud**: se usan **literales inline** (`HasMaxLength(500)`, `MaximumLength(500)`) como en todo el código. NO introducir clase de constantes (decisión consensuada: consistencia + YAGNI; posible refactor global futuro).
6. **Dapper**: SQL como `private const string ..._QUERY` UPPERCASE a nivel de clase; `DynamicParameters`; enums persistidos pasados como parámetro (`parameters.Add("@StatusDeleted", EntityStatus.Deleted)`), nunca números mágicos en el SQL.
7. **DTOs**: los de **command** (construidos desde dominio) usan `Currency`/enums tipados; los de **query** (Dapper) usan `string` para monedas (CHAR(3)→enum no lo convierte Dapper) y `DateOnly` para fechas (hay `DateOnlyTypeHandler` global).
8. **Excepciones → HTTP** (middleware `ExceptionHandlingMiddleware`): `ConflictException`→409, `NotFoundException`→404 (`ENTITY_NOT_FOUND`), `DomainException`→400 (usa `ErrorCode`), `ValidationException`→400 (`VALIDATION_ERROR`+`Field`).
9. **Ownership**: `Portfolio`/`Holding`/`Disposal` por `IdUser` del `Portfolio`; recurso ajeno o inexistente → **404** (`NotFoundException`), no 403.
10. **Autofac auto-registra** repos/handlers/validators por capa (`AsImplementedInterfaces`). **No** hay task de DI manual.
11. **Bloqueo de DLL por Visual Studio**: si `devenv.exe` tiene la app abierta, los builds en Debug fallan (MSB3027/MSB3021). **Compilar y testear con `-c Release`**.

**Comandos de build/test (Release):**
```bash
dotnet build src/backend/Backend.sln -c Release
dotnet test src/backend/tests/BigSchool.Domain.Tests/BigSchool.Domain.Tests.csproj -c Release
dotnet test src/backend/tests/BigSchool.Application.Tests/BigSchool.Application.Tests.csproj -c Release
dotnet test src/backend/tests/BigSchool.Integration.Tests/BigSchool.Integration.Tests.csproj -c Release
```
> Los E2E requieren MySQL de docker-compose levantado (`localhost:3306`, BD `bigschool_test`). Si no hay Docker, los E2E se omiten y se documenta.

**Datos seedeados del Plan 3A (disponibles para E2E sin red):**
- Companies (IDs fijos 1-4): `AAPL`/USD (NASDAQ), `MSFT`/USD, `SAN`/EUR (BME), `SHEL`/GBP (LSE).
- Valuations (2 por empresa, fechas 2026-01-02 y 2026-03-02): AAPL 195→210 USD, MSFT 420→440 USD, SAN 4.5→4.8 EUR, SHEL 28→30 GBP.
- ExchangeRates seed (`Source='seed'`, `RateDate=2026-01-01`): USD→EUR 0.92, GBP→EUR 1.17, CHF→EUR 1.06, JPY→EUR 0.0061.

---

## Estructura de ficheros (qué se crea/modifica)

**Domain** (`src/backend/src/BigSchool.Domain/`)
- Crear `Entities/Disposal.cs` → entidad nieta (venta).
- Reemplazar `Entities/Holding.cs` (stub) → entidad hija (lote).
- Reemplazar `Entities/Portfolio.cs` (stub) → AR completo.
- Crear `Exceptions/InsufficientSharesDomainException.cs`.

**Infrastructure** (`src/backend/src/BigSchool.Infrastructure/`)
- Crear `Persistence/Configurations/{PortfolioConfiguration,HoldingConfiguration,DisposalConfiguration}.cs`.
- Modificar `Persistence/BigSchoolDbContext.cs` (DbSets Portfolios/Holdings/Disposals).
- Migración `CreatePortfolios` (generada por EF).
- Crear `Persistence/Repositories/PortfolioRepository.cs`.

**Application** (`src/backend/src/BigSchool.Application/`)
- Modificar `Interfaces/Repositories/IPortfolioRepository.cs` (añadir `GetByIdWithHoldingsAsync`).
- Commands: `Commands/Investments/{CreatePortfolio,AddHolding,UpdateHolding,DeleteHolding,SellShares}/` (Command + Handler + Validator).
- Queries: `Queries/Investments/{GetPortfolios,GetPortfolioById,GetPortfolioPerformance}/` (Query + Handler + DTO).
- DTOs: `DTOs/Investments/{PortfolioDto,HoldingDto,DisposalDto,SellSharesResultDto}.cs`.

**WebApi** (`src/backend/src/BigSchool.WebApi/`)
- Crear `Controllers/PortfoliosController.cs`.

**Tests**
- Domain: `Entities/PortfolioTests.cs` (único test de dominio del agregado; cubre Holding/Disposal vía el AR).
- Application: handler + validator tests por caso.
- Integration: `Investments/PortfolioEndpointTestBase.cs` + un fichero por endpoint + ciclo completo; `MySqlDatabaseFixture.ResetAsync` ampliado.

**Docs**: `docs/02-backend-design.md`, `docs/01-arquitectura.md` (ADR), `docs/diario.md`.

---

## Task 1: Domain — `Disposal` (entidad nieta = venta) — solo implementación

> Sin test propio (no se construye fuera del AR; su comportamiento se valida en `PortfolioTests`, Task 3). Verificación = compila.

**Files:**
- Create: `src/backend/src/BigSchool.Domain/Entities/Disposal.cs`

- [x] **Step 1: Implementar `Disposal`**

```csharp
using BigSchool.Domain.Enums;
using BigSchool.Domain.ValueObjects;

namespace BigSchool.Domain.Entities;

/// <summary>
/// Venta (disposición) de shares de un lote (Holding). Entidad nieta del agregado Portfolio.
/// SellPrice es un snapshot MoneyConversion congelado a SellDate; RealizedPnL se calcula en el dominio.
/// Alta vía Holding.RecordDisposal (interno al agregado). No se construye nunca fuera del AR.
/// </summary>
public class Disposal : BaseEntity
{
    public int IdDisposal { get; private set; }
    public decimal Shares { get; private set; }
    public MoneyConversion SellPrice { get; private set; } = null!;
    public DateOnly SellDate { get; private set; }
    public Money RealizedPnL { get; private set; } = null!;
    public string? Notes { get; private set; }
    public EntityStatus IdStatus { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    protected Disposal() { } // EF Core

    private Disposal(decimal shares, MoneyConversion sellPrice, Money realizedPnL,
        DateOnly sellDate, string? notes, EntityStatus idStatus, DateTime createdAt)
    {
        Shares = shares;
        SellPrice = sellPrice;
        RealizedPnL = realizedPnL;
        SellDate = sellDate;
        Notes = notes;
        IdStatus = idStatus;
        CreatedAt = createdAt;
    }

    internal static Disposal Create(decimal shares, MoneyConversion sellPrice, Money realizedPnL,
        DateOnly sellDate, string? notes)
    {
        return new Disposal(shares, sellPrice, realizedPnL, sellDate,
            string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            EntityStatus.Active, DateTime.UtcNow);
    }

    internal void Delete()
    {
        IdStatus = EntityStatus.Deleted;
        UpdatedAt = DateTime.UtcNow;
    }
}
```

- [x] **Step 2: Verificar que compila**

Run: `dotnet build src/backend/src/BigSchool.Domain/BigSchool.Domain.csproj -c Release`
Expected: Build succeeded.

- [x] **Step 3: Commit**

```bash
git add src/backend/src/BigSchool.Domain/Entities/Disposal.cs
git commit -m "feat: entidad Disposal (venta, nieta del agregado Portfolio)"
```

---

## Task 2: Domain — `Holding` (entidad hija = lote de compra) — solo implementación

> Sin test propio (se valida vía el AR en `PortfolioTests`, Task 3). Verificación = compila.

**Files:**
- Modify (replace stub): `src/backend/src/BigSchool.Domain/Entities/Holding.cs`

- [x] **Step 1: Implementar `Holding`** (reemplaza el stub vacío completo)

```csharp
using BigSchool.Domain.Enums;
using BigSchool.Domain.ValueObjects;

namespace BigSchool.Domain.Entities;

/// <summary>
/// Lote de compra (tranche) de una Company dentro de un Portfolio. Entidad hija del agregado.
/// Shares es inmutable (las compradas); OpenShares se deriva restando las disposiciones activas.
/// AvgBuyPrice es un snapshot MoneyConversion congelado a BuyDate. Alta vía Portfolio.AddHolding.
/// No se construye ni se muta nunca fuera del AR (Portfolio).
/// </summary>
public class Holding : BaseEntity
{
    public int IdHolding { get; private set; }
    public int IdCompany { get; private set; }
    public decimal Shares { get; private set; }
    public MoneyConversion AvgBuyPrice { get; private set; } = null!;
    public DateOnly BuyDate { get; private set; }
    public string? Notes { get; private set; }
    public EntityStatus IdStatus { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private readonly List<Disposal> _disposals = [];
    public IReadOnlyCollection<Disposal> Disposals => _disposals.AsReadOnly();

    /// <summary>Shares aún abiertas = compradas − vendidas (disposiciones activas).</summary>
    public decimal OpenShares =>
        Shares - _disposals.Where(d => d.IdStatus != EntityStatus.Deleted).Sum(d => d.Shares);

    /// <summary>Un lote está cerrado cuando ya no quedan shares abiertas.</summary>
    public bool IsClosed => OpenShares == 0m;

    protected Holding() { } // EF Core

    private Holding(int companyId, decimal shares, MoneyConversion avgBuyPrice, DateOnly buyDate,
        string? notes, EntityStatus idStatus, DateTime createdAt)
    {
        IdCompany = companyId;
        Shares = shares;
        AvgBuyPrice = avgBuyPrice;
        BuyDate = buyDate;
        Notes = notes;
        IdStatus = idStatus;
        CreatedAt = createdAt;
    }

    internal static Holding Create(int companyId, decimal shares, MoneyConversion avgBuyPrice,
        DateOnly buyDate, string? notes)
    {
        if (companyId <= 0)
            throw new ArgumentException("El identificador de empresa es obligatorio.", nameof(companyId));
        if (shares <= 0m)
            throw new ArgumentException("Las shares deben ser mayores que cero.", nameof(shares));

        return new Holding(companyId, shares, avgBuyPrice, buyDate,
            string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            EntityStatus.Active, DateTime.UtcNow);
    }

    /// <summary>Registra una venta de este lote. El SellPrice (MoneyConversion) lo construye el AR (Portfolio). RealizedPnL = (SellPrice.Base − AvgBuyPrice.Base) × shares, en moneda base.</summary>
    internal Disposal RecordDisposal(decimal shares, MoneyConversion sellPrice, DateOnly sellDate, string? notes)
    {
        if (shares <= 0m)
            throw new ArgumentException("Las shares vendidas deben ser mayores que cero.", nameof(shares));
        if (shares > OpenShares)
            throw new ArgumentException("No se pueden vender más shares de las abiertas en el lote.", nameof(shares));

        var baseCurrency = AvgBuyPrice.Base.Currency;
        var realizedAmount = (sellPrice.Base.Amount - AvgBuyPrice.Base.Amount) * shares;
        var realizedPnL = Money.Create(realizedAmount, baseCurrency);

        var disposal = Disposal.Create(shares, sellPrice, realizedPnL, sellDate, notes);
        _disposals.Add(disposal);
        UpdatedAt = DateTime.UtcNow;
        return disposal;
    }

    internal void UpdateNotes(string? notes)
    {
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Soft-delete del lote y de sus disposiciones activas. Devuelve la suma del RealizedPnL revertido
    /// para que el Portfolio ajuste su columna RealizedPnL y mantenga el invariante.
    /// </summary>
    internal decimal Delete()
    {
        var reversed = 0m;
        foreach (var disposal in _disposals.Where(d => d.IdStatus != EntityStatus.Deleted))
        {
            reversed += disposal.RealizedPnL.Amount;
            disposal.Delete();
        }
        IdStatus = EntityStatus.Deleted;
        UpdatedAt = DateTime.UtcNow;
        return reversed;
    }
}
```

- [x] **Step 2: Verificar que compila**

Run: `dotnet build src/backend/src/BigSchool.Domain/BigSchool.Domain.csproj -c Release`
Expected: Build succeeded.

- [x] **Step 3: Commit**

```bash
git add src/backend/src/BigSchool.Domain/Entities/Holding.cs
git commit -m "feat: entidad Holding (lote) con OpenShares, RecordDisposal y soft-delete en cascada"
```

---

## Task 3: Domain — `Portfolio` (AR) + `InsufficientSharesDomainException` + `PortfolioTests`

Aquí está **toda** la lógica del agregado y **todos** los tests de dominio (ejercitan `Holding`/`Disposal` vía el AR).

**Files:**
- Create: `src/backend/src/BigSchool.Domain/Exceptions/InsufficientSharesDomainException.cs`
- Modify (replace stub): `src/backend/src/BigSchool.Domain/Entities/Portfolio.cs`
- Test: `src/backend/tests/BigSchool.Domain.Tests/Entities/PortfolioTests.cs`

- [x] **Step 1: Escribir el test que falla** (`PortfolioTests.cs`)

```csharp
using BigSchool.Domain.Entities;
using BigSchool.Domain.Enums;
using BigSchool.Domain.Exceptions;
using BigSchool.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace BigSchool.Domain.Tests.Entities;

public class PortfolioTests
{
    // Helper: precio de compra/venta como Money en moneda de la empresa (USD por defecto).
    private static Money Px(decimal amount, Currency ccy = Currency.USD) => Money.Create(amount, ccy);

    private static Portfolio NewPortfolio() => Portfolio.Create(1, "Mi cartera", Currency.EUR);

    [Fact]
    public void Create_InitializesRealizedPnLToZeroInBaseCurrency()
    {
        var p = Portfolio.Create(1, "  Cartera  ", Currency.EUR);

        p.IdUser.Should().Be(1);
        p.Name.Should().Be("Cartera"); // trim
        p.RealizedPnL.Amount.Should().Be(0m);
        p.RealizedPnL.Currency.Should().Be(Currency.EUR);
        p.IdStatus.Should().Be(EntityStatus.Active);
        p.Holdings.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_InvalidUser_Throws(int userId)
    {
        var act = () => Portfolio.Create(userId, "X", Currency.EUR);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_EmptyName_Throws()
    {
        var act = () => Portfolio.Create(1, "  ", Currency.EUR);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddHolding_CreatesLot_WithFrozenCostSnapshot()
    {
        var p = NewPortfolio();

        var h = p.AddHolding(companyId: 1, shares: 100m, buyPrice: Px(195m),
            baseCurrency: Currency.EUR, rate: 0.9m, buyDate: new DateOnly(2026, 1, 1),
            rateDate: new DateOnly(2026, 1, 1), notes: "lote 1");

        p.Holdings.Should().ContainSingle();
        h.Shares.Should().Be(100m);
        h.OpenShares.Should().Be(100m);
        h.AvgBuyPrice.Original.Amount.Should().Be(195m);
        h.AvgBuyPrice.Original.Currency.Should().Be(Currency.USD);
        h.AvgBuyPrice.Rate.Should().Be(0.9m);
        h.AvgBuyPrice.Base.Amount.Should().Be(175.50m); // 195 * 0.9
        h.AvgBuyPrice.Base.Currency.Should().Be(Currency.EUR);
    }

    [Fact]
    public void AddHolding_BaseCurrencyMismatch_Throws()
    {
        var p = NewPortfolio(); // base EUR
        var act = () => p.AddHolding(1, 10m, Px(195m), Currency.USD, 1m,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), null);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddHolding_SameCompanyTwice_CreatesTwoLots()
    {
        var p = NewPortfolio();
        p.AddHolding(1, 100m, Px(100m), Currency.EUR, 1m, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), null);
        p.AddHolding(1, 50m, Px(120m), Currency.EUR, 1m, new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 1), null);

        p.Holdings.Should().HaveCount(2);
    }

    [Fact]
    public void SellShares_SingleLot_PartialSell_GeneratesOneDisposal_AndAccumulatesRealized()
    {
        var p = NewPortfolio();
        // Compra EUR (rate 1) para aislar el cálculo: coste unidad base = 100
        p.AddHolding(3, 100m, Px(100m, Currency.EUR), Currency.EUR, 1m, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), null);

        var disposals = p.SellShares(companyId: 3, shares: 40m, sellPrice: Px(150m, Currency.EUR),
            baseCurrency: Currency.EUR, rate: 1m, sellDate: new DateOnly(2026, 3, 2), rateDate: new DateOnly(2026, 3, 2));

        disposals.Should().ContainSingle();
        disposals[0].Shares.Should().Be(40m);
        disposals[0].RealizedPnL.Amount.Should().Be(2000m); // (150-100)*40
        p.RealizedPnL.Amount.Should().Be(2000m);
        p.Holdings.Single().OpenShares.Should().Be(60m);
    }

    [Fact]
    public void SellShares_FIFO_ConsumesOldestLotsFirst_AcrossLots()
    {
        var p = NewPortfolio();
        // Lote A (más antiguo): 100 @ coste base 100. Lote B: 100 @ coste base 200.
        p.AddHolding(3, 100m, Px(100m, Currency.EUR), Currency.EUR, 1m, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), "A");
        p.AddHolding(3, 100m, Px(200m, Currency.EUR), Currency.EUR, 1m, new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 1), "B");

        // Vende 150 @ 250: consume 100 de A y 50 de B → 2 disposals.
        var disposals = p.SellShares(3, 150m, Px(250m, Currency.EUR), Currency.EUR, 1m,
            new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 2));

        disposals.Should().HaveCount(2);
        disposals[0].Shares.Should().Be(100m);                 // lote A completo
        disposals[0].RealizedPnL.Amount.Should().Be(15000m);   // (250-100)*100
        disposals[1].Shares.Should().Be(50m);                  // parte de lote B
        disposals[1].RealizedPnL.Amount.Should().Be(2500m);    // (250-200)*50

        var lots = p.Holdings.OrderBy(h => h.BuyDate).ToList();
        lots[0].OpenShares.Should().Be(0m);   // A cerrado
        lots[0].IsClosed.Should().BeTrue();
        lots[1].OpenShares.Should().Be(50m);  // B parcialmente abierto

        p.RealizedPnL.Amount.Should().Be(17500m); // 15000 + 2500
    }

    [Fact]
    public void SellShares_IncludesFxEffect_BetweenBuyAndSellDates()
    {
        var p = NewPortfolio(); // base EUR
        // Compra USD 100 @ rate 0.9 → coste base 90/u.
        p.AddHolding(1, 10m, Px(100m), Currency.EUR, 0.9m, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), null);
        // Vende USD 100 @ rate 1.0 (mismo precio en USD, pero el FX subió) → venta base 100/u.
        var disposals = p.SellShares(1, 10m, Px(100m), Currency.EUR, 1.0m,
            new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 2));

        // El P/L realizado captura el efecto FX: (100 - 90) * 10 = 100, aunque el precio en USD no cambió.
        disposals[0].RealizedPnL.Amount.Should().Be(100m);
        p.RealizedPnL.Amount.Should().Be(100m);
    }

    [Fact]
    public void SellShares_MoreThanAvailable_ThrowsInsufficientShares()
    {
        var p = NewPortfolio();
        p.AddHolding(3, 100m, Px(100m, Currency.EUR), Currency.EUR, 1m, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), null);

        var act = () => p.SellShares(3, 101m, Px(150m, Currency.EUR), Currency.EUR, 1m,
            new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 2));

        act.Should().Throw<InsufficientSharesDomainException>();
        p.RealizedPnL.Amount.Should().Be(0m); // no se mutó nada
    }

    [Fact]
    public void SellShares_OnlyConsidersThatCompany_OpenLots()
    {
        var p = NewPortfolio();
        p.AddHolding(1, 50m, Px(100m), Currency.EUR, 0.9m, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), null); // AAPL
        p.AddHolding(3, 50m, Px(100m, Currency.EUR), Currency.EUR, 1m, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), null); // SAN

        // Vender 60 de SAN (id 3) debe fallar aunque haya 50 de AAPL: solo cuentan los lotes de la misma Company.
        var act = () => p.SellShares(3, 60m, Px(150m, Currency.EUR), Currency.EUR, 1m,
            new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 2));
        act.Should().Throw<InsufficientSharesDomainException>();
    }

    [Fact]
    public void UpdateHolding_UpdatesNotes()
    {
        var p = NewPortfolio();
        var h = p.AddHolding(1, 10m, Px(100m), Currency.EUR, 0.9m, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), "old");

        p.UpdateHolding(h.IdHolding, "new notes");

        // IdHolding es 0 en memoria (no persistido); UpdateHolding lo localiza igual por referencia de id 0.
        p.Holdings.Single().Notes.Should().Be("new notes");
    }

    [Fact]
    public void UpdateHolding_UnknownId_ThrowsNotFound()
    {
        var p = NewPortfolio();
        var act = () => p.UpdateHolding(999, "x");
        act.Should().Throw<NotFoundException>();
    }

    [Fact]
    public void DeleteHolding_SoftDeletesLotAndDisposals_AndReversesRealized()
    {
        var p = NewPortfolio();
        var h = p.AddHolding(3, 100m, Px(100m, Currency.EUR), Currency.EUR, 1m, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), null);
        p.SellShares(3, 40m, Px(150m, Currency.EUR), Currency.EUR, 1m, new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 2));
        p.RealizedPnL.Amount.Should().Be(2000m);

        p.DeleteHolding(h.IdHolding);

        p.Holdings.Single().IdStatus.Should().Be(EntityStatus.Deleted);
        p.Holdings.Single().Disposals.Should().OnlyContain(d => d.IdStatus == EntityStatus.Deleted);
        p.RealizedPnL.Amount.Should().Be(0m); // realizado revertido
    }
}
```

> Nota sobre `IdHolding == 0` en memoria: en los tests todos los lotes no persistidos tienen `IdHolding = 0`. Los tests que usan `UpdateHolding`/`DeleteHolding` por id operan sobre carteras con **un solo** lote (id 0). Los tests de FIFO no dependen de `IdHolding` porque ordenan por `BuyDate` (todas distintas). El desempate por `IdHolding` solo importa con BuyDates iguales, escenario que se cubre en E2E (Task 15) con ids reales de BD.

- [x] **Step 2: Ejecutar el test (debe fallar)**

Run: `dotnet test src/backend/tests/BigSchool.Domain.Tests/BigSchool.Domain.Tests.csproj -c Release --filter FullyQualifiedName~PortfolioTests`
Expected: FAIL (no compila: `Portfolio` es stub, `InsufficientSharesDomainException` no existe).

- [x] **Step 3: Crear `InsufficientSharesDomainException`**

```csharp
namespace BigSchool.Domain.Exceptions;

/// <summary>
/// Se intentó vender más shares de las disponibles (abiertas) de una Company en la cartera. → HTTP 400.
/// </summary>
public class InsufficientSharesDomainException : DomainException
{
    public InsufficientSharesDomainException(int companyId, decimal requested, decimal available)
        : base("INSUFFICIENT_SHARES",
            $"No hay shares suficientes de la empresa {companyId}: se intentó vender {requested} pero solo hay {available} abiertas.")
    { }
}
```

- [x] **Step 4: Implementar `Portfolio`** (reemplaza el stub vacío completo)

```csharp
using BigSchool.Domain.Enums;
using BigSchool.Domain.Exceptions;
using BigSchool.Domain.ValueObjects;

namespace BigSchool.Domain.Entities;

/// <summary>
/// Cartera de inversión de un usuario (Aggregate Root). Posee Holding (lotes de compra) y, a través de
/// ellos, Disposal (ventas). Mantiene RealizedPnL persistido (plusvalía/minusvalía realizada consolidada
/// en la moneda base del usuario). Las ventas son FIFO a nivel (Portfolio, Company). Toda mutación de
/// hijas/nietas pasa por esta clase: nunca se construyen ni mutan Holding/Disposal desde fuera.
/// </summary>
public class Portfolio : BaseEntity, IAggregateRoot
{
    public int IdPortfolio { get; private set; }
    public int IdUser { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Money RealizedPnL { get; private set; } = null!;
    public EntityStatus IdStatus { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private readonly List<Holding> _holdings = [];
    public IReadOnlyCollection<Holding> Holdings => _holdings.AsReadOnly();

    protected Portfolio() { } // EF Core

    private Portfolio(int userId, string name, Money realizedPnL, EntityStatus idStatus, DateTime createdAt)
    {
        IdUser = userId;
        Name = name;
        RealizedPnL = realizedPnL;
        IdStatus = idStatus;
        CreatedAt = createdAt;
    }

    public static Portfolio Create(int userId, string name, Currency baseCurrency)
    {
        if (userId <= 0)
            throw new ArgumentException("El identificador de usuario es obligatorio.", nameof(userId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre de la cartera es obligatorio.", nameof(name));

        return new Portfolio(userId, name.Trim(), Money.Create(0m, baseCurrency),
            EntityStatus.Active, DateTime.UtcNow);
    }

    /// <summary>Registra una compra como nuevo lote. La capa Application resuelve el rate (Company.Currency → base) a buyDate.</summary>
    public Holding AddHolding(int companyId, decimal shares, Money buyPrice, Currency baseCurrency,
        decimal rate, DateOnly buyDate, DateOnly rateDate, string? notes)
    {
        if (baseCurrency != RealizedPnL.Currency)
            throw new ArgumentException("La moneda base no coincide con la de la cartera.", nameof(baseCurrency));

        var avgBuyPrice = MoneyConversion.Create(buyPrice, baseCurrency, rate, rateDate);
        var holding = Holding.Create(companyId, shares, avgBuyPrice, buyDate, notes);
        _holdings.Add(holding);
        UpdatedAt = DateTime.UtcNow;
        return holding;
    }

    public void UpdateHolding(int holdingId, string? notes)
    {
        var holding = FindActiveHolding(holdingId);
        holding.UpdateNotes(notes);
        UpdatedAt = DateTime.UtcNow;
    }

    public void DeleteHolding(int holdingId)
    {
        var holding = FindActiveHolding(holdingId);
        var reversed = holding.Delete();
        RealizedPnL = Money.Create(RealizedPnL.Amount - reversed, RealizedPnL.Currency);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Vende `shares` de una Company aplicando FIFO: consume los lotes abiertos más antiguos primero
    /// (orden BuyDate asc, desempate IdHolding asc), generando un Disposal por lote tocado. Acumula el
    /// realizado en RealizedPnL en el mismo SaveChanges. La capa Application resuelve el rate a sellDate.
    /// </summary>
    public IReadOnlyList<Disposal> SellShares(int companyId, decimal shares, Money sellPrice,
        Currency baseCurrency, decimal rate, DateOnly sellDate, DateOnly rateDate)
    {
        if (shares <= 0m)
            throw new ArgumentException("Las shares a vender deben ser mayores que cero.", nameof(shares));
        if (baseCurrency != RealizedPnL.Currency)
            throw new ArgumentException("La moneda base no coincide con la de la cartera.", nameof(baseCurrency));

        var openLots = _holdings
            .Where(h => h.IdCompany == companyId && h.IdStatus != EntityStatus.Deleted && h.OpenShares > 0m)
            .OrderBy(h => h.BuyDate).ThenBy(h => h.IdHolding)
            .ToList();

        var available = openLots.Sum(h => h.OpenShares);
        if (shares > available)
            throw new InsufficientSharesDomainException(companyId, shares, available);

        var sellConversion = MoneyConversion.Create(sellPrice, baseCurrency, rate, rateDate);

        var disposals = new List<Disposal>();
        var remaining = shares;
        var realizedTotal = 0m;

        foreach (var lot in openLots)
        {
            if (remaining <= 0m) break;
            var take = Math.Min(remaining, lot.OpenShares);
            var disposal = lot.RecordDisposal(take, sellConversion, sellDate, null);
            disposals.Add(disposal);
            realizedTotal += disposal.RealizedPnL.Amount;
            remaining -= take;
        }

        RealizedPnL = Money.Create(RealizedPnL.Amount + realizedTotal, RealizedPnL.Currency);
        UpdatedAt = DateTime.UtcNow;
        return disposals;
    }

    private Holding FindActiveHolding(int holdingId)
    {
        return _holdings.FirstOrDefault(h => h.IdHolding == holdingId && h.IdStatus != EntityStatus.Deleted)
            ?? throw new NotFoundException(nameof(Holding), holdingId);
    }
}
```

- [x] **Step 5: Ejecutar el test (debe pasar)**

Run: `dotnet test src/backend/tests/BigSchool.Domain.Tests/BigSchool.Domain.Tests.csproj -c Release --filter FullyQualifiedName~PortfolioTests`
Expected: PASS (todos).

- [x] **Step 6: Suite de dominio completa**

Run: `dotnet test src/backend/tests/BigSchool.Domain.Tests/BigSchool.Domain.Tests.csproj -c Release`
Expected: PASS (incluye los tests previos del proyecto sin regresiones).

- [x] **Step 7: Commit**

```bash
git add src/backend/src/BigSchool.Domain/Entities/Portfolio.cs src/backend/src/BigSchool.Domain/Exceptions/InsufficientSharesDomainException.cs src/backend/tests/BigSchool.Domain.Tests/Entities/PortfolioTests.cs
git commit -m "feat: agregado Portfolio (AddHolding, SellShares FIFO, RealizedPnL) + tests del agregado"
```

---

## Task 4: Infrastructure — EF Configurations + DbSets + migración `CreatePortfolios`

**Files:**
- Create: `src/backend/src/BigSchool.Infrastructure/Persistence/Configurations/PortfolioConfiguration.cs`
- Create: `src/backend/src/BigSchool.Infrastructure/Persistence/Configurations/HoldingConfiguration.cs`
- Create: `src/backend/src/BigSchool.Infrastructure/Persistence/Configurations/DisposalConfiguration.cs`
- Modify: `src/backend/src/BigSchool.Infrastructure/Persistence/BigSchoolDbContext.cs`
- Generate: `src/backend/src/BigSchool.Infrastructure/Persistence/Migrations/<timestamp>_CreatePortfolios.cs`

- [x] **Step 1: `PortfolioConfiguration`**

```csharp
using BigSchool.Domain.Entities;
using BigSchool.Domain.Enums;
using BigSchool.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BigSchool.Infrastructure.Persistence.Configurations;

public class PortfolioConfiguration : IEntityTypeConfiguration<Portfolio>
{
    public void Configure(EntityTypeBuilder<Portfolio> builder)
    {
        builder.ToTable("Portfolios");
        builder.HasKey(p => p.IdPortfolio);

        ConfigureProperties(builder);
        ConfigureRelationships(builder);
        ConfigureIndexes(builder);
        ConfigureFilters(builder);

        builder.Ignore(p => p.DomainEvents);
    }

    private static void ConfigureProperties(EntityTypeBuilder<Portfolio> builder)
    {
        builder.Property(p => p.IdPortfolio).ValueGeneratedOnAdd();
        builder.Property(p => p.IdUser).IsRequired();
        builder.Property(p => p.Name).IsRequired().HasMaxLength(100);
        builder.Property(p => p.IdStatus).IsRequired().HasDefaultValue(EntityStatus.Active).HasConversion<short>();
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt);

        // RealizedPnL (Money) en la moneda base del usuario.
        builder.OwnsOne(p => p.RealizedPnL, m =>
        {
            m.Property(x => x.Amount).HasColumnName("RealizedPnL").HasColumnType("decimal(18,2)");
            m.Property(x => x.Currency).HasColumnName("RealizedPnLCurrency")
                .HasConversion(CurrencyConverter.CharIso).HasColumnType("char(3)");
        });
        builder.Navigation(p => p.RealizedPnL).IsRequired();
    }

    private static void ConfigureRelationships(EntityTypeBuilder<Portfolio> builder)
    {
        builder.HasOne<User>().WithMany().HasForeignKey(p => p.IdUser)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Holdings)
            .WithOne()
            .HasForeignKey("IdPortfolio")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Holdings).UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureIndexes(EntityTypeBuilder<Portfolio> builder)
    {
        builder.HasIndex(p => p.IdUser);
    }

    private static void ConfigureFilters(EntityTypeBuilder<Portfolio> builder)
    {
        builder.HasQueryFilter(p => p.IdStatus != EntityStatus.Deleted);
    }
}
```

- [x] **Step 2: `HoldingConfiguration`**

```csharp
using BigSchool.Domain.Entities;
using BigSchool.Domain.Enums;
using BigSchool.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BigSchool.Infrastructure.Persistence.Configurations;

public class HoldingConfiguration : IEntityTypeConfiguration<Holding>
{
    public void Configure(EntityTypeBuilder<Holding> builder)
    {
        builder.ToTable("Holdings");
        builder.HasKey(h => h.IdHolding);

        ConfigureProperties(builder);
        ConfigureRelationships(builder);
        ConfigureIndexes(builder);
        ConfigureFilters(builder);

        builder.Ignore(h => h.DomainEvents);
    }

    private static void ConfigureProperties(EntityTypeBuilder<Holding> builder)
    {
        builder.Property(h => h.IdHolding).ValueGeneratedOnAdd();
        builder.Property(h => h.IdCompany).IsRequired();
        builder.Property(h => h.Shares).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(h => h.BuyDate).HasColumnType("date").IsRequired();
        builder.Property(h => h.Notes).HasMaxLength(500);
        builder.Property(h => h.IdStatus).IsRequired().HasDefaultValue(EntityStatus.Active).HasConversion<short>();
        builder.Property(h => h.CreatedAt).IsRequired();
        builder.Property(h => h.UpdatedAt);

        // Shadow FK al AR (la relación la declara PortfolioConfiguration).
        builder.Property<int>("IdPortfolio");

        // AvgBuyPrice (MoneyConversion) snapshot a BuyDate. Columnas Buy*.
        builder.OwnsOne(h => h.AvgBuyPrice, conv =>
        {
            conv.Property(c => c.Rate).HasColumnName("BuyExchangeRate").HasColumnType("decimal(18,6)");
            conv.Property(c => c.RateDate).HasColumnName("BuyRateDate").HasColumnType("date");

            conv.OwnsOne(c => c.Original, orig =>
            {
                orig.Property(m => m.Amount).HasColumnName("BuyOriginalAmount").HasColumnType("decimal(18,4)");
                orig.Property(m => m.Currency).HasColumnName("BuyOriginalCurrency")
                    .HasConversion(CurrencyConverter.CharIso).HasColumnType("char(3)");
            });
            conv.OwnsOne(c => c.Base, baseMoney =>
            {
                baseMoney.Property(m => m.Amount).HasColumnName("BuyBaseAmount").HasColumnType("decimal(18,2)");
                baseMoney.Property(m => m.Currency).HasColumnName("BuyBaseCurrency")
                    .HasConversion(CurrencyConverter.CharIso).HasColumnType("char(3)");
            });
        });
        builder.Navigation(h => h.AvgBuyPrice).IsRequired();
    }

    private static void ConfigureRelationships(EntityTypeBuilder<Holding> builder)
    {
        // FK al catálogo global Company (no cascada: las empresas no se borran por cartera).
        builder.HasOne<Company>().WithMany().HasForeignKey(h => h.IdCompany)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(h => h.Disposals)
            .WithOne()
            .HasForeignKey("IdHolding")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(h => h.Disposals).UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureIndexes(EntityTypeBuilder<Holding> builder)
    {
        builder.HasIndex("IdPortfolio", nameof(Holding.IdCompany));
    }

    private static void ConfigureFilters(EntityTypeBuilder<Holding> builder)
    {
        builder.HasQueryFilter(h => h.IdStatus != EntityStatus.Deleted);
    }
}
```

- [x] **Step 3: `DisposalConfiguration`**

```csharp
using BigSchool.Domain.Entities;
using BigSchool.Domain.Enums;
using BigSchool.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BigSchool.Infrastructure.Persistence.Configurations;

public class DisposalConfiguration : IEntityTypeConfiguration<Disposal>
{
    public void Configure(EntityTypeBuilder<Disposal> builder)
    {
        builder.ToTable("Disposals");
        builder.HasKey(d => d.IdDisposal);

        ConfigureProperties(builder);
        ConfigureRelationships(builder);
        ConfigureIndexes(builder);
        ConfigureFilters(builder);

        builder.Ignore(d => d.DomainEvents);
    }

    private static void ConfigureProperties(EntityTypeBuilder<Disposal> builder)
    {
        builder.Property(d => d.IdDisposal).ValueGeneratedOnAdd();
        builder.Property(d => d.Shares).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(d => d.SellDate).HasColumnType("date").IsRequired();
        builder.Property(d => d.Notes).HasMaxLength(500);
        builder.Property(d => d.IdStatus).IsRequired().HasDefaultValue(EntityStatus.Active).HasConversion<short>();
        builder.Property(d => d.CreatedAt).IsRequired();
        builder.Property(d => d.UpdatedAt);

        // Shadow FK al lote padre (la relación la declara HoldingConfiguration).
        builder.Property<int>("IdHolding");

        // SellPrice (MoneyConversion) snapshot a SellDate. Columnas Sell*.
        builder.OwnsOne(d => d.SellPrice, conv =>
        {
            conv.Property(c => c.Rate).HasColumnName("SellExchangeRate").HasColumnType("decimal(18,6)");
            conv.Property(c => c.RateDate).HasColumnName("SellRateDate").HasColumnType("date");

            conv.OwnsOne(c => c.Original, orig =>
            {
                orig.Property(m => m.Amount).HasColumnName("SellOriginalAmount").HasColumnType("decimal(18,4)");
                orig.Property(m => m.Currency).HasColumnName("SellOriginalCurrency")
                    .HasConversion(CurrencyConverter.CharIso).HasColumnType("char(3)");
            });
            conv.OwnsOne(c => c.Base, baseMoney =>
            {
                baseMoney.Property(m => m.Amount).HasColumnName("SellBaseAmount").HasColumnType("decimal(18,2)");
                baseMoney.Property(m => m.Currency).HasColumnName("SellBaseCurrency")
                    .HasConversion(CurrencyConverter.CharIso).HasColumnType("char(3)");
            });
        });
        builder.Navigation(d => d.SellPrice).IsRequired();

        // RealizedPnL (Money) en moneda base.
        builder.OwnsOne(d => d.RealizedPnL, m =>
        {
            m.Property(x => x.Amount).HasColumnName("RealizedPnL").HasColumnType("decimal(18,2)");
            m.Property(x => x.Currency).HasColumnName("RealizedPnLCurrency")
                .HasConversion(CurrencyConverter.CharIso).HasColumnType("char(3)");
        });
        builder.Navigation(d => d.RealizedPnL).IsRequired();
    }

    private static void ConfigureRelationships(EntityTypeBuilder<Disposal> builder)
    {
        // La relación Holding→Disposal la declara HoldingConfiguration (HasMany/WithOne/HasForeignKey "IdHolding").
    }

    private static void ConfigureIndexes(EntityTypeBuilder<Disposal> builder)
    {
        builder.HasIndex("IdHolding");
    }

    private static void ConfigureFilters(EntityTypeBuilder<Disposal> builder)
    {
        builder.HasQueryFilter(d => d.IdStatus != EntityStatus.Deleted);
    }
}
```

- [x] **Step 4: Modificar `BigSchoolDbContext`** — añadir DbSets y quitar los TODO

Reemplazar el bloque de DbSets para que quede:

```csharp
    // Aggregate Roots — acceso principal
    public DbSet<User> Users => Set<User>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Portfolio> Portfolios => Set<Portfolio>();

    // Entidades hijas — DbSet necesario para EF Core migrations/queries
    // El acceso de escritura se hace siempre a través del Aggregate Root
    public DbSet<SubCategory> SubCategories => Set<SubCategory>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Valuation> Valuations => Set<Valuation>();
    public DbSet<Holding> Holdings => Set<Holding>();
    public DbSet<Disposal> Disposals => Set<Disposal>();
```

> Eliminar las líneas `// TODO: ... Portfolio no está implementado` y `// TODO: Tasks futuras ... Holding`. Dejar el `// public DbSet<RagDocument>` como está (sigue pendiente).

- [x] **Step 5: Generar la migración**

Run (desde la raíz del repo):
```bash
dotnet ef migrations add CreatePortfolios \
  -p src/backend/src/BigSchool.Infrastructure \
  -s src/backend/src/BigSchool.WebApi \
  -o Persistence/Migrations
```
Expected: crea `<timestamp>_CreatePortfolios.cs` + `.Designer.cs` y actualiza `BigSchoolDbContextModelSnapshot.cs`.

- [x] **Step 6: Revisar la migración generada**

Abrir el `_CreatePortfolios.cs` y verificar que crea las tablas `Portfolios`, `Holdings`, `Disposals` con las columnas owned esperadas (`RealizedPnL`/`RealizedPnLCurrency`; `BuyOriginalAmount`…`BuyRateDate`; `SellOriginalAmount`…`SellRateDate`; `RealizedPnL`/`RealizedPnLCurrency` en Disposals), las FK (`Portfolios.IdUser`→Users, `Holdings.IdPortfolio`→Portfolios, `Holdings.IdCompany`→Companies (Restrict), `Disposals.IdHolding`→Holdings) y los índices. **No** debe contener cambios espurios sobre tablas existentes (Companies/Valuations/Transactions); si los hay, descartar y revisar las configs.

- [x] **Step 7: Build**

Run: `dotnet build src/backend/Backend.sln -c Release`
Expected: Build succeeded.

- [x] **Step 8: Commit**

```bash
git add src/backend/src/BigSchool.Infrastructure/Persistence/Configurations/PortfolioConfiguration.cs src/backend/src/BigSchool.Infrastructure/Persistence/Configurations/HoldingConfiguration.cs src/backend/src/BigSchool.Infrastructure/Persistence/Configurations/DisposalConfiguration.cs src/backend/src/BigSchool.Infrastructure/Persistence/BigSchoolDbContext.cs src/backend/src/BigSchool.Infrastructure/Persistence/Migrations/
git commit -m "feat: EF config (Portfolio/Holding/Disposal), DbSets y migración CreatePortfolios"
```

---

## Task 5: Application/Infrastructure — `IPortfolioRepository` + `PortfolioRepository`

**Files:**
- Modify: `src/backend/src/BigSchool.Application/Interfaces/Repositories/IPortfolioRepository.cs`
- Create: `src/backend/src/BigSchool.Infrastructure/Persistence/Repositories/PortfolioRepository.cs`

> `IPortfolioRepository` ya existe como stub `: IRepository<Portfolio, int> {}`. Se le añaden métodos de carga del grafo completo (Holdings + Disposals) para los comandos que mutan el agregado.

- [x] **Step 1: Ampliar `IPortfolioRepository`**

```csharp
using BigSchool.Domain.Entities;

namespace BigSchool.Application.Interfaces.Repositories;

public interface IPortfolioRepository : IRepository<Portfolio, int>
{
    /// <summary>Carga la cartera con sus Holdings y los Disposals de cada uno (grafo completo del agregado) para operaciones de escritura (AddHolding, SellShares, Update/Delete).</summary>
    Task<Portfolio?> GetByIdWithHoldingsAsync(int id, CancellationToken cancellationToken = default);
}
```

- [x] **Step 2: Implementar `PortfolioRepository`**

```csharp
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BigSchool.Infrastructure.Persistence.Repositories;

public class PortfolioRepository : EFRepository<Portfolio, int>, IPortfolioRepository
{
    public PortfolioRepository(BigSchoolDbContext context) : base(context)
    {
    }

    public async Task<Portfolio?> GetByIdWithHoldingsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await Context.Portfolios
            .Include(p => p.Holdings)
                .ThenInclude(h => h.Disposals)
            .FirstOrDefaultAsync(p => p.IdPortfolio == id, cancellationToken);
    }
}
```

> Autofac lo auto-registra (no hay task de DI). El filtro global de query oculta automáticamente las filas `Deleted` en todos los niveles.

- [x] **Step 3: Build**

Run: `dotnet build src/backend/Backend.sln -c Release`
Expected: Build succeeded.

- [x] **Step 4: Commit**

```bash
git add src/backend/src/BigSchool.Application/Interfaces/Repositories/IPortfolioRepository.cs src/backend/src/BigSchool.Infrastructure/Persistence/Repositories/PortfolioRepository.cs
git commit -m "feat: IPortfolioRepository.GetByIdWithHoldingsAsync + PortfolioRepository"
```

---

## Task 6: Application — `CreatePortfolio` (command + validator + `PortfolioDto`)

**Files:**
- Create: `src/backend/src/BigSchool.Application/DTOs/Investments/PortfolioDto.cs`
- Create: `src/backend/src/BigSchool.Application/Commands/Investments/CreatePortfolio/CreatePortfolioCommand.cs`
- Create: `.../CreatePortfolio/CreatePortfolioCommandHandler.cs`
- Create: `.../CreatePortfolio/CreatePortfolioCommandValidator.cs`
- Test: `src/backend/tests/BigSchool.Application.Tests/Commands/Investments/CreatePortfolioCommandHandlerTests.cs`

- [x] **Step 1: `PortfolioDto`** (DTO de command: Currency tipado)

```csharp
using BigSchool.Domain.Enums;

namespace BigSchool.Application.DTOs.Investments;

/// <summary>DTO de respuesta de comando (construido desde el dominio): Currency tipado.</summary>
public record PortfolioDto(int IdPortfolio, string Name, decimal RealizedPnL, Currency RealizedPnLCurrency);
```

- [x] **Step 2: `CreatePortfolioCommand`**

```csharp
using BigSchool.Application.DTOs.Investments;
using MediatR;

namespace BigSchool.Application.Commands.Investments.CreatePortfolio;

public record CreatePortfolioCommand(int IdUser, string Name) : IRequest<PortfolioDto>;
```

- [x] **Step 3: `CreatePortfolioCommandValidator`**

```csharp
using FluentValidation;

namespace BigSchool.Application.Commands.Investments.CreatePortfolio;

public class CreatePortfolioCommandValidator : AbstractValidator<CreatePortfolioCommand>
{
    public CreatePortfolioCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}
```

- [x] **Step 4: Escribir el test del handler que falla**

```csharp
using BigSchool.Application.Commands.Investments.CreatePortfolio;
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Domain.Entities;
using BigSchool.Domain.Enums;
using BigSchool.Domain.Exceptions;
using BigSchool.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace BigSchool.Application.Tests.Commands.Investments;

public class CreatePortfolioCommandHandlerTests
{
    private readonly Mock<IPortfolioRepository> _portfolios = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly CreatePortfolioCommandHandler _handler;

    public CreatePortfolioCommandHandlerTests()
    {
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<bool>())).ReturnsAsync(1);
        _portfolios.Setup(r => r.UnitOfWork).Returns(uow.Object);
        _handler = new CreatePortfolioCommandHandler(_portfolios.Object, _users.Object);
    }

    [Fact]
    public async Task Handle_CreatesPortfolio_WithRealizedPnLZeroInUserBase()
    {
        var user = User.Create("e2e@test.com", "h", "s", "User", Currency.USD);
        _users.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await _handler.Handle(new CreatePortfolioCommand(7, "Growth"), CancellationToken.None);

        result.Name.Should().Be("Growth");
        result.RealizedPnL.Should().Be(0m);
        result.RealizedPnLCurrency.Should().Be(Currency.USD);
        _portfolios.Verify(r => r.AddAsync(It.IsAny<Portfolio>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UnknownUser_ThrowsNotFound()
    {
        _users.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var act = () => _handler.Handle(new CreatePortfolioCommand(99, "X"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _portfolios.Verify(r => r.AddAsync(It.IsAny<Portfolio>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
```

- [x] **Step 5: Ejecutar el test (debe fallar)**

Run: `dotnet test src/backend/tests/BigSchool.Application.Tests/BigSchool.Application.Tests.csproj -c Release --filter FullyQualifiedName~CreatePortfolioCommandHandlerTests`
Expected: FAIL (handler no existe).

- [x] **Step 6: Implementar `CreatePortfolioCommandHandler`**

```csharp
using BigSchool.Application.DTOs.Investments;
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Domain.Entities;
using BigSchool.Domain.Exceptions;
using MediatR;

namespace BigSchool.Application.Commands.Investments.CreatePortfolio;

public class CreatePortfolioCommandHandler : IRequestHandler<CreatePortfolioCommand, PortfolioDto>
{
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly IUserRepository _userRepository;

    public CreatePortfolioCommandHandler(IPortfolioRepository portfolioRepository, IUserRepository userRepository)
    {
        _portfolioRepository = portfolioRepository;
        _userRepository = userRepository;
    }

    public async Task<PortfolioDto> Handle(CreatePortfolioCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.IdUser, cancellationToken)
            ?? throw new NotFoundException(nameof(User), request.IdUser);

        var portfolio = Portfolio.Create(request.IdUser, request.Name, user.BaseCurrency);
        await _portfolioRepository.AddAsync(portfolio, cancellationToken);
        await _portfolioRepository.UnitOfWork.SaveChangesAsync();

        return new PortfolioDto(portfolio.IdPortfolio, portfolio.Name,
            portfolio.RealizedPnL.Amount, portfolio.RealizedPnL.Currency);
    }
}
```

- [x] **Step 7: Ejecutar el test (debe pasar) + commit**

Run: `dotnet test src/backend/tests/BigSchool.Application.Tests/BigSchool.Application.Tests.csproj -c Release --filter FullyQualifiedName~CreatePortfolioCommandHandlerTests`
Expected: PASS.

```bash
git add src/backend/src/BigSchool.Application/DTOs/Investments/PortfolioDto.cs src/backend/src/BigSchool.Application/Commands/Investments/CreatePortfolio/ src/backend/tests/BigSchool.Application.Tests/Commands/Investments/CreatePortfolioCommandHandlerTests.cs
git commit -m "feat: CreatePortfolio command (handler, validator, PortfolioDto)"
```

---

## Task 7: Application — `AddHolding` (command + validator + `HoldingDto`)

Compra = nuevo lote. El handler resuelve el rate (Company.Currency → base de la cartera) a `BuyDate` y congela `AvgBuyPrice`. La base se toma de `portfolio.RealizedPnL.Currency` (no se carga el usuario).

**Files:**
- Create: `src/backend/src/BigSchool.Application/DTOs/Investments/HoldingDto.cs`
- Create: `src/backend/src/BigSchool.Application/Commands/Investments/AddHolding/AddHoldingCommand.cs`
- Create: `.../AddHolding/AddHoldingCommandHandler.cs`
- Create: `.../AddHolding/AddHoldingCommandValidator.cs`
- Test: `src/backend/tests/BigSchool.Application.Tests/Commands/Investments/AddHoldingCommandHandlerTests.cs`

- [ ] **Step 1: `HoldingDto`**

```csharp
using BigSchool.Domain.Enums;

namespace BigSchool.Application.DTOs.Investments;

/// <summary>DTO de respuesta de comando para un lote (Holding): monedas tipadas, snapshot AvgBuyPrice.</summary>
public record HoldingDto(
    int IdHolding,
    int IdCompany,
    decimal Shares,
    decimal BuyOriginalAmount,
    Currency BuyOriginalCurrency,
    decimal BuyExchangeRate,
    decimal BuyBaseAmount,
    Currency BuyBaseCurrency,
    DateOnly BuyRateDate,
    DateOnly BuyDate,
    string? Notes);
```

- [ ] **Step 2: `AddHoldingCommand`** (`BuyPrice` en la moneda de la empresa; no se pasa moneda)

```csharp
using BigSchool.Application.DTOs.Investments;
using MediatR;

namespace BigSchool.Application.Commands.Investments.AddHolding;

public record AddHoldingCommand(
    int PortfolioId,
    int IdUser,
    int IdCompany,
    decimal Shares,
    decimal BuyPrice,
    DateOnly BuyDate,
    string? Notes) : IRequest<HoldingDto>;
```

- [ ] **Step 3: `AddHoldingCommandValidator`**

```csharp
using FluentValidation;

namespace BigSchool.Application.Commands.Investments.AddHolding;

public class AddHoldingCommandValidator : AbstractValidator<AddHoldingCommand>
{
    public AddHoldingCommandValidator()
    {
        RuleFor(x => x.IdCompany).GreaterThan(0);
        RuleFor(x => x.Shares).GreaterThan(0m);
        RuleFor(x => x.BuyPrice).GreaterThan(0m);
        RuleFor(x => x.BuyDate).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
```

- [ ] **Step 4: Escribir el test del handler que falla**

```csharp
using BigSchool.Application.Commands.Investments.AddHolding;
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Application.Interfaces.Services;
using BigSchool.Domain.Entities;
using BigSchool.Domain.Enums;
using BigSchool.Domain.Exceptions;
using BigSchool.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace BigSchool.Application.Tests.Commands.Investments;

public class AddHoldingCommandHandlerTests
{
    private readonly Mock<IPortfolioRepository> _portfolios = new();
    private readonly Mock<ICompanyRepository> _companies = new();
    private readonly Mock<IExchangeRateProvider> _rates = new();
    private readonly AddHoldingCommandHandler _handler;

    public AddHoldingCommandHandlerTests()
    {
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<bool>())).ReturnsAsync(1);
        _portfolios.Setup(r => r.UnitOfWork).Returns(uow.Object);
        _handler = new AddHoldingCommandHandler(_portfolios.Object, _companies.Object, _rates.Object);
    }

    private static Portfolio PortfolioOf(int userId, Currency baseCcy) => Portfolio.Create(userId, "P", baseCcy);

    [Fact]
    public async Task Handle_UsdCompanyEurBase_FreezesConvertedCostSnapshot()
    {
        var portfolio = PortfolioOf(7, Currency.EUR);
        _portfolios.Setup(r => r.GetByIdWithHoldingsAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(portfolio);
        _companies.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Company.Create("Apple", "AAPL", "Tech", "NASDAQ", Currency.USD));
        _rates.Setup(r => r.GetRateAsync(Currency.USD, Currency.EUR, new DateOnly(2026, 1, 1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0.9m);

        var cmd = new AddHoldingCommand(5, 7, 1, 100m, 195m, new DateOnly(2026, 1, 1), "lote");
        var dto = await _handler.Handle(cmd, CancellationToken.None);

        dto.Shares.Should().Be(100m);
        dto.BuyOriginalAmount.Should().Be(195m);
        dto.BuyOriginalCurrency.Should().Be(Currency.USD);
        dto.BuyExchangeRate.Should().Be(0.9m);
        dto.BuyBaseAmount.Should().Be(175.50m);
        dto.BuyBaseCurrency.Should().Be(Currency.EUR);
        portfolio.Holdings.Should().ContainSingle();
        _portfolios.Verify(r => r.UnitOfWork.SaveChangesAsync(It.IsAny<bool>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EurCompanyEurBase_UsesRateOne_NoProviderCall()
    {
        var portfolio = PortfolioOf(7, Currency.EUR);
        _portfolios.Setup(r => r.GetByIdWithHoldingsAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(portfolio);
        _companies.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Company.Create("Santander", "SAN", "Fin", "BME", Currency.EUR));

        var cmd = new AddHoldingCommand(5, 7, 3, 10m, 4.5m, new DateOnly(2026, 1, 1), null);
        var dto = await _handler.Handle(cmd, CancellationToken.None);

        dto.BuyExchangeRate.Should().Be(1m);
        dto.BuyBaseAmount.Should().Be(4.5m);
        _rates.Verify(r => r.GetRateAsync(It.IsAny<Currency>(), It.IsAny<Currency>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_PortfolioOfAnotherUser_ThrowsNotFound()
    {
        var portfolio = PortfolioOf(7, Currency.EUR);
        _portfolios.Setup(r => r.GetByIdWithHoldingsAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(portfolio);

        var cmd = new AddHoldingCommand(5, 999, 1, 100m, 195m, new DateOnly(2026, 1, 1), null);
        var act = () => _handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_UnknownCompany_ThrowsNotFound()
    {
        var portfolio = PortfolioOf(7, Currency.EUR);
        _portfolios.Setup(r => r.GetByIdWithHoldingsAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(portfolio);
        _companies.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync((Company?)null);

        var cmd = new AddHoldingCommand(5, 7, 404, 100m, 195m, new DateOnly(2026, 1, 1), null);
        var act = () => _handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
```

- [ ] **Step 5: Ejecutar el test (debe fallar)**

Run: `dotnet test src/backend/tests/BigSchool.Application.Tests/BigSchool.Application.Tests.csproj -c Release --filter FullyQualifiedName~AddHoldingCommandHandlerTests`
Expected: FAIL.

- [ ] **Step 6: Implementar `AddHoldingCommandHandler`**

```csharp
using BigSchool.Application.DTOs.Investments;
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Application.Interfaces.Services;
using BigSchool.Domain.Entities;
using BigSchool.Domain.Exceptions;
using BigSchool.Domain.ValueObjects;
using MediatR;

namespace BigSchool.Application.Commands.Investments.AddHolding;

public class AddHoldingCommandHandler : IRequestHandler<AddHoldingCommand, HoldingDto>
{
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IExchangeRateProvider _exchangeRateProvider;

    public AddHoldingCommandHandler(
        IPortfolioRepository portfolioRepository,
        ICompanyRepository companyRepository,
        IExchangeRateProvider exchangeRateProvider)
    {
        _portfolioRepository = portfolioRepository;
        _companyRepository = companyRepository;
        _exchangeRateProvider = exchangeRateProvider;
    }

    public async Task<HoldingDto> Handle(AddHoldingCommand request, CancellationToken cancellationToken)
    {
        var portfolio = await _portfolioRepository.GetByIdWithHoldingsAsync(request.PortfolioId, cancellationToken);
        if (portfolio is null || portfolio.IdUser != request.IdUser)
            throw new NotFoundException(nameof(Portfolio), request.PortfolioId);

        var company = await _companyRepository.GetByIdAsync(request.IdCompany, cancellationToken)
            ?? throw new NotFoundException(nameof(Company), request.IdCompany);

        var baseCurrency = portfolio.RealizedPnL.Currency;
        var rate = company.Currency == baseCurrency
            ? 1m
            : await _exchangeRateProvider.GetRateAsync(company.Currency, baseCurrency, request.BuyDate, cancellationToken);

        var holding = portfolio.AddHolding(
            company.IdCompany,
            request.Shares,
            Money.Create(request.BuyPrice, company.Currency),
            baseCurrency,
            rate,
            request.BuyDate,
            request.BuyDate,
            request.Notes);

        await _portfolioRepository.UnitOfWork.SaveChangesAsync();

        var c = holding.AvgBuyPrice;
        return new HoldingDto(holding.IdHolding, holding.IdCompany, holding.Shares,
            c.Original.Amount, c.Original.Currency, c.Rate, c.Base.Amount, c.Base.Currency, c.RateDate,
            holding.BuyDate, holding.Notes);
    }
}
```

- [ ] **Step 7: Ejecutar el test (debe pasar) + commit**

Run: `dotnet test src/backend/tests/BigSchool.Application.Tests/BigSchool.Application.Tests.csproj -c Release --filter FullyQualifiedName~AddHoldingCommandHandlerTests`
Expected: PASS.

```bash
git add src/backend/src/BigSchool.Application/DTOs/Investments/HoldingDto.cs src/backend/src/BigSchool.Application/Commands/Investments/AddHolding/ src/backend/tests/BigSchool.Application.Tests/Commands/Investments/AddHoldingCommandHandlerTests.cs
git commit -m "feat: AddHolding command (compra=lote, snapshot AvgBuyPrice multimoneda)"
```

---

## Task 8: Application — `UpdateHolding` + `DeleteHolding`

**Files:**
- Create: `src/backend/src/BigSchool.Application/Commands/Investments/UpdateHolding/{UpdateHoldingCommand,UpdateHoldingCommandHandler,UpdateHoldingCommandValidator}.cs`
- Create: `src/backend/src/BigSchool.Application/Commands/Investments/DeleteHolding/{DeleteHoldingCommand,DeleteHoldingCommandHandler}.cs`
- Test: `src/backend/tests/BigSchool.Application.Tests/Commands/Investments/UpdateHoldingCommandHandlerTests.cs`
- Test: `src/backend/tests/BigSchool.Application.Tests/Commands/Investments/DeleteHoldingCommandHandlerTests.cs`

- [ ] **Step 1: `UpdateHoldingCommand` + validator**

```csharp
// UpdateHoldingCommand.cs
using BigSchool.Application.DTOs.Investments;
using MediatR;

namespace BigSchool.Application.Commands.Investments.UpdateHolding;

public record UpdateHoldingCommand(int PortfolioId, int IdUser, int HoldingId, string? Notes)
    : IRequest<HoldingDto>;
```

```csharp
// UpdateHoldingCommandValidator.cs
using FluentValidation;

namespace BigSchool.Application.Commands.Investments.UpdateHolding;

public class UpdateHoldingCommandValidator : AbstractValidator<UpdateHoldingCommand>
{
    public UpdateHoldingCommandValidator()
    {
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
```

- [ ] **Step 2: `DeleteHoldingCommand`**

```csharp
// DeleteHoldingCommand.cs
using MediatR;

namespace BigSchool.Application.Commands.Investments.DeleteHolding;

public record DeleteHoldingCommand(int PortfolioId, int IdUser, int HoldingId) : IRequest;
```

- [ ] **Step 3: Escribir los tests que fallan**

```csharp
// UpdateHoldingCommandHandlerTests.cs
using BigSchool.Application.Commands.Investments.UpdateHolding;
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Domain.Entities;
using BigSchool.Domain.Enums;
using BigSchool.Domain.Exceptions;
using BigSchool.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace BigSchool.Application.Tests.Commands.Investments;

public class UpdateHoldingCommandHandlerTests
{
    private readonly Mock<IPortfolioRepository> _portfolios = new();
    private readonly UpdateHoldingCommandHandler _handler;

    public UpdateHoldingCommandHandlerTests()
    {
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<bool>())).ReturnsAsync(1);
        _portfolios.Setup(r => r.UnitOfWork).Returns(uow.Object);
        _handler = new UpdateHoldingCommandHandler(_portfolios.Object);
    }

    [Fact]
    public async Task Handle_UpdatesNotes()
    {
        var portfolio = Portfolio.Create(7, "P", Currency.EUR);
        var holding = portfolio.AddHolding(3, 10m, BigSchool.Domain.ValueObjects.Money.Create(4.5m, Currency.EUR),
            Currency.EUR, 1m, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), "old");
        _portfolios.Setup(r => r.GetByIdWithHoldingsAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(portfolio);

        var dto = await _handler.Handle(new UpdateHoldingCommand(5, 7, holding.IdHolding, "new"), CancellationToken.None);

        dto.Notes.Should().Be("new");
    }

    [Fact]
    public async Task Handle_ForeignPortfolio_ThrowsNotFound()
    {
        var portfolio = Portfolio.Create(7, "P", Currency.EUR);
        _portfolios.Setup(r => r.GetByIdWithHoldingsAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(portfolio);

        var act = () => _handler.Handle(new UpdateHoldingCommand(5, 999, 1, "x"), CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
```

```csharp
// DeleteHoldingCommandHandlerTests.cs
using BigSchool.Application.Commands.Investments.DeleteHolding;
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Domain.Entities;
using BigSchool.Domain.Enums;
using BigSchool.Domain.Exceptions;
using BigSchool.Domain.Interfaces;
using BigSchool.Domain.ValueObjects;
using FluentAssertions;
using Moq;
using Xunit;

namespace BigSchool.Application.Tests.Commands.Investments;

public class DeleteHoldingCommandHandlerTests
{
    private readonly Mock<IPortfolioRepository> _portfolios = new();
    private readonly DeleteHoldingCommandHandler _handler;

    public DeleteHoldingCommandHandlerTests()
    {
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<bool>())).ReturnsAsync(1);
        _portfolios.Setup(r => r.UnitOfWork).Returns(uow.Object);
        _handler = new DeleteHoldingCommandHandler(_portfolios.Object);
    }

    [Fact]
    public async Task Handle_SoftDeletesHolding_AndReversesRealized()
    {
        var portfolio = Portfolio.Create(7, "P", Currency.EUR);
        var holding = portfolio.AddHolding(3, 100m, Money.Create(100m, Currency.EUR), Currency.EUR, 1m,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), null);
        portfolio.SellShares(3, 40m, Money.Create(150m, Currency.EUR), Currency.EUR, 1m,
            new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 2));
        _portfolios.Setup(r => r.GetByIdWithHoldingsAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(portfolio);

        await _handler.Handle(new DeleteHoldingCommand(5, 7, holding.IdHolding), CancellationToken.None);

        portfolio.Holdings.Single().IdStatus.Should().Be(EntityStatus.Deleted);
        portfolio.RealizedPnL.Amount.Should().Be(0m);
    }

    [Fact]
    public async Task Handle_ForeignPortfolio_ThrowsNotFound()
    {
        var portfolio = Portfolio.Create(7, "P", Currency.EUR);
        _portfolios.Setup(r => r.GetByIdWithHoldingsAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(portfolio);

        var act = () => _handler.Handle(new DeleteHoldingCommand(5, 999, 1), CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
```

- [ ] **Step 4: Ejecutar (deben fallar)**

Run: `dotnet test src/backend/tests/BigSchool.Application.Tests/BigSchool.Application.Tests.csproj -c Release --filter "FullyQualifiedName~UpdateHoldingCommandHandlerTests|FullyQualifiedName~DeleteHoldingCommandHandlerTests"`
Expected: FAIL.

- [ ] **Step 5: Implementar los handlers**

```csharp
// UpdateHoldingCommandHandler.cs
using BigSchool.Application.DTOs.Investments;
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Domain.Entities;
using BigSchool.Domain.Exceptions;
using MediatR;

namespace BigSchool.Application.Commands.Investments.UpdateHolding;

public class UpdateHoldingCommandHandler : IRequestHandler<UpdateHoldingCommand, HoldingDto>
{
    private readonly IPortfolioRepository _portfolioRepository;

    public UpdateHoldingCommandHandler(IPortfolioRepository portfolioRepository)
        => _portfolioRepository = portfolioRepository;

    public async Task<HoldingDto> Handle(UpdateHoldingCommand request, CancellationToken cancellationToken)
    {
        var portfolio = await _portfolioRepository.GetByIdWithHoldingsAsync(request.PortfolioId, cancellationToken);
        if (portfolio is null || portfolio.IdUser != request.IdUser)
            throw new NotFoundException(nameof(Portfolio), request.PortfolioId);

        portfolio.UpdateHolding(request.HoldingId, request.Notes); // lanza NotFoundException si el lote no existe
        await _portfolioRepository.UnitOfWork.SaveChangesAsync();

        var holding = portfolio.Holdings.First(h => h.IdHolding == request.HoldingId);
        var c = holding.AvgBuyPrice;
        return new HoldingDto(holding.IdHolding, holding.IdCompany, holding.Shares,
            c.Original.Amount, c.Original.Currency, c.Rate, c.Base.Amount, c.Base.Currency, c.RateDate,
            holding.BuyDate, holding.Notes);
    }
}
```

```csharp
// DeleteHoldingCommandHandler.cs
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Domain.Entities;
using BigSchool.Domain.Exceptions;
using MediatR;

namespace BigSchool.Application.Commands.Investments.DeleteHolding;

public class DeleteHoldingCommandHandler : IRequestHandler<DeleteHoldingCommand>
{
    private readonly IPortfolioRepository _portfolioRepository;

    public DeleteHoldingCommandHandler(IPortfolioRepository portfolioRepository)
        => _portfolioRepository = portfolioRepository;

    public async Task Handle(DeleteHoldingCommand request, CancellationToken cancellationToken)
    {
        var portfolio = await _portfolioRepository.GetByIdWithHoldingsAsync(request.PortfolioId, cancellationToken);
        if (portfolio is null || portfolio.IdUser != request.IdUser)
            throw new NotFoundException(nameof(Portfolio), request.PortfolioId);

        portfolio.DeleteHolding(request.HoldingId); // lanza NotFoundException si el lote no existe
        await _portfolioRepository.UnitOfWork.SaveChangesAsync();
    }
}
```

- [ ] **Step 6: Ejecutar (deben pasar) + commit**

Run: `dotnet test src/backend/tests/BigSchool.Application.Tests/BigSchool.Application.Tests.csproj -c Release --filter "FullyQualifiedName~UpdateHoldingCommandHandlerTests|FullyQualifiedName~DeleteHoldingCommandHandlerTests"`
Expected: PASS.

```bash
git add src/backend/src/BigSchool.Application/Commands/Investments/UpdateHolding/ src/backend/src/BigSchool.Application/Commands/Investments/DeleteHolding/ src/backend/tests/BigSchool.Application.Tests/Commands/Investments/UpdateHoldingCommandHandlerTests.cs src/backend/tests/BigSchool.Application.Tests/Commands/Investments/DeleteHoldingCommandHandlerTests.cs
git commit -m "feat: UpdateHolding y DeleteHolding commands (ownership 404, soft-delete + reversa de realizado)"
```

---

## Task 9: Application — `SellShares` (FIFO) + `DisposalDto` + `SellSharesResultDto`

Venta a nivel (Portfolio, Company) con FIFO. Genera N `Disposal` y actualiza `Portfolio.RealizedPnL` en el mismo `SaveChanges`.

**Files:**
- Create: `src/backend/src/BigSchool.Application/DTOs/Investments/DisposalDto.cs`
- Create: `src/backend/src/BigSchool.Application/DTOs/Investments/SellSharesResultDto.cs`
- Create: `src/backend/src/BigSchool.Application/Commands/Investments/SellShares/{SellSharesCommand,SellSharesCommandHandler,SellSharesCommandValidator}.cs`
- Test: `src/backend/tests/BigSchool.Application.Tests/Commands/Investments/SellSharesCommandHandlerTests.cs`

- [ ] **Step 1: DTOs**

```csharp
// DisposalDto.cs
using BigSchool.Domain.Enums;

namespace BigSchool.Application.DTOs.Investments;

public record DisposalDto(
    int IdDisposal,
    int IdHolding,
    decimal Shares,
    decimal SellOriginalAmount,
    Currency SellOriginalCurrency,
    decimal SellExchangeRate,
    decimal SellBaseAmount,
    Currency SellBaseCurrency,
    DateOnly SellRateDate,
    DateOnly SellDate,
    decimal RealizedPnL,
    Currency RealizedPnLCurrency,
    string? Notes);
```

```csharp
// SellSharesResultDto.cs
using BigSchool.Domain.Enums;

namespace BigSchool.Application.DTOs.Investments;

/// <summary>Resultado de una venta FIFO: los Disposal generados (uno por lote tocado) y el realizado consolidado de la cartera.</summary>
public record SellSharesResultDto(
    IReadOnlyList<DisposalDto> Disposals,
    decimal PortfolioRealizedPnL,
    Currency RealizedPnLCurrency);
```

- [ ] **Step 2: `SellSharesCommand` + validator**

```csharp
// SellSharesCommand.cs
using BigSchool.Application.DTOs.Investments;
using MediatR;

namespace BigSchool.Application.Commands.Investments.SellShares;

public record SellSharesCommand(
    int PortfolioId,
    int IdUser,
    int IdCompany,
    decimal Shares,
    decimal SellPrice,
    DateOnly SellDate,
    string? Notes) : IRequest<SellSharesResultDto>;
```

```csharp
// SellSharesCommandValidator.cs
using FluentValidation;

namespace BigSchool.Application.Commands.Investments.SellShares;

public class SellSharesCommandValidator : AbstractValidator<SellSharesCommand>
{
    public SellSharesCommandValidator()
    {
        RuleFor(x => x.IdCompany).GreaterThan(0);
        RuleFor(x => x.Shares).GreaterThan(0m);
        RuleFor(x => x.SellPrice).GreaterThan(0m);
        RuleFor(x => x.SellDate).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
```

- [ ] **Step 3: Escribir el test que falla**

```csharp
using BigSchool.Application.Commands.Investments.SellShares;
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Application.Interfaces.Services;
using BigSchool.Domain.Entities;
using BigSchool.Domain.Enums;
using BigSchool.Domain.Exceptions;
using BigSchool.Domain.Interfaces;
using BigSchool.Domain.ValueObjects;
using FluentAssertions;
using Moq;
using Xunit;

namespace BigSchool.Application.Tests.Commands.Investments;

public class SellSharesCommandHandlerTests
{
    private readonly Mock<IPortfolioRepository> _portfolios = new();
    private readonly Mock<ICompanyRepository> _companies = new();
    private readonly Mock<IExchangeRateProvider> _rates = new();
    private readonly SellSharesCommandHandler _handler;

    public SellSharesCommandHandlerTests()
    {
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<bool>())).ReturnsAsync(1);
        _portfolios.Setup(r => r.UnitOfWork).Returns(uow.Object);
        _handler = new SellSharesCommandHandler(_portfolios.Object, _companies.Object, _rates.Object);
    }

    // Cartera EUR con dos lotes de SAN (EUR, rate 1) para FIFO determinista sin proveedor.
    private Portfolio TwoLotPortfolioSan()
    {
        var p = Portfolio.Create(7, "P", Currency.EUR);
        p.AddHolding(3, 100m, Money.Create(100m, Currency.EUR), Currency.EUR, 1m, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), "A");
        p.AddHolding(3, 100m, Money.Create(200m, Currency.EUR), Currency.EUR, 1m, new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 1), "B");
        return p;
    }

    [Fact]
    public async Task Handle_FifoAcrossLots_GeneratesNDisposals_AndConsolidatesRealized()
    {
        var portfolio = TwoLotPortfolioSan();
        _portfolios.Setup(r => r.GetByIdWithHoldingsAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(portfolio);
        _companies.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Company.Create("Santander", "SAN", "Fin", "BME", Currency.EUR));

        var cmd = new SellSharesCommand(5, 7, 3, 150m, 250m, new DateOnly(2026, 3, 2), null);
        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.Disposals.Should().HaveCount(2);
        result.Disposals[0].Shares.Should().Be(100m);
        result.Disposals[0].RealizedPnL.Should().Be(15000m); // (250-100)*100
        result.Disposals[1].Shares.Should().Be(50m);
        result.Disposals[1].RealizedPnL.Should().Be(2500m);  // (250-200)*50
        result.PortfolioRealizedPnL.Should().Be(17500m);
        result.RealizedPnLCurrency.Should().Be(Currency.EUR);
        _rates.Verify(r => r.GetRateAsync(It.IsAny<Currency>(), It.IsAny<Currency>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UsdCompany_ResolvesRateAtSellDate()
    {
        var portfolio = Portfolio.Create(7, "P", Currency.EUR);
        portfolio.AddHolding(1, 10m, Money.Create(100m, Currency.USD), Currency.EUR, 0.9m, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), null);
        _portfolios.Setup(r => r.GetByIdWithHoldingsAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(portfolio);
        _companies.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Company.Create("Apple", "AAPL", "Tech", "NASDAQ", Currency.USD));
        _rates.Setup(r => r.GetRateAsync(Currency.USD, Currency.EUR, new DateOnly(2026, 3, 2), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1.0m);

        var cmd = new SellSharesCommand(5, 7, 1, 10m, 100m, new DateOnly(2026, 3, 2), null);
        var result = await _handler.Handle(cmd, CancellationToken.None);

        // coste base 90/u (100*0.9); venta base 100/u (100*1.0) → realizado (100-90)*10 = 100
        result.PortfolioRealizedPnL.Should().Be(100m);
        result.Disposals[0].SellExchangeRate.Should().Be(1.0m);
    }

    [Fact]
    public async Task Handle_InsufficientShares_ThrowsDomainException()
    {
        var portfolio = TwoLotPortfolioSan(); // 200 abiertas
        _portfolios.Setup(r => r.GetByIdWithHoldingsAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(portfolio);
        _companies.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Company.Create("Santander", "SAN", "Fin", "BME", Currency.EUR));

        var cmd = new SellSharesCommand(5, 7, 3, 201m, 250m, new DateOnly(2026, 3, 2), null);
        var act = () => _handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<InsufficientSharesDomainException>();
    }

    [Fact]
    public async Task Handle_ForeignPortfolio_ThrowsNotFound()
    {
        var portfolio = TwoLotPortfolioSan();
        _portfolios.Setup(r => r.GetByIdWithHoldingsAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(portfolio);

        var cmd = new SellSharesCommand(5, 999, 3, 10m, 250m, new DateOnly(2026, 3, 2), null);
        var act = () => _handler.Handle(cmd, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
```

- [ ] **Step 4: Ejecutar (debe fallar)**

Run: `dotnet test src/backend/tests/BigSchool.Application.Tests/BigSchool.Application.Tests.csproj -c Release --filter FullyQualifiedName~SellSharesCommandHandlerTests`
Expected: FAIL.

- [ ] **Step 5: Implementar `SellSharesCommandHandler`**

```csharp
using BigSchool.Application.DTOs.Investments;
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Application.Interfaces.Services;
using BigSchool.Domain.Entities;
using BigSchool.Domain.Exceptions;
using BigSchool.Domain.ValueObjects;
using MediatR;

namespace BigSchool.Application.Commands.Investments.SellShares;

public class SellSharesCommandHandler : IRequestHandler<SellSharesCommand, SellSharesResultDto>
{
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IExchangeRateProvider _exchangeRateProvider;

    public SellSharesCommandHandler(
        IPortfolioRepository portfolioRepository,
        ICompanyRepository companyRepository,
        IExchangeRateProvider exchangeRateProvider)
    {
        _portfolioRepository = portfolioRepository;
        _companyRepository = companyRepository;
        _exchangeRateProvider = exchangeRateProvider;
    }

    public async Task<SellSharesResultDto> Handle(SellSharesCommand request, CancellationToken cancellationToken)
    {
        var portfolio = await _portfolioRepository.GetByIdWithHoldingsAsync(request.PortfolioId, cancellationToken);
        if (portfolio is null || portfolio.IdUser != request.IdUser)
            throw new NotFoundException(nameof(Portfolio), request.PortfolioId);

        var company = await _companyRepository.GetByIdAsync(request.IdCompany, cancellationToken)
            ?? throw new NotFoundException(nameof(Company), request.IdCompany);

        var baseCurrency = portfolio.RealizedPnL.Currency;
        var rate = company.Currency == baseCurrency
            ? 1m
            : await _exchangeRateProvider.GetRateAsync(company.Currency, baseCurrency, request.SellDate, cancellationToken);

        var created = portfolio.SellShares(
            company.IdCompany,
            request.Shares,
            Money.Create(request.SellPrice, company.Currency),
            baseCurrency,
            rate,
            request.SellDate,
            request.SellDate);

        await _portfolioRepository.UnitOfWork.SaveChangesAsync();

        var disposalDtos = created
            .Select(d =>
            {
                var holding = portfolio.Holdings.First(h => h.Disposals.Contains(d));
                var s = d.SellPrice;
                return new DisposalDto(
                    d.IdDisposal, holding.IdHolding, d.Shares,
                    s.Original.Amount, s.Original.Currency, s.Rate, s.Base.Amount, s.Base.Currency, s.RateDate,
                    d.SellDate, d.RealizedPnL.Amount, d.RealizedPnL.Currency, d.Notes);
            })
            .ToList();

        return new SellSharesResultDto(disposalDtos, portfolio.RealizedPnL.Amount, portfolio.RealizedPnL.Currency);
    }
}
```

> Notas sobre `Notes`: la venta puede generar varios `Disposal`; el dominio no propaga `request.Notes` a cada uno (los crea con `null`) para no ensuciar el historial. Si en el futuro se quiere anotar la venta, se añadiría al dominio. El test no comprueba `Notes` por lote.

- [ ] **Step 6: Ejecutar (debe pasar) + commit**

Run: `dotnet test src/backend/tests/BigSchool.Application.Tests/BigSchool.Application.Tests.csproj -c Release --filter FullyQualifiedName~SellSharesCommandHandlerTests`
Expected: PASS.

```bash
git add src/backend/src/BigSchool.Application/DTOs/Investments/DisposalDto.cs src/backend/src/BigSchool.Application/DTOs/Investments/SellSharesResultDto.cs src/backend/src/BigSchool.Application/Commands/Investments/SellShares/ src/backend/tests/BigSchool.Application.Tests/Commands/Investments/SellSharesCommandHandlerTests.cs
git commit -m "feat: SellShares command (FIFO multi-lote, N Disposal, RealizedPnL consolidado)"
```

---

## Task 10: Application — Queries Dapper `GetPortfolios` + `GetPortfolioById`

Consolidan en la moneda base del usuario. La valoración de mercado usa la **última** `Valuation` de cada empresa convertida al tipo de **su fecha**, con **fallback al último tipo conocido ≤ esa fecha** (los tipos seed están a 2026-01-01, las valoraciones a 2026-01-02/03-02 → el fallback es imprescindible).

**Files:**
- Create: `src/backend/src/BigSchool.Application/Queries/Investments/PortfolioSqlFragments.cs` (fragmento SQL compartido)
- Create: `src/backend/src/BigSchool.Application/DTOs/Investments/PortfolioListItemDto.cs`
- Create: `src/backend/src/BigSchool.Application/DTOs/Investments/HoldingListItemDto.cs`
- Create: `src/backend/src/BigSchool.Application/DTOs/Investments/PortfolioDetailDto.cs`
- Create: `src/backend/src/BigSchool.Application/Queries/Investments/GetPortfolios/{GetPortfoliosQuery,GetPortfoliosQueryHandler}.cs`
- Create: `src/backend/src/BigSchool.Application/Queries/Investments/GetPortfolioById/{GetPortfolioByIdQuery,GetPortfolioByIdQueryHandler}.cs`

> Estas queries se verifican en E2E (Tasks 14-15) contra MySQL real; aquí no hay unit test (Dapper requiere BD).

- [ ] **Step 1: Fragmento SQL compartido `PortfolioSqlFragments`**

```csharp
namespace BigSchool.Application.Queries.Investments;

/// <summary>
/// Fragmentos SQL compartidos por las queries de cartera. HOLDING_VALUATION calcula, por cada lote,
/// las shares abiertas, el coste base, la última valoración convertida a la moneda base (con fallback
/// al último tipo ≤ fecha de la valoración) y el P/L no realizado. Parámetros requeridos por el
/// consumidor: @StatusDeleted (EntityStatus.Deleted) y @BaseCurrency (string ISO de la base del usuario).
/// </summary>
internal static class PortfolioSqlFragments
{
    public const string HOLDING_VALUATION = @"
SELECT
    x.IdHolding, x.IdPortfolio, x.IdCompany, x.Ticker, x.CompanyCurrency,
    x.Shares, x.OpenShares,
    x.BuyOriginalAmount, x.BuyOriginalCurrency, x.BuyExchangeRate,
    x.BuyBaseAmount, x.BuyBaseCurrency, x.BuyRateDate, x.BuyDate, x.Notes,
    x.LastPrice, x.LastDate, x.Rate,
    ROUND(x.OpenShares * COALESCE(x.LastPrice, 0) * x.Rate, 2) AS MarketValue,
    ROUND(x.OpenShares * x.BuyBaseAmount, 2) AS CostBasis,
    ROUND(x.OpenShares * COALESCE(x.LastPrice, 0) * x.Rate - x.OpenShares * x.BuyBaseAmount, 2) AS UnrealizedPnL
FROM (
    SELECT
        h.IdHolding, h.IdPortfolio, h.IdCompany, co.Ticker, co.Currency AS CompanyCurrency,
        h.Shares,
        (h.Shares - COALESCE((SELECT SUM(d.Shares) FROM Disposals d
                              WHERE d.IdHolding = h.IdHolding AND d.IdStatus <> @StatusDeleted), 0)) AS OpenShares,
        h.BuyOriginalAmount, h.BuyOriginalCurrency, h.BuyExchangeRate,
        h.BuyBaseAmount, h.BuyBaseCurrency, h.BuyRateDate, h.BuyDate, h.Notes,
        (SELECT v.Price FROM Valuations v WHERE v.IdCompany = h.IdCompany AND v.IdStatus <> @StatusDeleted
         ORDER BY v.Date DESC, v.IdValuation DESC LIMIT 1) AS LastPrice,
        (SELECT v.Date FROM Valuations v WHERE v.IdCompany = h.IdCompany AND v.IdStatus <> @StatusDeleted
         ORDER BY v.Date DESC, v.IdValuation DESC LIMIT 1) AS LastDate,
        CASE WHEN co.Currency = @BaseCurrency THEN 1
             ELSE COALESCE((SELECT er.Rate FROM ExchangeRates er
                            WHERE er.FromCurrency = co.Currency AND er.ToCurrency = @BaseCurrency
                              AND er.RateDate <= (SELECT v.Date FROM Valuations v
                                                  WHERE v.IdCompany = h.IdCompany AND v.IdStatus <> @StatusDeleted
                                                  ORDER BY v.Date DESC, v.IdValuation DESC LIMIT 1)
                            ORDER BY er.RateDate DESC LIMIT 1), 0)
        END AS Rate
    FROM Holdings h
    JOIN Companies co ON co.IdCompany = h.IdCompany
    WHERE h.IdStatus <> @StatusDeleted
) x";
}
```

- [ ] **Step 2: DTOs de query**

```csharp
// PortfolioListItemDto.cs
namespace BigSchool.Application.DTOs.Investments;

public record PortfolioListItemDto(
    int IdPortfolio, string Name,
    decimal RealizedPnL, string RealizedPnLCurrency,
    decimal MarketValue, decimal CostBasis, decimal UnrealizedPnL, decimal TotalPnL);
```

```csharp
// HoldingListItemDto.cs
namespace BigSchool.Application.DTOs.Investments;

public record HoldingListItemDto(
    int IdHolding, int IdCompany, string Ticker, string CompanyCurrency,
    decimal Shares, decimal OpenShares,
    decimal BuyOriginalAmount, string BuyOriginalCurrency, decimal BuyExchangeRate,
    decimal BuyBaseAmount, string BuyBaseCurrency, DateOnly BuyRateDate, DateOnly BuyDate, string? Notes,
    decimal MarketValue, decimal CostBasis, decimal UnrealizedPnL);
```

```csharp
// PortfolioDetailDto.cs
namespace BigSchool.Application.DTOs.Investments;

public record PortfolioDetailDto(
    int IdPortfolio, string Name,
    decimal RealizedPnL, string RealizedPnLCurrency,
    IReadOnlyList<HoldingListItemDto> Holdings);
```

- [ ] **Step 3: `GetPortfolios` (query + handler)**

```csharp
// GetPortfoliosQuery.cs
using BigSchool.Application.Common;
using BigSchool.Application.DTOs.Investments;
using MediatR;

namespace BigSchool.Application.Queries.Investments.GetPortfolios;

public record GetPortfoliosQuery(int IdUser) : IRequest<IReadOnlyList<PortfolioListItemDto>>;
```

```csharp
// GetPortfoliosQueryHandler.cs
using BigSchool.Application.DTOs.Investments;
using BigSchool.Application.Interfaces;
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Domain.Enums;
using Dapper;
using MediatR;

namespace BigSchool.Application.Queries.Investments.GetPortfolios;

public class GetPortfoliosQueryHandler : IRequestHandler<GetPortfoliosQuery, IReadOnlyList<PortfolioListItemDto>>
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly IUserRepository _userRepository;

    public GetPortfoliosQueryHandler(IDbConnectionFactory dbFactory, IUserRepository userRepository)
    {
        _dbFactory = dbFactory;
        _userRepository = userRepository;
    }

    private const string GETPORTFOLIOS_QUERY = @"
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
ORDER BY p.IdPortfolio;";

    public async Task<IReadOnlyList<PortfolioListItemDto>> Handle(GetPortfoliosQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.IdUser, cancellationToken);
        var baseCurrency = (user?.BaseCurrency ?? Currency.EUR).ToString();

        var parameters = new DynamicParameters();
        parameters.Add("@IdUser", request.IdUser);
        parameters.Add("@StatusDeleted", EntityStatus.Deleted);
        parameters.Add("@BaseCurrency", baseCurrency);

        using var conn = _dbFactory.CreateConnection();
        var rows = await conn.QueryAsync<PortfolioListItemDto>(GETPORTFOLIOS_QUERY, parameters);
        return rows.ToList();
    }
}
```

- [ ] **Step 4: `GetPortfolioById` (query + handler)**

```csharp
// GetPortfolioByIdQuery.cs
using BigSchool.Application.DTOs.Investments;
using MediatR;

namespace BigSchool.Application.Queries.Investments.GetPortfolioById;

public record GetPortfolioByIdQuery(int IdPortfolio, int IdUser) : IRequest<PortfolioDetailDto?>;
```

```csharp
// GetPortfolioByIdQueryHandler.cs
using BigSchool.Application.DTOs.Investments;
using BigSchool.Application.Interfaces;
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Application.Queries.Investments;
using BigSchool.Domain.Enums;
using Dapper;
using MediatR;

namespace BigSchool.Application.Queries.Investments.GetPortfolioById;

public class GetPortfolioByIdQueryHandler : IRequestHandler<GetPortfolioByIdQuery, PortfolioDetailDto?>
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly IUserRepository _userRepository;

    public GetPortfolioByIdQueryHandler(IDbConnectionFactory dbFactory, IUserRepository userRepository)
    {
        _dbFactory = dbFactory;
        _userRepository = userRepository;
    }

    private const string HEADER_QUERY = @"SELECT IdPortfolio, Name, RealizedPnL, RealizedPnLCurrency
                                          FROM Portfolios
                                          WHERE IdPortfolio = @IdPortfolio AND IdUser = @IdUser AND IdStatus <> @StatusDeleted
                                          LIMIT 1;";

    private const string HOLDINGS_QUERY = @"
SELECT hv.IdHolding, hv.IdCompany, hv.Ticker, hv.CompanyCurrency,
       hv.Shares, hv.OpenShares,
       hv.BuyOriginalAmount, hv.BuyOriginalCurrency, hv.BuyExchangeRate,
       hv.BuyBaseAmount, hv.BuyBaseCurrency, hv.BuyRateDate, hv.BuyDate, hv.Notes,
       hv.MarketValue, hv.CostBasis, hv.UnrealizedPnL
FROM (" + PortfolioSqlFragments.HOLDING_VALUATION + @") hv
WHERE hv.IdPortfolio = @IdPortfolio
ORDER BY hv.BuyDate, hv.IdHolding;";

    public async Task<PortfolioDetailDto?> Handle(GetPortfolioByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.IdUser, cancellationToken);
        var baseCurrency = (user?.BaseCurrency ?? Currency.EUR).ToString();

        var parameters = new DynamicParameters();
        parameters.Add("@IdPortfolio", request.IdPortfolio);
        parameters.Add("@IdUser", request.IdUser);
        parameters.Add("@StatusDeleted", EntityStatus.Deleted);
        parameters.Add("@BaseCurrency", baseCurrency);

        using var conn = _dbFactory.CreateConnection();

        var header = await conn.QuerySingleOrDefaultAsync(HEADER_QUERY, parameters);
        if (header is null) return null; // no existe o no es del usuario → el controller devuelve 404

        var holdings = (await conn.QueryAsync<HoldingListItemDto>(HOLDINGS_QUERY, parameters)).ToList();

        return new PortfolioDetailDto(
            (int)header.IdPortfolio, (string)header.Name,
            (decimal)header.RealizedPnL, (string)header.RealizedPnLCurrency,
            holdings);
    }
}
```

> `QuerySingleOrDefaultAsync` sin tipo devuelve `dynamic` (una fila); el casteo explícito de columnas es el patrón ya usado en `PostTransactionTests`. Alternativa: un DTO header dedicado — opcional.

- [ ] **Step 5: Build + commit**

Run: `dotnet build src/backend/Backend.sln -c Release`
Expected: Build succeeded.

```bash
git add src/backend/src/BigSchool.Application/Queries/Investments/PortfolioSqlFragments.cs src/backend/src/BigSchool.Application/DTOs/Investments/PortfolioListItemDto.cs src/backend/src/BigSchool.Application/DTOs/Investments/HoldingListItemDto.cs src/backend/src/BigSchool.Application/DTOs/Investments/PortfolioDetailDto.cs src/backend/src/BigSchool.Application/Queries/Investments/GetPortfolios/ src/backend/src/BigSchool.Application/Queries/Investments/GetPortfolioById/
git commit -m "feat: queries Dapper GetPortfolios y GetPortfolioById (valoración multimoneda con fallback de tipo)"
```

---

## Task 11: Application — Query Dapper `GetPortfolioPerformance`

Desglose realizado / no realizado / total en la moneda base, por lote abierto y consolidado.

**Files:**
- Create: `src/backend/src/BigSchool.Application/DTOs/Investments/HoldingPerformanceDto.cs`
- Create: `src/backend/src/BigSchool.Application/DTOs/Investments/PortfolioPerformanceDto.cs`
- Create: `src/backend/src/BigSchool.Application/Queries/Investments/GetPortfolioPerformance/{GetPortfolioPerformanceQuery,GetPortfolioPerformanceQueryHandler}.cs`

- [ ] **Step 1: DTOs**

```csharp
// HoldingPerformanceDto.cs
namespace BigSchool.Application.DTOs.Investments;

public record HoldingPerformanceDto(
    int IdHolding, int IdCompany, string Ticker,
    decimal OpenShares, decimal CostBasis, decimal MarketValue,
    decimal UnrealizedPnL, decimal UnrealizedPnLPct);
```

```csharp
// PortfolioPerformanceDto.cs
namespace BigSchool.Application.DTOs.Investments;

public record PortfolioPerformanceDto(
    int IdPortfolio, string Name, string BaseCurrency,
    decimal MarketValue, decimal CostBasis, decimal UnrealizedPnL,
    decimal RealizedPnL, decimal TotalPnL, decimal ReturnPct,
    IReadOnlyList<HoldingPerformanceDto> Holdings);
```

- [ ] **Step 2: Query + handler**

```csharp
// GetPortfolioPerformanceQuery.cs
using BigSchool.Application.DTOs.Investments;
using MediatR;

namespace BigSchool.Application.Queries.Investments.GetPortfolioPerformance;

public record GetPortfolioPerformanceQuery(int IdPortfolio, int IdUser) : IRequest<PortfolioPerformanceDto?>;
```

```csharp
// GetPortfolioPerformanceQueryHandler.cs
using BigSchool.Application.DTOs.Investments;
using BigSchool.Application.Interfaces;
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Application.Queries.Investments;
using BigSchool.Domain.Enums;
using Dapper;
using MediatR;

namespace BigSchool.Application.Queries.Investments.GetPortfolioPerformance;

public class GetPortfolioPerformanceQueryHandler
    : IRequestHandler<GetPortfolioPerformanceQuery, PortfolioPerformanceDto?>
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly IUserRepository _userRepository;

    public GetPortfolioPerformanceQueryHandler(IDbConnectionFactory dbFactory, IUserRepository userRepository)
    {
        _dbFactory = dbFactory;
        _userRepository = userRepository;
    }

    private const string HEADER_QUERY = @"SELECT IdPortfolio, Name, RealizedPnL, RealizedPnLCurrency
                                          FROM Portfolios
                                          WHERE IdPortfolio = @IdPortfolio AND IdUser = @IdUser AND IdStatus <> @StatusDeleted
                                          LIMIT 1;";

    // Solo lotes abiertos (OpenShares > 0) para el desglose no realizado.
    private const string OPEN_HOLDINGS_QUERY = @"
SELECT hv.IdHolding, hv.IdCompany, hv.Ticker, hv.OpenShares,
       hv.CostBasis, hv.MarketValue, hv.UnrealizedPnL
FROM (" + PortfolioSqlFragments.HOLDING_VALUATION + @") hv
WHERE hv.IdPortfolio = @IdPortfolio AND hv.OpenShares > 0
ORDER BY hv.BuyDate, hv.IdHolding;";

    private sealed record HoldingRow(int IdHolding, int IdCompany, string Ticker, decimal OpenShares,
        decimal CostBasis, decimal MarketValue, decimal UnrealizedPnL);

    public async Task<PortfolioPerformanceDto?> Handle(GetPortfolioPerformanceQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.IdUser, cancellationToken);
        var baseCurrency = (user?.BaseCurrency ?? Currency.EUR).ToString();

        var parameters = new DynamicParameters();
        parameters.Add("@IdPortfolio", request.IdPortfolio);
        parameters.Add("@IdUser", request.IdUser);
        parameters.Add("@StatusDeleted", EntityStatus.Deleted);
        parameters.Add("@BaseCurrency", baseCurrency);

        using var conn = _dbFactory.CreateConnection();

        var header = await conn.QuerySingleOrDefaultAsync(HEADER_QUERY, parameters);
        if (header is null) return null;

        var rows = (await conn.QueryAsync<HoldingRow>(OPEN_HOLDINGS_QUERY, parameters)).ToList();

        var holdings = rows.Select(r => new HoldingPerformanceDto(
            r.IdHolding, r.IdCompany, r.Ticker, r.OpenShares, r.CostBasis, r.MarketValue, r.UnrealizedPnL,
            r.CostBasis > 0m ? Math.Round(r.UnrealizedPnL / r.CostBasis * 100m, 2) : 0m)).ToList();

        var marketValue = holdings.Sum(h => h.MarketValue);
        var costBasis = holdings.Sum(h => h.CostBasis);
        var unrealized = holdings.Sum(h => h.UnrealizedPnL);
        var realized = (decimal)header.RealizedPnL;
        var total = realized + unrealized;
        var returnPct = costBasis > 0m ? Math.Round(unrealized / costBasis * 100m, 2) : 0m;

        return new PortfolioPerformanceDto(
            (int)header.IdPortfolio, (string)header.Name, (string)header.RealizedPnLCurrency,
            marketValue, costBasis, unrealized, realized, total, returnPct, holdings);
    }
}
```

- [ ] **Step 3: Build + commit**

Run: `dotnet build src/backend/Backend.sln -c Release`
Expected: Build succeeded.

```bash
git add src/backend/src/BigSchool.Application/DTOs/Investments/HoldingPerformanceDto.cs src/backend/src/BigSchool.Application/DTOs/Investments/PortfolioPerformanceDto.cs src/backend/src/BigSchool.Application/Queries/Investments/GetPortfolioPerformance/
git commit -m "feat: query Dapper GetPortfolioPerformance (realizado/no realizado/total en base)"
```

---

## Task 12: WebApi — `PortfoliosController`

8 endpoints `[Authorize]`. El controller solo orquesta (request → Mediator → response), igual que `TransactionsController`. `IdUser` se obtiene de `CurrentUser.GetId(User, _encryptor)`.

**Files:**
- Create: `src/backend/src/BigSchool.WebApi/Controllers/PortfoliosController.cs`

- [ ] **Step 1: Implementar `PortfoliosController`**

```csharp
using BigSchool.Application.Commands.Investments.AddHolding;
using BigSchool.Application.Commands.Investments.CreatePortfolio;
using BigSchool.Application.Commands.Investments.DeleteHolding;
using BigSchool.Application.Commands.Investments.SellShares;
using BigSchool.Application.Commands.Investments.UpdateHolding;
using BigSchool.Application.Common;
using BigSchool.Application.DTOs.Investments;
using BigSchool.Application.Interfaces.Services;
using BigSchool.Application.Queries.Investments.GetPortfolioById;
using BigSchool.Application.Queries.Investments.GetPortfolioPerformance;
using BigSchool.Application.Queries.Investments.GetPortfolios;
using BigSchool.WebApi.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BigSchool.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/portfolios")]
public class PortfoliosController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserIdEncryptor _encryptor;

    public PortfoliosController(IMediator mediator, IUserIdEncryptor encryptor)
    {
        _mediator = mediator;
        _encryptor = encryptor;
    }

    private int UserId => CurrentUser.GetId(User, _encryptor);

    public record CreatePortfolioRequest(string Name);
    public record AddHoldingRequest(int IdCompany, decimal Shares, decimal BuyPrice, DateOnly BuyDate, string? Notes);
    public record UpdateHoldingRequest(string? Notes);
    public record SellSharesRequest(int CompanyId, decimal Shares, decimal SellPrice, DateOnly SellDate, string? Notes);

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PortfolioListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get()
    {
        var result = await _mediator.Send(new GetPortfoliosQuery(UserId));
        return Ok(ApiResponse<IReadOnlyList<PortfolioListItemDto>>.Success(result));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<PortfolioDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreatePortfolioRequest body)
    {
        var result = await _mediator.Send(new CreatePortfolioCommand(UserId, body.Name));
        return Ok(ApiResponse<PortfolioDto>.Success(result));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<PortfolioDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _mediator.Send(new GetPortfolioByIdQuery(id, UserId));
        return result is null
            ? NotFound(ApiResponse.Fail(new ApiError { Code = "ENTITY_NOT_FOUND", Message = "Cartera no encontrada." }))
            : Ok(ApiResponse<PortfolioDetailDto>.Success(result));
    }

    [HttpGet("{id:int}/performance")]
    [ProducesResponseType(typeof(ApiResponse<PortfolioPerformanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Performance(int id)
    {
        var result = await _mediator.Send(new GetPortfolioPerformanceQuery(id, UserId));
        return result is null
            ? NotFound(ApiResponse.Fail(new ApiError { Code = "ENTITY_NOT_FOUND", Message = "Cartera no encontrada." }))
            : Ok(ApiResponse<PortfolioPerformanceDto>.Success(result));
    }

    [HttpPost("{id:int}/holdings")]
    [ProducesResponseType(typeof(ApiResponse<HoldingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddHolding(int id, [FromBody] AddHoldingRequest body)
    {
        var command = new AddHoldingCommand(id, UserId, body.IdCompany, body.Shares, body.BuyPrice, body.BuyDate, body.Notes);
        var result = await _mediator.Send(command);
        return Ok(ApiResponse<HoldingDto>.Success(result));
    }

    [HttpPut("{id:int}/holdings/{holdingId:int}")]
    [ProducesResponseType(typeof(ApiResponse<HoldingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateHolding(int id, int holdingId, [FromBody] UpdateHoldingRequest body)
    {
        var command = new UpdateHoldingCommand(id, UserId, holdingId, body.Notes);
        var result = await _mediator.Send(command);
        return Ok(ApiResponse<HoldingDto>.Success(result));
    }

    [HttpDelete("{id:int}/holdings/{holdingId:int}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteHolding(int id, int holdingId)
    {
        await _mediator.Send(new DeleteHoldingCommand(id, UserId, holdingId));
        return Ok(ApiResponse.Success());
    }

    [HttpPost("{id:int}/sales")]
    [ProducesResponseType(typeof(ApiResponse<SellSharesResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Sell(int id, [FromBody] SellSharesRequest body)
    {
        var command = new SellSharesCommand(id, UserId, body.CompanyId, body.Shares, body.SellPrice, body.SellDate, body.Notes);
        var result = await _mediator.Send(command);
        return Ok(ApiResponse<SellSharesResultDto>.Success(result));
    }
}
```

- [ ] **Step 2: Build + commit**

Run: `dotnet build src/backend/Backend.sln -c Release`
Expected: Build succeeded.

```bash
git add src/backend/src/BigSchool.WebApi/Controllers/PortfoliosController.cs
git commit -m "feat: PortfoliosController (8 endpoints carteras/holdings/ventas FIFO/performance)"
```

---

## Task 13: Integration — `ResetAsync` ampliado + `PortfolioEndpointTestBase`

**Files:**
- Modify: `src/backend/tests/BigSchool.Integration.Tests/Fixtures/MySqlDatabaseFixture.cs`
- Create: `src/backend/tests/BigSchool.Integration.Tests/Investments/PortfolioEndpointTestBase.cs`

- [ ] **Step 1: Ampliar `ResetAsync`** — limpiar las tablas de cartera y los datos de catálogo creados por tests

Reemplazar el cuerpo del método `ResetAsync` por:

```csharp
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
```

- [ ] **Step 2: `PortfolioEndpointTestBase`** — helpers y tipos de deserialización

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BigSchool.Application.Interfaces.Services;
using BigSchool.Domain.Entities;
using BigSchool.Domain.Enums;
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
```

- [ ] **Step 3: Build de tests + commit**

Run: `dotnet build src/backend/tests/BigSchool.Integration.Tests/BigSchool.Integration.Tests.csproj -c Release`
Expected: Build succeeded.

```bash
git add src/backend/tests/BigSchool.Integration.Tests/Fixtures/MySqlDatabaseFixture.cs src/backend/tests/BigSchool.Integration.Tests/Investments/PortfolioEndpointTestBase.cs
git commit -m "test: ResetAsync limpia tablas de cartera/catálogo + PortfolioEndpointTestBase"
```

---

## Task 14: Integration E2E — CreatePortfolio, AddHolding, GetPortfolios, GetPortfolioById

Un fichero por endpoint. **Requiere MySQL levantado.** Usa el catálogo seedeado.

**Files:**
- Create: `src/backend/tests/BigSchool.Integration.Tests/Investments/PostPortfolioTests.cs`
- Create: `src/backend/tests/BigSchool.Integration.Tests/Investments/PostHoldingTests.cs`
- Create: `src/backend/tests/BigSchool.Integration.Tests/Investments/GetPortfoliosTests.cs`
- Create: `src/backend/tests/BigSchool.Integration.Tests/Investments/GetPortfolioByIdTests.cs`

- [ ] **Step 1: `PostPortfolioTests`**

```csharp
using System.Net;
using System.Net.Http.Json;
using BigSchool.Domain.Enums;
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
```

- [ ] **Step 2: `PostHoldingTests`** (compra multimoneda con verificación física y conversión determinista)

```csharp
using System.Net;
using System.Net.Http.Json;
using BigSchool.Domain.Enums;
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
```

- [ ] **Step 3: `GetPortfoliosTests`** (resumen con valor de mercado convertido)

```csharp
using System.Net;
using System.Net.Http.Json;
using BigSchool.Domain.Enums;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Investments;

[Collection(IntegrationCollection.Name)]
public class GetPortfoliosTests : PortfolioEndpointTestBase
{
    public GetPortfoliosTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Get_ReturnsSummary_WithMarketValueConvertedToBase()
    {
        var (userId, email) = await SeedUserAsync(Currency.EUR);
        var client = AuthenticatedClient(userId, email);
        var portfolioId = await CreatePortfolioViaApiAsync(client, "Tech");

        await SeedExchangeRateAsync(Currency.USD, Currency.EUR, 0.90m, new DateOnly(2026, 1, 5)); // compra
        await SeedExchangeRateAsync(Currency.USD, Currency.EUR, 1.00m, new DateOnly(2026, 3, 2)); // última valoración AAPL
        await AddHoldingViaApiAsync(client, portfolioId, CompanyAaplUsd, 10m, 195m, "2026-01-05");

        var response = await client.GetAsync("/api/v1/portfolios");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = (await response.Content.ReadFromJsonAsync<ApiEnvelope<List<PortfolioListItemResponse>>>())!.Data!;
        var p = list.Single(x => x.IdPortfolio == portfolioId);
        p.Name.Should().Be("Tech");
        p.RealizedPnL.Should().Be(0m);
        // Última valoración AAPL = 210 USD (2026-03-02) × rate 1.00 × 10 = 2100
        p.MarketValue.Should().Be(2100m);
        p.CostBasis.Should().Be(1755m);      // 175.50 × 10
        p.UnrealizedPnL.Should().Be(345m);   // 2100 − 1755
        p.TotalPnL.Should().Be(345m);        // realized 0 + unrealized
    }

    [Fact]
    public async Task Get_WithoutToken_Returns401()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/v1/portfolios");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

- [ ] **Step 4: `GetPortfolioByIdTests`**

```csharp
using System.Net;
using System.Net.Http.Json;
using BigSchool.Domain.Enums;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Investments;

[Collection(IntegrationCollection.Name)]
public class GetPortfolioByIdTests : PortfolioEndpointTestBase
{
    public GetPortfolioByIdTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetById_ReturnsDetail_WithHoldings()
    {
        var (userId, email) = await SeedUserAsync(Currency.EUR);
        var client = AuthenticatedClient(userId, email);
        var portfolioId = await CreatePortfolioViaApiAsync(client, "Mixto");
        await AddHoldingViaApiAsync(client, portfolioId, CompanySanEur, 100m, 4.5m, "2026-01-05", "lote SAN");

        var response = await client.GetAsync($"/api/v1/portfolios/{portfolioId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = (await response.Content.ReadFromJsonAsync<ApiEnvelope<PortfolioDetailResponse>>())!.Data!;
        detail.IdPortfolio.Should().Be(portfolioId);
        detail.Name.Should().Be("Mixto");
        detail.Holdings.Should().ContainSingle();
        var h = detail.Holdings[0];
        h.Ticker.Should().Be("SAN");
        h.CompanyCurrency.Should().Be("EUR");
        h.Shares.Should().Be(100m);
        h.OpenShares.Should().Be(100m);
        h.BuyBaseAmount.Should().Be(4.5m);
        h.Notes.Should().Be("lote SAN");
    }

    [Fact]
    public async Task GetById_ForeignPortfolio_Returns404()
    {
        var (ownerId, ownerEmail) = await SeedUserAsync();
        var portfolioId = await CreatePortfolioViaApiAsync(AuthenticatedClient(ownerId, ownerEmail));

        var (otherId, otherEmail) = await SeedUserAsync();
        var response = await AuthenticatedClient(otherId, otherEmail).GetAsync($"/api/v1/portfolios/{portfolioId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_WithoutToken_Returns401()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/v1/portfolios/1");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

- [ ] **Step 5: Ejecutar los E2E de esta task + commit**

Run: `dotnet test src/backend/tests/BigSchool.Integration.Tests/BigSchool.Integration.Tests.csproj -c Release --filter "FullyQualifiedName~Investments.PostPortfolioTests|FullyQualifiedName~Investments.PostHoldingTests|FullyQualifiedName~Investments.GetPortfoliosTests|FullyQualifiedName~Investments.GetPortfolioByIdTests"`
Expected: PASS (todos). Si no hay Docker/MySQL, documentar el SKIP y continuar.

```bash
git add src/backend/tests/BigSchool.Integration.Tests/Investments/PostPortfolioTests.cs src/backend/tests/BigSchool.Integration.Tests/Investments/PostHoldingTests.cs src/backend/tests/BigSchool.Integration.Tests/Investments/GetPortfoliosTests.cs src/backend/tests/BigSchool.Integration.Tests/Investments/GetPortfolioByIdTests.cs
git commit -m "test(e2e): CreatePortfolio, AddHolding (multimoneda), GetPortfolios y GetPortfolioById"
```

---

## Task 15: Integration E2E — venta FIFO, ciclo completo value investing, performance, Put/Delete

**Files:**
- Create: `src/backend/tests/BigSchool.Integration.Tests/Investments/PostSaleTests.cs`
- Create: `src/backend/tests/BigSchool.Integration.Tests/Investments/GetPerformanceTests.cs`
- Create: `src/backend/tests/BigSchool.Integration.Tests/Investments/PutHoldingTests.cs`
- Create: `src/backend/tests/BigSchool.Integration.Tests/Investments/DeleteHoldingTests.cs`

- [ ] **Step 1: `PostSaleTests`** (FIFO multi-lote + ciclo value investing multimoneda + 400/401/404)

```csharp
using System.Net;
using System.Net.Http.Json;
using BigSchool.Domain.Enums;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Investments;

[Collection(IntegrationCollection.Name)]
public class PostSaleTests : PortfolioEndpointTestBase
{
    public PostSaleTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Sell_FifoAcrossLots_GeneratesNDisposals_AndConsolidatesRealized()
    {
        var (userId, email) = await SeedUserAsync(Currency.EUR);
        var client = AuthenticatedClient(userId, email);
        var portfolioId = await CreatePortfolioViaApiAsync(client, "FIFO");

        // Dos lotes de SAN (EUR, rate 1) con fechas distintas → FIFO determinista sin FX.
        var lotA = await AddHoldingViaApiAsync(client, portfolioId, CompanySanEur, 100m, 4.0m, "2026-01-05", "A");
        var lotB = await AddHoldingViaApiAsync(client, portfolioId, CompanySanEur, 100m, 6.0m, "2026-02-05", "B");

        // Vender 150 @ 8 → consume A (100) y parte de B (50).
        var response = await client.PostAsJsonAsync($"/api/v1/portfolios/{portfolioId}/sales", new
        {
            companyId = CompanySanEur, shares = 150m, sellPrice = 8.0m, sellDate = "2026-03-05", notes = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = (await response.Content.ReadFromJsonAsync<ApiEnvelope<SellResultResponse>>())!.Data!;
        result.Disposals.Should().HaveCount(2);
        result.Disposals[0].IdHolding.Should().Be(lotA);
        result.Disposals[0].Shares.Should().Be(100m);
        result.Disposals[0].RealizedPnL.Should().Be(400m); // (8-4)*100
        result.Disposals[1].IdHolding.Should().Be(lotB);
        result.Disposals[1].Shares.Should().Be(50m);
        result.Disposals[1].RealizedPnL.Should().Be(100m); // (8-6)*50
        result.PortfolioRealizedPnL.Should().Be(500m);
        result.RealizedPnLCurrency.Should().Be("EUR");

        // Persistencia física: RealizedPnL de la cartera, disposals y open shares.
        (await ReadPortfolioRealizedPnLAsync(portfolioId)).Should().Be(500m);
        (await CountActiveDisposalsAsync(lotA)).Should().Be(1);
        (await CountActiveDisposalsAsync(lotB)).Should().Be(1);

        var detail = (await (await client.GetAsync($"/api/v1/portfolios/{portfolioId}"))
            .Content.ReadFromJsonAsync<ApiEnvelope<PortfolioDetailResponse>>())!.Data!;
        detail.Holdings.Single(h => h.IdHolding == lotA).OpenShares.Should().Be(0m);
        detail.Holdings.Single(h => h.IdHolding == lotB).OpenShares.Should().Be(50m);
    }

    [Fact]
    public async Task Sell_UsdLot_RealizedIncludesFxEffect()
    {
        var (userId, email) = await SeedUserAsync(Currency.EUR);
        var client = AuthenticatedClient(userId, email);
        var portfolioId = await CreatePortfolioViaApiAsync(client, "USD");

        await SeedExchangeRateAsync(Currency.USD, Currency.EUR, 0.90m, new DateOnly(2026, 1, 5)); // compra
        await SeedExchangeRateAsync(Currency.USD, Currency.EUR, 1.00m, new DateOnly(2026, 3, 5)); // venta
        var lot = await AddHoldingViaApiAsync(client, portfolioId, CompanyAaplUsd, 10m, 195m, "2026-01-05");

        var response = await client.PostAsJsonAsync($"/api/v1/portfolios/{portfolioId}/sales", new
        {
            companyId = CompanyAaplUsd, shares = 10m, sellPrice = 200m, sellDate = "2026-03-05", notes = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = (await response.Content.ReadFromJsonAsync<ApiEnvelope<SellResultResponse>>())!.Data!;
        var d = result.Disposals.Single();
        d.IdHolding.Should().Be(lot);
        d.SellOriginalAmount.Should().Be(200m);
        d.SellOriginalCurrency.Should().Be("USD");
        d.SellExchangeRate.Should().Be(1.00m);
        d.SellBaseAmount.Should().Be(200m);          // 200 * 1.00
        // coste base 175.50/u (195*0.9); venta base 200/u → realizado (200-175.50)*10 = 245
        d.RealizedPnL.Should().Be(245m);
        result.PortfolioRealizedPnL.Should().Be(245m);
        (await ReadPortfolioRealizedPnLAsync(portfolioId)).Should().Be(245m);
    }

    [Fact]
    public async Task Sell_MoreThanAvailable_Returns400_InsufficientShares()
    {
        var (userId, email) = await SeedUserAsync(Currency.EUR);
        var client = AuthenticatedClient(userId, email);
        var portfolioId = await CreatePortfolioViaApiAsync(client);
        await AddHoldingViaApiAsync(client, portfolioId, CompanySanEur, 100m, 4.0m, "2026-01-05");

        var response = await client.PostAsJsonAsync($"/api/v1/portfolios/{portfolioId}/sales", new
        {
            companyId = CompanySanEur, shares = 101m, sellPrice = 8.0m, sellDate = "2026-03-05"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        env!.Errors.Should().Contain(e => e.Code == "INSUFFICIENT_SHARES");
    }

    [Fact]
    public async Task Sell_WithoutToken_Returns401()
    {
        var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/portfolios/1/sales",
            new { companyId = 3, shares = 1m, sellPrice = 8m, sellDate = "2026-03-05" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Sell_ForeignPortfolio_Returns404()
    {
        var (ownerId, ownerEmail) = await SeedUserAsync();
        var ownerClient = AuthenticatedClient(ownerId, ownerEmail);
        var portfolioId = await CreatePortfolioViaApiAsync(ownerClient);
        await AddHoldingViaApiAsync(ownerClient, portfolioId, CompanySanEur, 100m, 4.0m, "2026-01-05");

        var (otherId, otherEmail) = await SeedUserAsync();
        var response = await AuthenticatedClient(otherId, otherEmail).PostAsJsonAsync(
            $"/api/v1/portfolios/{portfolioId}/sales",
            new { companyId = CompanySanEur, shares = 10m, sellPrice = 8m, sellDate = "2026-03-05" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
```

- [ ] **Step 2: `GetPerformanceTests`** (desglose realizado/no realizado/total)

```csharp
using System.Net;
using System.Net.Http.Json;
using BigSchool.Domain.Enums;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Investments;

[Collection(IntegrationCollection.Name)]
public class GetPerformanceTests : PortfolioEndpointTestBase
{
    public GetPerformanceTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Performance_UnrealizedOnly_UsdHolding()
    {
        var (userId, email) = await SeedUserAsync(Currency.EUR);
        var client = AuthenticatedClient(userId, email);
        var portfolioId = await CreatePortfolioViaApiAsync(client, "Tech");
        await SeedExchangeRateAsync(Currency.USD, Currency.EUR, 0.90m, new DateOnly(2026, 1, 5));
        await SeedExchangeRateAsync(Currency.USD, Currency.EUR, 1.00m, new DateOnly(2026, 3, 2)); // fecha última valoración AAPL
        await AddHoldingViaApiAsync(client, portfolioId, CompanyAaplUsd, 10m, 195m, "2026-01-05");

        var response = await client.GetAsync($"/api/v1/portfolios/{portfolioId}/performance");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var perf = (await response.Content.ReadFromJsonAsync<ApiEnvelope<PerformanceResponse>>())!.Data!;
        perf.BaseCurrency.Should().Be("EUR");
        perf.MarketValue.Should().Be(2100m);   // 210 USD × 1.00 × 10
        perf.CostBasis.Should().Be(1755m);      // 175.50 × 10
        perf.UnrealizedPnL.Should().Be(345m);
        perf.RealizedPnL.Should().Be(0m);
        perf.TotalPnL.Should().Be(345m);
        perf.ReturnPct.Should().Be(19.66m);     // 345/1755*100
        perf.Holdings.Should().ContainSingle();
        perf.Holdings[0].Ticker.Should().Be("AAPL");
        perf.Holdings[0].UnrealizedPnLPct.Should().Be(19.66m);
    }

    [Fact]
    public async Task Performance_FullCycle_RealizedAndUnrealized()
    {
        var (userId, email) = await SeedUserAsync(Currency.EUR);
        var client = AuthenticatedClient(userId, email);
        var portfolioId = await CreatePortfolioViaApiAsync(client, "Mixto");

        // SAN EUR (rate 1). Compra 100 @ 4; vende 40 @ 8 → realizado 160. Quedan 60 abiertas.
        await AddHoldingViaApiAsync(client, portfolioId, CompanySanEur, 100m, 4.0m, "2026-01-05");
        var sell = await client.PostAsJsonAsync($"/api/v1/portfolios/{portfolioId}/sales", new
        {
            companyId = CompanySanEur, shares = 40m, sellPrice = 8.0m, sellDate = "2026-03-05"
        });
        sell.EnsureSuccessStatusCode();

        var response = await client.GetAsync($"/api/v1/portfolios/{portfolioId}/performance");
        var perf = (await response.Content.ReadFromJsonAsync<ApiEnvelope<PerformanceResponse>>())!.Data!;

        perf.RealizedPnL.Should().Be(160m);     // (8-4)*40
        // Última valoración SAN = 4.8 EUR (2026-03-02). Quedan 60 abiertas.
        perf.MarketValue.Should().Be(288m);     // 60 × 4.8
        perf.CostBasis.Should().Be(240m);       // 60 × 4
        perf.UnrealizedPnL.Should().Be(48m);    // 288 − 240
        perf.TotalPnL.Should().Be(208m);        // 160 + 48
        perf.ReturnPct.Should().Be(20.00m);     // 48/240*100
    }

    [Fact]
    public async Task Performance_ForeignPortfolio_Returns404()
    {
        var (ownerId, ownerEmail) = await SeedUserAsync();
        var portfolioId = await CreatePortfolioViaApiAsync(AuthenticatedClient(ownerId, ownerEmail));
        var (otherId, otherEmail) = await SeedUserAsync();

        var response = await AuthenticatedClient(otherId, otherEmail)
            .GetAsync($"/api/v1/portfolios/{portfolioId}/performance");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Performance_WithoutToken_Returns401()
    {
        var response = await Factory.CreateClient().GetAsync("/api/v1/portfolios/1/performance");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

- [ ] **Step 3: `PutHoldingTests`**

```csharp
using System.Net;
using System.Net.Http.Json;
using BigSchool.Domain.Enums;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Investments;

[Collection(IntegrationCollection.Name)]
public class PutHoldingTests : PortfolioEndpointTestBase
{
    public PutHoldingTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Put_UpdatesNotes_AndPersists()
    {
        var (userId, email) = await SeedUserAsync(Currency.EUR);
        var client = AuthenticatedClient(userId, email);
        var portfolioId = await CreatePortfolioViaApiAsync(client);
        var holdingId = await AddHoldingViaApiAsync(client, portfolioId, CompanySanEur, 100m, 4.5m, "2026-01-05", "old");

        var response = await client.PutAsJsonAsync(
            $"/api/v1/portfolios/{portfolioId}/holdings/{holdingId}", new { notes = "actualizado" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = (await response.Content.ReadFromJsonAsync<ApiEnvelope<HoldingResponse>>())!.Data!;
        dto.Notes.Should().Be("actualizado");

        await using var conn = new MySqlConnector.MySqlConnection(Fixture.ConnectionString);
        var notes = await Dapper.SqlMapper.QuerySingleAsync<string>(conn,
            "SELECT Notes FROM Holdings WHERE IdHolding = @id;", new { id = holdingId });
        notes.Should().Be("actualizado");
    }

    [Fact]
    public async Task Put_ForeignPortfolio_Returns404()
    {
        var (ownerId, ownerEmail) = await SeedUserAsync();
        var ownerClient = AuthenticatedClient(ownerId, ownerEmail);
        var portfolioId = await CreatePortfolioViaApiAsync(ownerClient);
        var holdingId = await AddHoldingViaApiAsync(ownerClient, portfolioId, CompanySanEur, 10m, 4.5m, "2026-01-05");

        var (otherId, otherEmail) = await SeedUserAsync();
        var response = await AuthenticatedClient(otherId, otherEmail).PutAsJsonAsync(
            $"/api/v1/portfolios/{portfolioId}/holdings/{holdingId}", new { notes = "x" });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Put_WithoutToken_Returns401()
    {
        var response = await Factory.CreateClient().PutAsJsonAsync(
            "/api/v1/portfolios/1/holdings/1", new { notes = "x" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

- [ ] **Step 4: `DeleteHoldingTests`** (soft-delete + reversa de realizado)

```csharp
using System.Net;
using System.Net.Http.Json;
using BigSchool.Domain.Enums;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Investments;

[Collection(IntegrationCollection.Name)]
public class DeleteHoldingTests : PortfolioEndpointTestBase
{
    public DeleteHoldingTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Delete_SoftDeletesHolding_ReversesRealized_AndHidesFromReads()
    {
        var (userId, email) = await SeedUserAsync(Currency.EUR);
        var client = AuthenticatedClient(userId, email);
        var portfolioId = await CreatePortfolioViaApiAsync(client);
        var holdingId = await AddHoldingViaApiAsync(client, portfolioId, CompanySanEur, 100m, 4.0m, "2026-01-05");
        var sell = await client.PostAsJsonAsync($"/api/v1/portfolios/{portfolioId}/sales", new
        {
            companyId = CompanySanEur, shares = 40m, sellPrice = 8.0m, sellDate = "2026-03-05"
        });
        sell.EnsureSuccessStatusCode();
        (await ReadPortfolioRealizedPnLAsync(portfolioId)).Should().Be(160m);

        var response = await client.DeleteAsync($"/api/v1/portfolios/{portfolioId}/holdings/{holdingId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        // Soft-delete: la fila persiste con IdStatus=Deleted.
        ((short)(await ReadHoldingStatusAsync(holdingId))!).Should().Be((short)EntityStatus.Deleted);
        // Realizado revertido.
        (await ReadPortfolioRealizedPnLAsync(portfolioId)).Should().Be(0m);
        // Las lecturas posteriores la ocultan.
        var detail = (await (await client.GetAsync($"/api/v1/portfolios/{portfolioId}"))
            .Content.ReadFromJsonAsync<ApiEnvelope<PortfolioDetailResponse>>())!.Data!;
        detail.Holdings.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_ForeignPortfolio_Returns404()
    {
        var (ownerId, ownerEmail) = await SeedUserAsync();
        var ownerClient = AuthenticatedClient(ownerId, ownerEmail);
        var portfolioId = await CreatePortfolioViaApiAsync(ownerClient);
        var holdingId = await AddHoldingViaApiAsync(ownerClient, portfolioId, CompanySanEur, 10m, 4.5m, "2026-01-05");

        var (otherId, otherEmail) = await SeedUserAsync();
        var response = await AuthenticatedClient(otherId, otherEmail)
            .DeleteAsync($"/api/v1/portfolios/{portfolioId}/holdings/{holdingId}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_WithoutToken_Returns401()
    {
        var response = await Factory.CreateClient().DeleteAsync("/api/v1/portfolios/1/holdings/1");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

- [ ] **Step 5: Ejecutar todos los E2E de Investments + commit**

Run: `dotnet test src/backend/tests/BigSchool.Integration.Tests/BigSchool.Integration.Tests.csproj -c Release --filter "FullyQualifiedName~Investments"`
Expected: PASS (todos). Si no hay Docker/MySQL, documentar el SKIP.

```bash
git add src/backend/tests/BigSchool.Integration.Tests/Investments/PostSaleTests.cs src/backend/tests/BigSchool.Integration.Tests/Investments/GetPerformanceTests.cs src/backend/tests/BigSchool.Integration.Tests/Investments/PutHoldingTests.cs src/backend/tests/BigSchool.Integration.Tests/Investments/DeleteHoldingTests.cs
git commit -m "test(e2e): venta FIFO multimoneda, performance, PUT/DELETE holding (soft-delete + reversa)"
```

---

## Task 16: Documentación + suite completa + PR

**Files:**
- Modify: `docs/02-backend-design.md`
- Modify: `docs/01-arquitectura.md`
- Modify: `docs/diario.md`

- [ ] **Step 1: `docs/02-backend-design.md`** — actualizar el modelo de datos de Inversiones

Añadir/actualizar (leer el fichero y editar la sección de Inversiones):
- Tabla **Portfolios**: `IdPortfolio, IdUser (FK Users), Name VARCHAR(100), RealizedPnL DECIMAL(18,2) + RealizedPnLCurrency CHAR(3), IdStatus, CreatedAt, UpdatedAt`. Nota: `RealizedPnL` es **columna persistida** mantenida por el agregado.
- Tabla **Holdings** = **lote de compra**: `IdHolding, IdPortfolio (FK), IdCompany (FK Companies, RESTRICT), Shares DECIMAL(18,4), AvgBuyPrice como MoneyConversion (BuyOriginalAmount/BuyOriginalCurrency/BuyExchangeRate/BuyBaseAmount/BuyBaseCurrency/BuyRateDate), BuyDate DATE, Notes VARCHAR(500), IdStatus, auditoría`. Derivado `OpenShares = Shares − Σ Disposals activos`.
- Tabla **Disposals** (nueva) = **venta**: `IdDisposal, IdHolding (FK), Shares DECIMAL(18,4), SellPrice como MoneyConversion (Sell*), SellDate DATE, RealizedPnL DECIMAL(18,2) + RealizedPnLCurrency CHAR(3) (calculado), Notes, IdStatus, auditoría`.
- Regla **FIFO** para ventas (sección 5 de la spec): la venta es a nivel (Portfolio, Company); consume lotes por antigüedad; genera N Disposals.
- Endpoints nuevos (sección 8 de la spec): `GET/POST /portfolios`, `GET /portfolios/{id}`, `POST/PUT/DELETE /portfolios/{id}/holdings[/{hId}]`, `POST /portfolios/{id}/sales`, `GET /portfolios/{id}/performance`.

- [ ] **Step 2: `docs/01-arquitectura.md`** — ADR del BC Inversiones

Añadir un ADR (formato existente del fichero) con la decisión:
> **ADR — BC Inversiones (value investing multimoneda).** Catálogo `Company`/`Valuation` global vs cartera `Portfolio` por-usuario. `Holding` = lote (recompra = nuevo lote). Lado venta `Disposal` con **disposición FIFO obligatoria** (IRPF). Coste base y precio de venta como `MoneyConversion` snapshot congelado a su fecha; `RealizedPnL` consolidado y persistido en `Portfolio`; performance consolidada en la moneda base con conversión de la última valoración al tipo de su fecha (fallback al último tipo ≤ fecha). Hijas/nietas (`Holding`/`Disposal`) accedidas solo a través del AR; sin `InternalsVisibleTo` (tests del agregado).

- [ ] **Step 3: `docs/diario.md`** — entrada de Plan 3B

Añadir al principio de la lista de entradas (formato de las existentes):
```markdown
## 2026-06-21 — BC Inversiones Plan 3B: Carteras (Portfolio/Holding/Disposal, FIFO)

Implementado el agregado `Portfolio` (AR por-usuario) con `Holding` (lote) y `Disposal` (venta):
- **Dominio**: `AddHolding` (snapshot `AvgBuyPrice` multimoneda congelado a `BuyDate`), `SellShares`
  **FIFO** a nivel (Portfolio, Company) generando N `Disposal`, `RealizedPnL` consolidado y persistido,
  `DeleteHolding` con soft-delete en cascada y reversa del realizado. `InsufficientSharesDomainException` (400).
- **DDD**: hijas/nietas accedidas solo por el AR; **sin `InternalsVisibleTo`** — toda la lógica se testea
  desde `PortfolioTests`.
- **Persistencia**: configs `Portfolio/Holding/Disposal` (owned `MoneyConversion`/`Money`, shadow FKs),
  migración `CreatePortfolios`, `PortfolioRepository.GetByIdWithHoldingsAsync`.
- **CQRS**: commands `CreatePortfolio`/`AddHolding`/`UpdateHolding`/`DeleteHolding`/`SellShares`; queries
  Dapper `GetPortfolios`/`GetPortfolioById`/`GetPortfolioPerformance` (consolidación en base con fallback
  de tipo). `PortfoliosController` (8 endpoints `[Authorize]`).
- **Tests**: dominio (FIFO exhaustivo), Application (handlers + validators), E2E por endpoint + ciclo
  completo value investing (compra multi-lote → venta FIFO cruzando lotes → `RealizedPnL` y performance).

Siguiente paso: integración frontend del BC Inversiones.
```

- [ ] **Step 4: Suite completa (Release)**

Run:
```bash
dotnet build src/backend/Backend.sln -c Release
dotnet test src/backend/tests/BigSchool.Domain.Tests/BigSchool.Domain.Tests.csproj -c Release
dotnet test src/backend/tests/BigSchool.Application.Tests/BigSchool.Application.Tests.csproj -c Release
dotnet test src/backend/tests/BigSchool.Integration.Tests/BigSchool.Integration.Tests.csproj -c Release
```
Expected: todo PASS, sin regresiones. Documentar el resultado (nº de tests). Si no hay Docker, los E2E se omiten — anotarlo.

- [ ] **Step 5: Commit docs + push + PR**

```bash
git add docs/02-backend-design.md docs/01-arquitectura.md docs/diario.md
git commit -m "docs: BC Inversiones Plan 3B (modelo Portfolio/Holding/Disposal, ADR FIFO, diario)"
git push -u origin feature/plan-3b-portfolios
gh pr create --base develop --title "feat: BC Inversiones Plan 3B — Carteras (Portfolio/Holding/Disposal, FIFO)" --body "$(cat <<'EOF'
## Resumen
Implementa el BC Inversiones — Carteras (Plan 3B), consumiendo el catálogo del Plan 3A.

- Agregado `Portfolio` (AR) → `Holding` (lote) → `Disposal` (venta), 3 niveles.
- Ventas **FIFO** a nivel (Portfolio, Company); `RealizedPnL` persistido en `Portfolio`.
- `AvgBuyPrice`/`SellPrice` como `MoneyConversion` snapshot multimoneda congelado a su fecha.
- CQRS: 5 commands + 3 queries Dapper (consolidación en base con fallback de tipo).
- `PortfoliosController` (8 endpoints `[Authorize]`).
- Tests: dominio (FIFO exhaustivo, vía el AR — **sin `InternalsVisibleTo`**), Application, E2E por endpoint + ciclo completo value investing.

## Verificación
- Build Release: OK
- Domain/Application/Integration tests: (rellenar nº) PASS

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 6: Anunciar la URL del PR y parar para revisión humana.**

---

## Self-Review (checklist del autor del plan)

- **Cobertura de spec 002**: §3.1 catálogo (3A) ✔ consumido; §3.2 Portfolio/Holding/Disposal ✔ (Tasks 1-4); §4 multimoneda (snapshot a Buy/Sell, valor a fecha de valoración con fallback) ✔ (Tasks 7, 9, 10, 11); §5 FIFO ✔ (Task 3 dominio + Task 9 handler + Task 15 E2E); §6.1 commands ✔ (Tasks 6-9); §6.2 queries ✔ (Tasks 10-11); §7 performance ✔ (Task 11); §8 endpoints ✔ (Task 12); §9 seed (3A) ✔ reutilizado; §11 testing ✔ (dominio/app/E2E ciclo completo); §13 docs ✔ (Task 16).
- **Sin placeholders**: todo el código está completo; las queries Dapper llevan SQL real; los tests tienen aserciones numéricas concretas.
- **Consistencia de tipos**: `AddHolding`/`SellShares` usan `Money` en moneda de empresa + `rate` resuelto en Application → `MoneyConversion` en dominio (igual que `Transaction`). DTOs command (Currency tipado) vs query (string) respetados. `IdHolding` en `DisposalDto` se obtiene correlacionando por referencia tras `SaveChanges`.
- **Feedback del usuario aplicado**: (1) ctores `protected/private/internal static` en `Holding`/`Disposal`; (2) configs con `ConfigureProperties/Relationships/Indexes/Filters`; (3) literales inline (sin clase de constantes); (4) **sin `InternalsVisibleTo`**, hijas/nietas testeadas por el AR.
- **Riesgo conocido**: `IExchangeRateProvider` resuelve por fecha exacta en cache (sin fallback) → los E2E con compra/venta en moneda extranjera **siembran la tasa a la fecha exacta**. La query de performance **sí** hace fallback (último tipo ≤ fecha). Verificado en el diseño de cada test.

---

## Execution Handoff

Plan completo y guardado en `docs/superpowers/plans/010-2026-06-21-backend-inversiones-plan-3b-portfolios.md`. Dos opciones de ejecución:

1. **Subagent-Driven (recomendado)** — un subagente fresco por task, revisión en dos fases (spec + calidad) entre tasks.
2. **Inline (executing-plans)** — ejecución por lotes con checkpoints.

> Nota: las Tasks 1 y 2 (Disposal, Holding) no tienen test propio por diseño (se validan vía el AR en Task 3); en la revisión de cumplimiento de spec de esas tasks, tratar "compila + cubierto por `PortfolioTests`" como criterio de DONE.
