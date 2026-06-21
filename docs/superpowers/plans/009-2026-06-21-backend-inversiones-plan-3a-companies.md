# BC Inversiones — Plan 3A: Catálogo (Company + Valuation) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementar el catálogo global de empresas y cotizaciones (`Company` AR + `Valuation` entidad hija) con CQRS, endpoints REST y tests E2E, como primer plan del BC Inversiones.

**Architecture:** `Company` es un Aggregate Root global (sin `IdUser`) que posee `Valuation` como entidad hija (mismo patrón que `User`↔`SubCategory`). `Valuation.Price` es un `Money` (owned type) en la moneda de cotización de la empresa — sin conversión a base de usuario (eso vive en Plan 3B). Commands con EF Core, queries con Dapper (convención `const _QUERY` UPPERCASE + `DynamicParameters`), soft-delete + Global Query Filter, envelope `ApiResponse<T>`, JWT real.

**Tech Stack:** .NET 8, EF Core + Pomelo MySQL, Dapper, MediatR, FluentValidation, xUnit + FluentAssertions + Moq, `WebApplicationFactory<Program>` para E2E contra MySQL `bigschool_test`.

Ruta base del backend: `src/backend/`. Todos los comandos se ejecutan desde la raíz del repo `C:\SourceCode\BigSchool-TFM`.

---

## Contexto del código existente (leer antes de empezar)

- **Patrón AR + hija**: `User` (AR) posee `SubCategory` (hija). Constructor `protected` para EF, `private`/`internal` factory `Create`, colección privada `_items` expuesta como `IReadOnlyCollection`. Ver `src/backend/src/BigSchool.Domain/Entities/User.cs` y `SubCategory.cs`.
- **VOs multimoneda ya existen**: `Money` (`Money.Create(amount, currency)`, escala 2 — pero las columnas de precio de inversión usan `decimal(18,4)`), `Currency` (enum `: short`, nombre = ISO alpha-3). Ver `ValueObjects/Money.cs`, `Enums/Currency.cs`.
- **Conversor EF**: `CurrencyConverter.CharIso` (`Currency`↔`char(3)`). Ver `Infrastructure/Persistence/Converters/CurrencyConverter.cs`.
- **Owned type en EF**: `TransactionConfiguration.ConfigureConversion` muestra `OwnsOne` con `HasColumnName`/`HasConversion`.
- **Repos**: `IRepository<T,Y>` (Application) + `EFRepository<T,Y>` (Infra, abstracto, expone `Context` y `UnitOfWork`). `TransactionRepository` es el ejemplo mínimo. Los repos se **auto-registran** en Autofac (`Program.cs` hace `RegisterAssemblyTypes(...).AsImplementedInterfaces()` por capa) — **no hay tarea de DI**.
- **Excepciones**: `DomainException` (abstract, `ErrorCode`), `ConflictException(errorCode, message)` → 409, `NotFoundException(entityName, key)` → 404 (`ENTITY_NOT_FOUND`). El `ExceptionHandlingMiddleware` mapea: `ConflictException`→409, `NotFoundException`→404, `DomainException`→400, `ValidationException`→400 (`VALIDATION_ERROR` + `Field`), `UnauthorizedAccessException`→401.
- **Envelope**: `ApiResponse<T>.Success(data, meta?)` / `ApiResponse.Fail(params ApiError[])`. `ApiError { Code, Message, Field }`.
- **DTOs**: convención del proyecto — DTOs construidos desde el dominio (commands) usan tipos fuertes (`Currency`); DTOs de queries Dapper usan `string` para monedas (Dapper no convierte `char(3)`→enum) y `DateOnly` para fechas (hay `DateOnlyTypeHandler` registrado globalmente).
- **Controller**: ver `TransactionsController` para el patrón `[ApiController]`/`[Authorize]`/`ProducesResponseType`/`ApiResponse`. Las companies son catálogo global: el controller **no** necesita `IUserIdEncryptor` ni `UserId`.
- **Tests E2E**: reglas obligatorias en `src/backend/AGENTS.md` (un fichero por endpoint, ≥1 exhaustivo con persistencia física, 401/400/404/409). Bases: `IntegrationTestBase` (envelope `ApiEnvelope<T>`), fixtures `MySqlDatabaseFixture` (`ConnectionString`, `ResetAsync`), `BigSchoolWebAppFactory`, `IntegrationCollection`.

---

## File Structure

**Domain** (`src/backend/src/BigSchool.Domain/`)
- Modificar `Entities/Company.cs` (stub vacío → AR completo).
- Crear `Entities/Valuation.cs` (entidad hija).
- Crear `Exceptions/DuplicateValuationDomainException.cs`, `Exceptions/DuplicateTickerDomainException.cs`.

**Infrastructure** (`src/backend/src/BigSchool.Infrastructure/`)
- Crear `Persistence/Configurations/CompanyConfiguration.cs`, `Persistence/Configurations/ValuationConfiguration.cs`.
- Modificar `Persistence/BigSchoolDbContext.cs` (DbSets), `Persistence/Extensions/SeedDataExtensions.cs` (seed), generar migración `CreateCompanies`.
- Crear `Persistence/Repositories/CompanyRepository.cs`.

**Application** (`src/backend/src/BigSchool.Application/`)
- Crear `Interfaces/Repositories/ICompanyRepository.cs`.
- Crear `DTOs/Investments/{CompanyDto,ValuationDto,CompanyListItemDto,ValuationListItemDto}.cs`.
- Crear `Commands/Investments/CreateCompany/*`, `Commands/Investments/AddValuation/*`.
- Crear `Queries/Investments/{GetCompanies,GetCompanyById,GetCompanyValuations}/*`.

**WebApi** (`src/backend/src/BigSchool.WebApi/`)
- Crear `Controllers/CompaniesController.cs`.

**Tests**
- `tests/BigSchool.Domain.Tests/Entities/CompanyTests.cs`.
- `tests/BigSchool.Application.Tests/Commands/Investments/{CreateCompanyCommandHandlerTests,AddValuationCommandHandlerTests}.cs`.
- `tests/BigSchool.Integration.Tests/Investments/{CompanyEndpointTestBase,CreateCompanyTests,AddValuationTests,GetCompaniesTests,GetCompanyByIdTests,GetCompanyValuationsTests}.cs`.
- Modificar `tests/BigSchool.Integration.Tests/Fixtures/MySqlDatabaseFixture.cs` (`ResetAsync`).

---

### Task 1: Dominio — `Company` (AR) + `Valuation` (hija) + excepción de valoración duplicada

**Files:**
- Modify: `src/backend/src/BigSchool.Domain/Entities/Company.cs`
- Create: `src/backend/src/BigSchool.Domain/Entities/Valuation.cs`
- Create: `src/backend/src/BigSchool.Domain/Exceptions/DuplicateValuationDomainException.cs`
- Test: `src/backend/tests/BigSchool.Domain.Tests/Entities/CompanyTests.cs`

- [x] **Step 1: Escribir el test de dominio (falla al no existir el modelo)**

Crear `src/backend/tests/BigSchool.Domain.Tests/Entities/CompanyTests.cs`:

```csharp
using BigSchool.Domain.Entities;
using BigSchool.Domain.Enums;
using BigSchool.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace BigSchool.Domain.Tests.Entities;

public class CompanyTests
{
    private static readonly DateOnly D1 = new(2026, 1, 2);
    private static readonly DateOnly D2 = new(2026, 3, 2);

    private static Company NewCompany()
        => Company.Create("Apple Inc.", "aapl", "Technology", "NASDAQ", Currency.USD);

    [Fact]
    public void Create_NormalizesTicker_AndSetsActive()
    {
        var company = NewCompany();

        company.Name.Should().Be("Apple Inc.");
        company.Ticker.Should().Be("AAPL");            // normaliza a mayúsculas
        company.Sector.Should().Be("Technology");
        company.Market.Should().Be("NASDAQ");
        company.Currency.Should().Be(Currency.USD);
        company.IdStatus.Should().Be(EntityStatus.Active);
        company.Valuations.Should().BeEmpty();
    }

    [Theory]
    [InlineData("", "AAPL")]
    [InlineData("Apple", "")]
    public void Create_WithEmptyNameOrTicker_Throws(string name, string ticker)
    {
        var act = () => Company.Create(name, ticker, null, null, Currency.USD);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddValuation_AddsPriceInCompanyCurrency()
    {
        var company = NewCompany();

        var valuation = company.AddValuation(195.50m, D1, "manual");

        company.Valuations.Should().ContainSingle();
        valuation.Price.Amount.Should().Be(195.50m);
        valuation.Price.Currency.Should().Be(Currency.USD);   // hereda la moneda de la empresa
        valuation.Date.Should().Be(D1);
        valuation.Source.Should().Be("manual");
        valuation.IdStatus.Should().Be(EntityStatus.Active);
    }

    [Fact]
    public void AddValuation_NonPositivePrice_Throws()
    {
        var company = NewCompany();

        var act = () => company.AddValuation(0m, D1, null);

        act.Should().Throw<ArgumentException>().WithMessage("*precio*");
    }

    [Fact]
    public void AddValuation_DuplicateDate_Throws()
    {
        var company = NewCompany();
        company.AddValuation(195.50m, D1, null);

        var act = () => company.AddValuation(200m, D1, null);

        act.Should().Throw<DuplicateValuationDomainException>();
    }

    [Fact]
    public void AddValuation_DifferentDates_BothAdded()
    {
        var company = NewCompany();
        company.AddValuation(195.50m, D1, null);
        company.AddValuation(210m, D2, null);

        company.Valuations.Should().HaveCount(2);
    }
}
```

- [x] **Step 2: Ejecutar el test para verificar que falla a compilar**

Run: `dotnet test src/backend/tests/BigSchool.Domain.Tests/BigSchool.Domain.Tests.csproj --filter "FullyQualifiedName~CompanyTests"`
Expected: FAIL de compilación (`Company.Create`, `AddValuation`, `Valuation`, `DuplicateValuationDomainException` no existen).

- [x] **Step 3: Crear la excepción de valoración duplicada**

Crear `src/backend/src/BigSchool.Domain/Exceptions/DuplicateValuationDomainException.cs`:

```csharp
namespace BigSchool.Domain.Exceptions;

public class DuplicateValuationDomainException : ConflictException
{
    public DuplicateValuationDomainException(string ticker, DateOnly date)
        : base("DUPLICATE_VALUATION",
            $"Ya existe una valoración de '{ticker}' para la fecha {date:yyyy-MM-dd}.") { }
}
```

- [x] **Step 4: Crear la entidad hija `Valuation`**

Crear `src/backend/src/BigSchool.Domain/Entities/Valuation.cs`:

```csharp
using BigSchool.Domain.Enums;
using BigSchool.Domain.ValueObjects;

namespace BigSchool.Domain.Entities;

/// <summary>
/// Cotización puntual de una Company (entidad hija). Price es un Money en la moneda de la empresa;
/// no lleva conversión a base de usuario (el catálogo es global). Alta vía Company.AddValuation.
/// </summary>
public class Valuation : BaseEntity
{
    public int IdValuation { get; private set; }
    public Money Price { get; private set; } = null!;
    public DateOnly Date { get; private set; }
    public string? Source { get; private set; }
    public EntityStatus IdStatus { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    protected Valuation() { } // EF Core

    private Valuation(Money price, DateOnly date, string? source, EntityStatus idStatus, DateTime createdAt)
    {
        Price = price;
        Date = date;
        Source = source;
        IdStatus = idStatus;
        CreatedAt = createdAt;
    }

    internal static Valuation Create(Money price, DateOnly date, string? source)
    {
        return new Valuation(price, date, source, EntityStatus.Active, DateTime.UtcNow);
    }
}
```

- [x] **Step 5: Implementar el AR `Company`**

Reemplazar el contenido de `src/backend/src/BigSchool.Domain/Entities/Company.cs`:

```csharp
using BigSchool.Domain.Enums;
using BigSchool.Domain.Exceptions;
using BigSchool.Domain.ValueObjects;

namespace BigSchool.Domain.Entities;

/// <summary>
/// Empresa cotizada (Aggregate Root global, sin IdUser). Posee Valuation como entidad hija.
/// </summary>
public class Company : BaseEntity, IAggregateRoot
{
    public int IdCompany { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Ticker { get; private set; } = string.Empty;
    public string? Sector { get; private set; }
    public string? Market { get; private set; }
    public Currency Currency { get; private set; }
    public EntityStatus IdStatus { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private readonly List<Valuation> _valuations = [];
    public IReadOnlyCollection<Valuation> Valuations => _valuations.AsReadOnly();

    protected Company() { } // EF Core

    private Company(string name, string ticker, string? sector, string? market,
        Currency currency, EntityStatus idStatus, DateTime createdAt)
    {
        Name = name;
        Ticker = ticker;
        Sector = sector;
        Market = market;
        Currency = currency;
        IdStatus = idStatus;
        CreatedAt = createdAt;
    }

    public static Company Create(string name, string ticker, string? sector, string? market, Currency currency)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre es obligatorio.", nameof(name));
        if (string.IsNullOrWhiteSpace(ticker))
            throw new ArgumentException("El ticker es obligatorio.", nameof(ticker));

        return new Company(
            name.Trim(),
            ticker.Trim().ToUpperInvariant(),
            string.IsNullOrWhiteSpace(sector) ? null : sector.Trim(),
            string.IsNullOrWhiteSpace(market) ? null : market.Trim(),
            currency,
            EntityStatus.Active,
            DateTime.UtcNow);
    }

    public Valuation AddValuation(decimal price, DateOnly date, string? source)
    {
        if (price <= 0m)
            throw new ArgumentException("El precio debe ser mayor que cero.", nameof(price));

        if (_valuations.Any(v => v.Date == date && v.IdStatus != EntityStatus.Deleted))
            throw new DuplicateValuationDomainException(Ticker, date);

        var valuation = Valuation.Create(Money.Create(price, Currency), date, source?.Trim());
        _valuations.Add(valuation);
        return valuation;
    }
}
```

> **Nota**: `Money.Create` redondea a 2 decimales (escala del VO). Para precios de inversión la columna es `decimal(18,4)`; en la práctica los precios sembrados/probados usan ≤2 decimales, así que el redondeo no afecta. Si en el futuro se requieren 4 decimales reales en el dominio, se ampliará `Money.Scale` (fuera del alcance de 3A).

- [x] **Step 6: Ejecutar el test para verificar que pasa**

Run: `dotnet test src/backend/tests/BigSchool.Domain.Tests/BigSchool.Domain.Tests.csproj --filter "FullyQualifiedName~CompanyTests"`
Expected: PASS los 7 casos (2 del Theory + 5 Facts).

- [x] **Step 7: Commit**

```bash
git add src/backend/src/BigSchool.Domain/Entities/Company.cs src/backend/src/BigSchool.Domain/Entities/Valuation.cs src/backend/src/BigSchool.Domain/Exceptions/DuplicateValuationDomainException.cs src/backend/tests/BigSchool.Domain.Tests/Entities/CompanyTests.cs
git commit -m "feat: Company (AR) + Valuation (hija) con AddValuation y unicidad de fecha

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 2: Infraestructura — EF configs, DbSets, seed y migración `CreateCompanies`

**Files:**
- Create: `src/backend/src/BigSchool.Infrastructure/Persistence/Configurations/CompanyConfiguration.cs`
- Create: `src/backend/src/BigSchool.Infrastructure/Persistence/Configurations/ValuationConfiguration.cs`
- Modify: `src/backend/src/BigSchool.Infrastructure/Persistence/BigSchoolDbContext.cs`
- Modify: `src/backend/src/BigSchool.Infrastructure/Persistence/Extensions/SeedDataExtensions.cs`
- Create (generado): `src/backend/src/BigSchool.Infrastructure/Persistence/Migrations/*_CreateCompanies.cs`

- [x] **Step 1: Crear `ValuationConfiguration`**

Crear `src/backend/src/BigSchool.Infrastructure/Persistence/Configurations/ValuationConfiguration.cs`:

```csharp
using BigSchool.Domain.Entities;
using BigSchool.Domain.Enums;
using BigSchool.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BigSchool.Infrastructure.Persistence.Configurations;
public class ValuationConfiguration : IEntityTypeConfiguration<Valuation>
{
    public void Configure(EntityTypeBuilder<Valuation> builder)
    {
        builder.ToTable("Valuations");
        builder.HasKey(v => v.IdValuation);

        ConfigureProperties(builder);
        ConfigureRelationships(builder);
        ConfigureIndexes(builder);
        ConfigureFilters(builder);
        
        builder.Ignore(v => v.DomainEvents);
    }

    private static void ConfigureProperties(EntityTypeBuilder<Valuation> builder)
    {
        builder.Property(v => v.IdValuation).ValueGeneratedOnAdd();
        builder.Property(v => v.Date).HasColumnType("date").IsRequired();
        builder.Property(v => v.Source).HasMaxLength(100);
        builder.Property(v => v.IdStatus).IsRequired().HasDefaultValue(EntityStatus.Active).HasConversion<short>();
        builder.Property(v => v.CreatedAt).IsRequired();
        builder.Property(v => v.UpdatedAt);

        // Shadow FK IdCompany (la relación la declara CompanyConfiguration).
        builder.Property<int>("IdCompany");

    }

    private static void ConfigureRelationships(EntityTypeBuilder<Valuation> builder)
    {
        builder.OwnsOne(v => v.Price, p =>
        {
            p.Property(m => m.Amount).HasColumnName("Price").HasColumnType("decimal(18,4)");
            p.Property(m => m.Currency).HasColumnName("PriceCurrency")
                .HasConversion(CurrencyConverter.CharIso).HasColumnType("char(3)");
        });
        builder.Navigation(v => v.Price).IsRequired();

    }

    private static void ConfigureIndexes(EntityTypeBuilder<Valuation> builder)
    {
        builder.HasIndex("IdCompany", nameof(Valuation.Date)).IsUnique();
    }

    private static void ConfigureFilters(EntityTypeBuilder<Valuation> builder)
    {
        builder.HasQueryFilter(v => v.IdStatus != EntityStatus.Deleted);
    }
}
```

- [x] **Step 2: Crear `CompanyConfiguration`**

Crear `src/backend/src/BigSchool.Infrastructure/Persistence/Configurations/CompanyConfiguration.cs`:

```csharp
using BigSchool.Domain.Entities;
using BigSchool.Domain.Enums;
using BigSchool.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BigSchool.Infrastructure.Persistence.Configurations;
public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Companies");
        builder.HasKey(c => c.IdCompany);

        ConfigureProperties(builder);
        ConfigureRelationships(builder);
        ConfigureIndexes(builder);
        ConfigureFilters(builder);

        builder.Ignore(c => c.DomainEvents);
    }
    private void ConfigureProperties(EntityTypeBuilder<Company> builder)
    {
        builder.Property(c => c.IdCompany).ValueGeneratedOnAdd();
        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Ticker).IsRequired().HasMaxLength(10);
        builder.Property(c => c.Sector).HasMaxLength(100);
        builder.Property(c => c.Market).HasMaxLength(50);
        builder.Property(c => c.Currency).IsRequired()
           .HasConversion(CurrencyConverter.CharIso).HasColumnType("char(3)").HasDefaultValueSql("'EUR'");
        builder.Property(c => c.IdStatus).IsRequired().HasDefaultValue(EntityStatus.Active).HasConversion<short>();
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt);
    }

    private void ConfigureRelationships(EntityTypeBuilder<Company> builder)
    {
        builder.HasMany(c => c.Valuations)
            .WithOne()
            .HasForeignKey("IdCompany")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.Valuations).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
    private void ConfigureIndexes(EntityTypeBuilder<Company> builder)
    {
        builder.HasIndex(c => c.Ticker).IsUnique();
    }


    private void ConfigureFilters(EntityTypeBuilder<Company> builder)
    {
        builder.HasQueryFilter(c => c.IdStatus != EntityStatus.Deleted);
    }
}
```

- [x] **Step 3: Registrar los DbSets en `BigSchoolDbContext`**

En `src/backend/src/BigSchool.Infrastructure/Persistence/BigSchoolDbContext.cs`, sustituir el bloque de comentarios de Portfolio/entidades futuras. Reemplazar:

```csharp
    // Aggregate Roots — acceso principal
    public DbSet<User> Users => Set<User>();
    // TODO: Task futura — Portfolio no está implementado aún
    // public DbSet<Portfolio> Portfolios => Set<Portfolio>();

    // Entidades hijas — DbSet necesario para EF Core migrations/queries
    // El acceso de escritura se hace siempre a través del Aggregate Root
    public DbSet<SubCategory> SubCategories => Set<SubCategory>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    // TODO: Tasks futuras — Entidades no implementadas aún
    // public DbSet<Holding> Holdings => Set<Holding>();
    // public DbSet<RagDocument> RagDocuments => Set<RagDocument>();
```

por:

```csharp
    // Aggregate Roots — acceso principal
    public DbSet<User> Users => Set<User>();
    public DbSet<Company> Companies => Set<Company>();
    // TODO: Task futura (Plan 3B) — Portfolio no está implementado aún
    // public DbSet<Portfolio> Portfolios => Set<Portfolio>();

    // Entidades hijas — DbSet necesario para EF Core migrations/queries
    // El acceso de escritura se hace siempre a través del Aggregate Root
    public DbSet<SubCategory> SubCategories => Set<SubCategory>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Valuation> Valuations => Set<Valuation>();
    // TODO: Tasks futuras — Entidades no implementadas aún
    // public DbSet<Holding> Holdings => Set<Holding>();
    // public DbSet<RagDocument> RagDocuments => Set<RagDocument>();
```

Y en `OnModelCreating`, tras `modelBuilder.SeedExchangeRates();`, añadir `modelBuilder.SeedCompanies();`:

```csharp
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BigSchoolDbContext).Assembly);
        modelBuilder.SeedSubCategories();
        modelBuilder.SeedExchangeRates();
        modelBuilder.SeedCompanies();
```

- [x] **Step 4: Añadir el seed de empresas y valoraciones**

En `src/backend/src/BigSchool.Infrastructure/Persistence/Extensions/SeedDataExtensions.cs`, añadir este método dentro de la clase `SeedDataExtensions` (después de `SeedExchangeRates`):

```csharp
    public static ModelBuilder SeedCompanies(this ModelBuilder modelBuilder)
    {
        // Catálogo global de demo (4 empresas en 3 monedas). IDs fijos 1-4 → el fixture de tests
        // los preserva y limpia solo las creadas por tests (IdCompany > 4).
        modelBuilder.Entity<Domain.Entities.Company>().HasData(
            new { IdCompany = 1, Name = "Apple Inc.", Ticker = "AAPL", Sector = (string?)"Technology", Market = (string?)"NASDAQ", Currency = Currency.USD, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdCompany = 2, Name = "Microsoft Corp.", Ticker = "MSFT", Sector = (string?)"Technology", Market = (string?)"NASDAQ", Currency = Currency.USD, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdCompany = 3, Name = "Banco Santander", Ticker = "SAN", Sector = (string?)"Financials", Market = (string?)"BME", Currency = Currency.EUR, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdCompany = 4, Name = "Shell plc", Ticker = "SHEL", Sector = (string?)"Energy", Market = (string?)"LSE", Currency = Currency.GBP, IdStatus = EntityStatus.Active, CreatedAt = SeedDate }
        );

        // Valuations: fila base (FK shadow IdCompany incluida en el objeto anónimo).
        modelBuilder.Entity<Domain.Entities.Valuation>().HasData(
            new { IdValuation = 1, IdCompany = 1, Date = new DateOnly(2026, 1, 2), Source = (string?)"seed", IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdValuation = 2, IdCompany = 1, Date = new DateOnly(2026, 3, 2), Source = (string?)"seed", IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdValuation = 3, IdCompany = 2, Date = new DateOnly(2026, 1, 2), Source = (string?)"seed", IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdValuation = 4, IdCompany = 2, Date = new DateOnly(2026, 3, 2), Source = (string?)"seed", IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdValuation = 5, IdCompany = 3, Date = new DateOnly(2026, 1, 2), Source = (string?)"seed", IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdValuation = 6, IdCompany = 3, Date = new DateOnly(2026, 3, 2), Source = (string?)"seed", IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdValuation = 7, IdCompany = 4, Date = new DateOnly(2026, 1, 2), Source = (string?)"seed", IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdValuation = 8, IdCompany = 4, Date = new DateOnly(2026, 3, 2), Source = (string?)"seed", IdStatus = EntityStatus.Active, CreatedAt = SeedDate }
        );

        // Owned type Price (Money): FK shadow del owned = "{Owner}{OwnerPk}" = "ValuationIdValuation".
        modelBuilder.Entity<Domain.Entities.Valuation>().OwnsOne(v => v.Price).HasData(
            new { ValuationIdValuation = 1, Amount = 195.0000m, Currency = Currency.USD },
            new { ValuationIdValuation = 2, Amount = 210.0000m, Currency = Currency.USD },
            new { ValuationIdValuation = 3, Amount = 420.0000m, Currency = Currency.USD },
            new { ValuationIdValuation = 4, Amount = 440.0000m, Currency = Currency.USD },
            new { ValuationIdValuation = 5, Amount = 4.5000m, Currency = Currency.EUR },
            new { ValuationIdValuation = 6, Amount = 4.8000m, Currency = Currency.EUR },
            new { ValuationIdValuation = 7, Amount = 28.0000m, Currency = Currency.GBP },
            new { ValuationIdValuation = 8, Amount = 30.0000m, Currency = Currency.GBP }
        );

        return modelBuilder;
    }
```

> Si `dotnet ef migrations add` se queja del nombre de la FK shadow del owned type, el mensaje indicará el nombre esperado; ajustar `ValuationIdValuation` a lo que reporte (el valor por convención de EF es `ValuationIdValuation`).

- [x] **Step 5: Generar la migración**

Run:
```bash
dotnet ef migrations add CreateCompanies --project src/backend/src/BigSchool.Infrastructure/BigSchool.Infrastructure.csproj --startup-project src/backend/src/BigSchool.WebApi/BigSchool.WebApi.csproj --output-dir Persistence/Migrations
```
Expected: se genera `*_CreateCompanies.cs` con `CreateTable("Companies")`, `CreateTable("Valuations")`, índices únicos `(Ticker)` y `(IdCompany, Date)`, y los `InsertData` del seed (4 companies + 8 valuations). 0 errores.

- [x] **Step 6: Compilar Infrastructure**

Run: `dotnet build src/backend/src/BigSchool.Infrastructure/BigSchool.Infrastructure.csproj -c Release`
Expected: BUILD SUCCEEDED (puede haber warning MSB3277 pre-existente de versiones EF, ignorable).

- [x] **Step 7: Commit**

```bash
git add src/backend/src/BigSchool.Infrastructure/Persistence/Configurations/CompanyConfiguration.cs src/backend/src/BigSchool.Infrastructure/Persistence/Configurations/ValuationConfiguration.cs src/backend/src/BigSchool.Infrastructure/Persistence/BigSchoolDbContext.cs src/backend/src/BigSchool.Infrastructure/Persistence/Extensions/SeedDataExtensions.cs src/backend/src/BigSchool.Infrastructure/Persistence/Migrations/
git commit -m "feat: EF config, DbSets, seed y migración CreateCompanies (Companies + Valuations)

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 3: Repositorio — `ICompanyRepository` + `CompanyRepository`

**Files:**
- Create: `src/backend/src/BigSchool.Application/Interfaces/Repositories/ICompanyRepository.cs`
- Create: `src/backend/src/BigSchool.Infrastructure/Persistence/Repositories/CompanyRepository.cs`

- [x] **Step 1: Crear la interfaz del repositorio**

Crear `src/backend/src/BigSchool.Application/Interfaces/Repositories/ICompanyRepository.cs`:

```csharp
using BigSchool.Domain.Entities;

namespace BigSchool.Application.Interfaces.Repositories;

public interface ICompanyRepository : IRepository<Company, int>
{
    Task<Company?> GetByTickerAsync(string ticker, CancellationToken cancellationToken = default);
    Task<Company?> GetByIdWithValuationsAsync(int id, CancellationToken cancellationToken = default);
}
```

- [x] **Step 2: Crear la implementación**

Crear `src/backend/src/BigSchool.Infrastructure/Persistence/Repositories/CompanyRepository.cs`:

```csharp
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BigSchool.Infrastructure.Persistence.Repositories;

public class CompanyRepository : EFRepository<Company, int>, ICompanyRepository
{
    public CompanyRepository(BigSchoolDbContext context) : base(context)
    {
    }

    public async Task<Company?> GetByIdWithValuationsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await Context.Companies
            .Include(c => c.Valuations)
            .FirstOrDefaultAsync(c => c.IdCompany == id, cancellationToken);
    }

    public async Task<Company?> GetByTickerAsync(string ticker, CancellationToken cancellationToken = default)
    {
        var normalized = ticker.Trim().ToUpperInvariant();
        return await Context.Companies.FirstOrDefaultAsync(c => c.Ticker == normalized, cancellationToken);
    }
}
```

- [x] **Step 3: Compilar**

Run: `dotnet build src/backend/src/BigSchool.Infrastructure/BigSchool.Infrastructure.csproj -c Release`
Expected: BUILD SUCCEEDED. (No hace falta DI: Autofac auto-registra `CompanyRepository` como `ICompanyRepository`.)

- [x] **Step 4: Commit**

```bash
git add src/backend/src/BigSchool.Application/Interfaces/Repositories/ICompanyRepository.cs src/backend/src/BigSchool.Infrastructure/Persistence/Repositories/CompanyRepository.cs
git commit -m "feat: ICompanyRepository + CompanyRepository (GetByTicker, GetByIdWithValuations)

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 4: Command `CreateCompany` + `CompanyDto` + validator + excepción de ticker duplicado

**Files:**
- Create: `src/backend/src/BigSchool.Domain/Exceptions/DuplicateTickerDomainException.cs`
- Create: `src/backend/src/BigSchool.Application/DTOs/Investments/CompanyDto.cs`
- Create: `src/backend/src/BigSchool.Application/Commands/Investments/CreateCompany/CreateCompanyCommand.cs`
- Create: `src/backend/src/BigSchool.Application/Commands/Investments/CreateCompany/CreateCompanyCommandValidator.cs`
- Create: `src/backend/src/BigSchool.Application/Commands/Investments/CreateCompany/CreateCompanyCommandHandler.cs`
- Test: `src/backend/tests/BigSchool.Application.Tests/Commands/Investments/CreateCompanyCommandHandlerTests.cs`

- [x] **Step 1: Escribir el test del handler (falla al no existir)**

Crear `src/backend/tests/BigSchool.Application.Tests/Commands/Investments/CreateCompanyCommandHandlerTests.cs`:

```csharp
using BigSchool.Application.Commands.Investments.CreateCompany;
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Domain.Entities;
using BigSchool.Domain.Enums;
using BigSchool.Domain.Exceptions;
using BigSchool.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace BigSchool.Application.Tests.Commands.Investments;

public class CreateCompanyCommandHandlerTests
{
    private readonly Mock<ICompanyRepository> _repo = new();
    private readonly CreateCompanyCommandHandler _handler;

    public CreateCompanyCommandHandlerTests()
    {
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<bool>())).ReturnsAsync(1);
        _repo.Setup(r => r.UnitOfWork).Returns(uow.Object);
        _handler = new CreateCompanyCommandHandler(_repo.Object);
    }

    [Fact]
    public async Task Handle_NewTicker_CreatesCompany()
    {
        _repo.Setup(r => r.GetByTickerAsync("AAPL", It.IsAny<CancellationToken>())).ReturnsAsync((Company?)null);

        var command = new CreateCompanyCommand("Apple Inc.", "AAPL", "Technology", "NASDAQ", Currency.USD);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.Ticker.Should().Be("AAPL");
        result.Currency.Should().Be(Currency.USD);
        _repo.Verify(r => r.AddAsync(It.IsAny<Company>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicateTicker_ThrowsConflict()
    {
        _repo.Setup(r => r.GetByTickerAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Company.Create("Apple Inc.", "AAPL", null, null, Currency.USD));

        var command = new CreateCompanyCommand("Apple Inc.", "AAPL", null, null, Currency.USD);
        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<DuplicateTickerDomainException>();
        _repo.Verify(r => r.AddAsync(It.IsAny<Company>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
```

- [x] **Step 2: Ejecutar para verificar que falla a compilar**

Run: `dotnet test src/backend/tests/BigSchool.Application.Tests/BigSchool.Application.Tests.csproj --filter "FullyQualifiedName~CreateCompanyCommandHandlerTests"`
Expected: FAIL de compilación (tipos no existen).

- [x] **Step 3: Crear la excepción de ticker duplicado**

Crear `src/backend/src/BigSchool.Domain/Exceptions/DuplicateTickerDomainException.cs`:

```csharp
namespace BigSchool.Domain.Exceptions;

public class DuplicateTickerDomainException : ConflictException
{
    public DuplicateTickerDomainException(string ticker)
        : base("DUPLICATE_TICKER", $"Ya existe una empresa con el ticker '{ticker}'.") { }
}
```

- [x] **Step 4: Crear el `CompanyDto`**

Crear `src/backend/src/BigSchool.Application/DTOs/Investments/CompanyDto.cs`:

```csharp
using BigSchool.Domain.Enums;

namespace BigSchool.Application.DTOs.Investments;

/// <summary>DTO de respuesta de comando (construido desde el dominio): Currency tipado.</summary>
public record CompanyDto(int IdCompany, string Name, string Ticker, string? Sector, string? Market, Currency Currency);
```

- [x] **Step 5: Crear el command y el validator**

Crear `src/backend/src/BigSchool.Application/Commands/Investments/CreateCompany/CreateCompanyCommand.cs`:

```csharp
using BigSchool.Application.DTOs.Investments;
using BigSchool.Domain.Enums;
using MediatR;

namespace BigSchool.Application.Commands.Investments.CreateCompany;

public record CreateCompanyCommand(string Name, string Ticker, string? Sector, string? Market, Currency Currency)
    : IRequest<CompanyDto>;
```

Crear `src/backend/src/BigSchool.Application/Commands/Investments/CreateCompany/CreateCompanyCommandValidator.cs`:

```csharp
using FluentValidation;

namespace BigSchool.Application.Commands.Investments.CreateCompany;

public class CreateCompanyCommandValidator : AbstractValidator<CreateCompanyCommand>
{
    public CreateCompanyCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Ticker).NotEmpty().MaximumLength(10);
        RuleFor(x => x.Currency).IsInEnum();
        RuleFor(x => x.Sector).MaximumLength(100);
        RuleFor(x => x.Market).MaximumLength(50);
    }
}
```

- [x] **Step 6: Crear el handler**

Crear `src/backend/src/BigSchool.Application/Commands/Investments/CreateCompany/CreateCompanyCommandHandler.cs`:

```csharp
using BigSchool.Application.DTOs.Investments;
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Domain.Entities;
using BigSchool.Domain.Exceptions;
using MediatR;

namespace BigSchool.Application.Commands.Investments.CreateCompany;

public class CreateCompanyCommandHandler : IRequestHandler<CreateCompanyCommand, CompanyDto>
{
    private readonly ICompanyRepository _companyRepository;

    public CreateCompanyCommandHandler(ICompanyRepository companyRepository) => _companyRepository = companyRepository;

    public async Task<CompanyDto> Handle(CreateCompanyCommand request, CancellationToken cancellationToken)
    {
        var existing = await _companyRepository.GetByTickerAsync(request.Ticker, cancellationToken);
        if (existing is not null)
            throw new DuplicateTickerDomainException(request.Ticker.Trim().ToUpperInvariant());

        var company = Company.Create(request.Name, request.Ticker, request.Sector, request.Market, request.Currency);
        await _companyRepository.AddAsync(company, cancellationToken);
        await _companyRepository.UnitOfWork.SaveChangesAsync();

        return new CompanyDto(company.IdCompany, company.Name, company.Ticker,
            company.Sector, company.Market, company.Currency);
    }
}
```

- [x] **Step 7: Ejecutar el test**

Run: `dotnet test src/backend/tests/BigSchool.Application.Tests/BigSchool.Application.Tests.csproj --filter "FullyQualifiedName~CreateCompanyCommandHandlerTests"`
Expected: PASS los 2 tests.

- [x] **Step 8: Commit**

```bash
git add src/backend/src/BigSchool.Domain/Exceptions/DuplicateTickerDomainException.cs src/backend/src/BigSchool.Application/DTOs/Investments/CompanyDto.cs src/backend/src/BigSchool.Application/Commands/Investments/CreateCompany/ src/backend/tests/BigSchool.Application.Tests/Commands/Investments/CreateCompanyCommandHandlerTests.cs
git commit -m "feat: CreateCompany command (handler, validator, CompanyDto, 409 ticker duplicado)

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 5: Command `AddValuation` + `ValuationDto` + validator

**Files:**
- Create: `src/backend/src/BigSchool.Application/DTOs/Investments/ValuationDto.cs`
- Create: `src/backend/src/BigSchool.Application/Commands/Investments/AddValuation/AddValuationCommand.cs`
- Create: `src/backend/src/BigSchool.Application/Commands/Investments/AddValuation/AddValuationCommandValidator.cs`
- Create: `src/backend/src/BigSchool.Application/Commands/Investments/AddValuation/AddValuationCommandHandler.cs`
- Test: `src/backend/tests/BigSchool.Application.Tests/Commands/Investments/AddValuationCommandHandlerTests.cs`

- [x] **Step 1: Escribir el test del handler (falla al no existir)**

Crear `src/backend/tests/BigSchool.Application.Tests/Commands/Investments/AddValuationCommandHandlerTests.cs`:

```csharp
using BigSchool.Application.Commands.Investments.AddValuation;
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Domain.Entities;
using BigSchool.Domain.Enums;
using BigSchool.Domain.Exceptions;
using BigSchool.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace BigSchool.Application.Tests.Commands.Investments;

public class AddValuationCommandHandlerTests
{
    private readonly Mock<ICompanyRepository> _repo = new();
    private readonly AddValuationCommandHandler _handler;

    public AddValuationCommandHandlerTests()
    {
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<bool>())).ReturnsAsync(1);
        _repo.Setup(r => r.UnitOfWork).Returns(uow.Object);
        _handler = new AddValuationCommandHandler(_repo.Object);
    }

    [Fact]
    public async Task Handle_ExistingCompany_AddsValuationInCompanyCurrency()
    {
        _repo.Setup(r => r.GetByIdWithValuationsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Company.Create("Apple Inc.", "AAPL", null, null, Currency.USD));

        var command = new AddValuationCommand(1, 195.50m, new DateOnly(2026, 1, 2), "manual");
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IdCompany.Should().Be(1);
        result.Price.Should().Be(195.50m);
        result.Currency.Should().Be(Currency.USD);   // moneda de la empresa
        result.Date.Should().Be(new DateOnly(2026, 1, 2));
    }

    [Fact]
    public async Task Handle_UnknownCompany_ThrowsNotFound()
    {
        _repo.Setup(r => r.GetByIdWithValuationsAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((Company?)null);

        var command = new AddValuationCommand(99, 195.50m, new DateOnly(2026, 1, 2), null);
        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
```

- [x] **Step 2: Ejecutar para verificar que falla a compilar**

Run: `dotnet test src/backend/tests/BigSchool.Application.Tests/BigSchool.Application.Tests.csproj --filter "FullyQualifiedName~AddValuationCommandHandlerTests"`
Expected: FAIL de compilación.

- [x] **Step 3: Crear el `ValuationDto`**

Crear `src/backend/src/BigSchool.Application/DTOs/Investments/ValuationDto.cs`:

```csharp
using BigSchool.Domain.Enums;

namespace BigSchool.Application.DTOs.Investments;

/// <summary>DTO de respuesta de comando (construido desde el dominio): Currency tipado.</summary>
public record ValuationDto(int IdValuation, int IdCompany, decimal Price, Currency Currency, DateOnly Date, string? Source);
```

- [x] **Step 4: Crear el command y el validator**

Crear `src/backend/src/BigSchool.Application/Commands/Investments/AddValuation/AddValuationCommand.cs`:

```csharp
using BigSchool.Application.DTOs.Investments;
using MediatR;

namespace BigSchool.Application.Commands.Investments.AddValuation;

public record AddValuationCommand(int IdCompany, decimal Price, DateOnly Date, string? Source)
    : IRequest<ValuationDto>;
```

Crear `src/backend/src/BigSchool.Application/Commands/Investments/AddValuation/AddValuationCommandValidator.cs`:

```csharp
using FluentValidation;

namespace BigSchool.Application.Commands.Investments.AddValuation;

public class AddValuationCommandValidator : AbstractValidator<AddValuationCommand>
{
    public AddValuationCommandValidator()
    {
        RuleFor(x => x.Price).GreaterThan(0).WithMessage("El precio debe ser mayor que cero.");
        RuleFor(x => x.Date).NotEmpty().WithMessage("La fecha es obligatoria.");
        RuleFor(x => x.Source).MaximumLength(100);
    }
}
```

- [x] **Step 5: Crear el handler**

Crear `src/backend/src/BigSchool.Application/Commands/Investments/AddValuation/AddValuationCommandHandler.cs`:

```csharp
using BigSchool.Application.DTOs.Investments;
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Domain.Entities;
using BigSchool.Domain.Exceptions;
using MediatR;

namespace BigSchool.Application.Commands.Investments.AddValuation;

public class AddValuationCommandHandler : IRequestHandler<AddValuationCommand, ValuationDto>
{
    private readonly ICompanyRepository _companyRepository;

    public AddValuationCommandHandler(ICompanyRepository companyRepository) => _companyRepository = companyRepository;

    public async Task<ValuationDto> Handle(AddValuationCommand request, CancellationToken cancellationToken)
    {
        var company = await _companyRepository.GetByIdWithValuationsAsync(request.IdCompany, cancellationToken)
            ?? throw new NotFoundException(nameof(Company), request.IdCompany);

        var valuation = company.AddValuation(request.Price, request.Date, request.Source);
        await _companyRepository.UnitOfWork.SaveChangesAsync();

        return new ValuationDto(valuation.IdValuation, request.IdCompany, valuation.Price.Amount,
            valuation.Price.Currency, valuation.Date, valuation.Source);
    }
}
```

- [x] **Step 6: Ejecutar el test**

Run: `dotnet test src/backend/tests/BigSchool.Application.Tests/BigSchool.Application.Tests.csproj --filter "FullyQualifiedName~AddValuationCommandHandlerTests"`
Expected: PASS los 2 tests.

- [x] **Step 7: Commit**

```bash
git add src/backend/src/BigSchool.Application/DTOs/Investments/ValuationDto.cs src/backend/src/BigSchool.Application/Commands/Investments/AddValuation/ src/backend/tests/BigSchool.Application.Tests/Commands/Investments/AddValuationCommandHandlerTests.cs
git commit -m "feat: AddValuation command (handler, validator, ValuationDto, 404 empresa, 409 fecha duplicada)

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 6: Queries Dapper — `GetCompanies`, `GetCompanyById`, `GetCompanyValuations` + DTOs

**Files:**
- Create: `src/backend/src/BigSchool.Application/DTOs/Investments/CompanyListItemDto.cs`
- Create: `src/backend/src/BigSchool.Application/DTOs/Investments/ValuationListItemDto.cs`
- Create: `src/backend/src/BigSchool.Application/Queries/Investments/GetCompanies/GetCompaniesQuery.cs`
- Create: `src/backend/src/BigSchool.Application/Queries/Investments/GetCompanies/GetCompaniesQueryHandler.cs`
- Create: `src/backend/src/BigSchool.Application/Queries/Investments/GetCompanyById/GetCompanyByIdQuery.cs`
- Create: `src/backend/src/BigSchool.Application/Queries/Investments/GetCompanyById/GetCompanyByIdQueryHandler.cs`
- Create: `src/backend/src/BigSchool.Application/Queries/Investments/GetCompanyValuations/GetCompanyValuationsQuery.cs`
- Create: `src/backend/src/BigSchool.Application/Queries/Investments/GetCompanyValuations/GetCompanyValuationsQueryHandler.cs`

> Las queries Dapper se validan vía tests E2E (Tasks 10-13), no unitarios — coherente con el resto del proyecto.

- [x] **Step 1: Crear los DTOs de query (Dapper → `string` para monedas, `DateOnly` para fechas)**

Crear `src/backend/src/BigSchool.Application/DTOs/Investments/CompanyListItemDto.cs`:

```csharp
namespace BigSchool.Application.DTOs.Investments;

/// <summary>DTO de query Dapper. Currency como string (Dapper no convierte CHAR(3)→enum).</summary>
public record CompanyListItemDto(
    int IdCompany, string Name, string Ticker, string? Sector, string? Market,
    string Currency, decimal? LastPrice, DateOnly? LastValuationDate);
```

Crear `src/backend/src/BigSchool.Application/DTOs/Investments/ValuationListItemDto.cs`:

```csharp
namespace BigSchool.Application.DTOs.Investments;

public record ValuationListItemDto(
    int IdValuation, int IdCompany, decimal Price, string PriceCurrency, DateOnly Date, string? Source);
```

- [x] **Step 2: Crear `GetCompanies` (lista + filtro + última cotización)**

Crear `src/backend/src/BigSchool.Application/Queries/Investments/GetCompanies/GetCompaniesQuery.cs`:

```csharp
using BigSchool.Application.DTOs.Investments;
using MediatR;

namespace BigSchool.Application.Queries.Investments.GetCompanies;

public record GetCompaniesQuery(string? Sector, string? Market) : IRequest<IReadOnlyList<CompanyListItemDto>>;
```

Crear `src/backend/src/BigSchool.Application/Queries/Investments/GetCompanies/GetCompaniesQueryHandler.cs`:

```csharp
using BigSchool.Application.DTOs.Investments;
using BigSchool.Application.Interfaces;
using BigSchool.Domain.Enums;
using Dapper;
using MediatR;

namespace BigSchool.Application.Queries.Investments.GetCompanies;

public class GetCompaniesQueryHandler : IRequestHandler<GetCompaniesQuery, IReadOnlyList<CompanyListItemDto>>
{
    private readonly IDbConnectionFactory _dbFactory;

    public GetCompaniesQueryHandler(IDbConnectionFactory dbFactory) => _dbFactory = dbFactory;

    private const string GETCOMPANIES_QUERY = @"SELECT c.IdCompany, c.Name, c.Ticker, c.Sector, c.Market, c.Currency,
                                                       v.Price AS LastPrice, v.Date AS LastValuationDate
                                                FROM Companies c
                                                LEFT JOIN Valuations v ON v.IdValuation = (
                                                    SELECT v2.IdValuation FROM Valuations v2
                                                    WHERE v2.IdCompany = c.IdCompany AND v2.IdStatus <> @StatusDeleted
                                                    ORDER BY v2.Date DESC, v2.IdValuation DESC LIMIT 1)
                                                WHERE c.IdStatus <> @StatusDeleted
                                                  AND (@Sector IS NULL OR c.Sector = @Sector)
                                                  AND (@Market IS NULL OR c.Market = @Market)
                                                ORDER BY c.Name;";

    public async Task<IReadOnlyList<CompanyListItemDto>> Handle(GetCompaniesQuery request, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@StatusDeleted", EntityStatus.Deleted);
        parameters.Add("@Sector", request.Sector);
        parameters.Add("@Market", request.Market);

        using var conn = _dbFactory.CreateConnection();
        var rows = await conn.QueryAsync<CompanyListItemDto>(GETCOMPANIES_QUERY, parameters);
        return rows.ToList();
    }
}
```

- [x] **Step 3: Crear `GetCompanyById`**

Crear `src/backend/src/BigSchool.Application/Queries/Investments/GetCompanyById/GetCompanyByIdQuery.cs`:

```csharp
using BigSchool.Application.DTOs.Investments;
using MediatR;

namespace BigSchool.Application.Queries.Investments.GetCompanyById;

public record GetCompanyByIdQuery(int IdCompany) : IRequest<CompanyListItemDto?>;
```

Crear `src/backend/src/BigSchool.Application/Queries/Investments/GetCompanyById/GetCompanyByIdQueryHandler.cs`:

```csharp
using BigSchool.Application.DTOs.Investments;
using BigSchool.Application.Interfaces;
using BigSchool.Domain.Enums;
using Dapper;
using MediatR;

namespace BigSchool.Application.Queries.Investments.GetCompanyById;

public class GetCompanyByIdQueryHandler : IRequestHandler<GetCompanyByIdQuery, CompanyListItemDto?>
{
    private readonly IDbConnectionFactory _dbFactory;

    public GetCompanyByIdQueryHandler(IDbConnectionFactory dbFactory) => _dbFactory = dbFactory;

    private const string GETCOMPANYBYID_QUERY = @"SELECT c.IdCompany, c.Name, c.Ticker, c.Sector, c.Market, c.Currency,
                                                         v.Price AS LastPrice, v.Date AS LastValuationDate
                                                  FROM Companies c
                                                  LEFT JOIN Valuations v ON v.IdValuation = (
                                                      SELECT v2.IdValuation FROM Valuations v2
                                                      WHERE v2.IdCompany = c.IdCompany AND v2.IdStatus <> @StatusDeleted
                                                      ORDER BY v2.Date DESC, v2.IdValuation DESC LIMIT 1)
                                                  WHERE c.IdCompany = @IdCompany AND c.IdStatus <> @StatusDeleted
                                                  LIMIT 1;";

    public async Task<CompanyListItemDto?> Handle(GetCompanyByIdQuery request, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@IdCompany", request.IdCompany);
        parameters.Add("@StatusDeleted", EntityStatus.Deleted);

        using var conn = _dbFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<CompanyListItemDto>(GETCOMPANYBYID_QUERY, parameters);
    }
}
```

- [x] **Step 4: Crear `GetCompanyValuations`**

Crear `src/backend/src/BigSchool.Application/Queries/Investments/GetCompanyValuations/GetCompanyValuationsQuery.cs`:

```csharp
using BigSchool.Application.DTOs.Investments;
using MediatR;

namespace BigSchool.Application.Queries.Investments.GetCompanyValuations;

public record GetCompanyValuationsQuery(int IdCompany) : IRequest<IReadOnlyList<ValuationListItemDto>>;
```

Crear `src/backend/src/BigSchool.Application/Queries/Investments/GetCompanyValuations/GetCompanyValuationsQueryHandler.cs`:

```csharp
using BigSchool.Application.DTOs.Investments;
using BigSchool.Application.Interfaces;
using BigSchool.Domain.Enums;
using Dapper;
using MediatR;

namespace BigSchool.Application.Queries.Investments.GetCompanyValuations;

public class GetCompanyValuationsQueryHandler : IRequestHandler<GetCompanyValuationsQuery, IReadOnlyList<ValuationListItemDto>>
{
    private readonly IDbConnectionFactory _dbFactory;

    public GetCompanyValuationsQueryHandler(IDbConnectionFactory dbFactory) => _dbFactory = dbFactory;

    private const string GETCOMPANYVALUATIONS_QUERY = @"SELECT IdValuation, IdCompany, Price, PriceCurrency, Date, Source
                                                        FROM Valuations
                                                        WHERE IdCompany = @IdCompany AND IdStatus <> @StatusDeleted
                                                        ORDER BY Date DESC, IdValuation DESC;";

    public async Task<IReadOnlyList<ValuationListItemDto>> Handle(GetCompanyValuationsQuery request, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@IdCompany", request.IdCompany);
        parameters.Add("@StatusDeleted", EntityStatus.Deleted);

        using var conn = _dbFactory.CreateConnection();
        var rows = await conn.QueryAsync<ValuationListItemDto>(GETCOMPANYVALUATIONS_QUERY, parameters);
        return rows.ToList();
    }
}
```

- [x] **Step 5: Compilar Application**

Run: `dotnet build src/backend/src/BigSchool.Application/BigSchool.Application.csproj -c Release`
Expected: BUILD SUCCEEDED.

- [x] **Step 6: Commit**

```bash
git add src/backend/src/BigSchool.Application/DTOs/Investments/CompanyListItemDto.cs src/backend/src/BigSchool.Application/DTOs/Investments/ValuationListItemDto.cs src/backend/src/BigSchool.Application/Queries/Investments/
git commit -m "feat: queries Dapper GetCompanies, GetCompanyById, GetCompanyValuations + DTOs

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 7: `CompaniesController` (5 endpoints)

**Files:**
- Create: `src/backend/src/BigSchool.WebApi/Controllers/CompaniesController.cs`

- [x] **Step 1: Crear el controller**

Crear `src/backend/src/BigSchool.WebApi/Controllers/CompaniesController.cs`:

```csharp
using BigSchool.Application.Commands.Investments.AddValuation;
using BigSchool.Application.Commands.Investments.CreateCompany;
using BigSchool.Application.Common;
using BigSchool.Application.DTOs.Investments;
using BigSchool.Application.Queries.Investments.GetCompanies;
using BigSchool.Application.Queries.Investments.GetCompanyById;
using BigSchool.Application.Queries.Investments.GetCompanyValuations;
using BigSchool.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BigSchool.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/companies")]
public class CompaniesController : ControllerBase
{
    private readonly IMediator _mediator;

    public CompaniesController(IMediator mediator) => _mediator = mediator;

    public record CreateCompanyRequest(string Name, string Ticker, string? Sector, string? Market, Currency Currency);
    public record AddValuationRequest(decimal Price, DateOnly Date, string? Source);

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CompanyListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] string? sector, [FromQuery] string? market)
    {
        var result = await _mediator.Send(new GetCompaniesQuery(sector, market));
        return Ok(ApiResponse<IReadOnlyList<CompanyListItemDto>>.Success(result));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _mediator.Send(new GetCompanyByIdQuery(id));
        return result is null
            ? NotFound(ApiResponse.Fail(new ApiError { Code = "ENTITY_NOT_FOUND", Message = "Empresa no encontrada." }))
            : Ok(ApiResponse<CompanyListItemDto>.Success(result));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CompanyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateCompanyRequest body)
    {
        var command = new CreateCompanyCommand(body.Name, body.Ticker, body.Sector, body.Market, body.Currency);
        var result = await _mediator.Send(command);
        return Ok(ApiResponse<CompanyDto>.Success(result));
    }

    [HttpGet("{id:int}/valuations")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ValuationListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetValuations(int id)
    {
        var result = await _mediator.Send(new GetCompanyValuationsQuery(id));
        return Ok(ApiResponse<IReadOnlyList<ValuationListItemDto>>.Success(result));
    }

    [HttpPost("{id:int}/valuations")]
    [ProducesResponseType(typeof(ApiResponse<ValuationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddValuation(int id, [FromBody] AddValuationRequest body)
    {
        var command = new AddValuationCommand(id, body.Price, body.Date, body.Source);
        var result = await _mediator.Send(command);
        return Ok(ApiResponse<ValuationDto>.Success(result));
    }
}
```

- [x] **Step 2: Compilar la solución de backend**

Run: `dotnet build src/backend/src/BigSchool.WebApi/BigSchool.WebApi.csproj -c Release`
Expected: BUILD SUCCEEDED.

- [x] **Step 3: Commit**

```bash
git add src/backend/src/BigSchool.WebApi/Controllers/CompaniesController.cs
git commit -m "feat: CompaniesController (GET lista/detalle/valoraciones, POST company/valuation)

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 8: Integración — `ResetAsync` (limpiar catálogo de tests) + `CompanyEndpointTestBase`

**Files:**
- Modify: `src/backend/tests/BigSchool.Integration.Tests/Fixtures/MySqlDatabaseFixture.cs`
- Create: `src/backend/tests/BigSchool.Integration.Tests/Investments/CompanyEndpointTestBase.cs`

- [x] **Step 1: Ampliar `ResetAsync` para limpiar companies/valuations creadas por tests**

En `src/backend/tests/BigSchool.Integration.Tests/Fixtures/MySqlDatabaseFixture.cs`, dentro de `ResetAsync`, entre `TRUNCATE TABLE Transactions;` y `DELETE FROM SubCategories...`, añadir las dos líneas de limpieza de catálogo (preservan las 4 empresas semilla con IdCompany 1-4 y sus valoraciones):

Reemplazar:

```csharp
        await conn.ExecuteAsync("SET FOREIGN_KEY_CHECKS = 0;");
        await conn.ExecuteAsync("TRUNCATE TABLE Transactions;");
        await conn.ExecuteAsync("DELETE FROM SubCategories WHERE IdUser IS NOT NULL;");
```

por:

```csharp
        await conn.ExecuteAsync("SET FOREIGN_KEY_CHECKS = 0;");
        await conn.ExecuteAsync("TRUNCATE TABLE Transactions;");
        // Catálogo de inversiones: preservar las 4 empresas semilla (IdCompany 1-4) y sus valoraciones;
        // limpiar solo las creadas por tests.
        await conn.ExecuteAsync("DELETE FROM Valuations WHERE IdCompany > 4;");
        await conn.ExecuteAsync("DELETE FROM Companies WHERE IdCompany > 4;");
        await conn.ExecuteAsync("DELETE FROM SubCategories WHERE IdUser IS NOT NULL;");
```

- [x] **Step 2: Crear la base de tests E2E de inversiones**

Crear `src/backend/tests/BigSchool.Integration.Tests/Investments/CompanyEndpointTestBase.cs`:

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BigSchool.Application.Interfaces.Services;
using BigSchool.Domain.Entities;
using BigSchool.Domain.Enums;
using BigSchool.Infrastructure.Persistence;
using BigSchool.Integration.Tests.Fixtures;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

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
```

- [x] **Step 3: Compilar el proyecto de tests de integración**

Run: `dotnet build src/backend/tests/BigSchool.Integration.Tests/BigSchool.Integration.Tests.csproj -c Release`
Expected: BUILD SUCCEEDED.

- [x] **Step 4: Commit**

```bash
git add src/backend/tests/BigSchool.Integration.Tests/Fixtures/MySqlDatabaseFixture.cs src/backend/tests/BigSchool.Integration.Tests/Investments/CompanyEndpointTestBase.cs
git commit -m "test: ResetAsync limpia catálogo de tests + CompanyEndpointTestBase

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 9: E2E `CreateCompanyTests` — exhaustivo + 409 + 400 + 401

**Files:**
- Create: `src/backend/tests/BigSchool.Integration.Tests/Investments/CreateCompanyTests.cs`

- [x] **Step 1: Escribir el fichero**

Crear `src/backend/tests/BigSchool.Integration.Tests/Investments/CreateCompanyTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Investments;

[Collection(IntegrationCollection.Name)]
public class CreateCompanyTests : CompanyEndpointTestBase
{
    public CreateCompanyTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Post_NewCompany_ReturnsDto_AndPersists()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        var ticker = $"T{Guid.NewGuid():N}".Substring(0, 8).ToUpperInvariant();

        var response = await client.PostAsJsonAsync("/api/v1/companies", new
        {
            name = "Nvidia Corp.",
            ticker,
            sector = "Technology",
            market = "NASDAQ",
            currency = "USD"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<CompanyResponse>>();
        env!.Errors.Should().BeEmpty();
        var dto = env.Data!;
        dto.IdCompany.Should().BeGreaterThan(SeededCompaniesCount);
        dto.Name.Should().Be("Nvidia Corp.");
        dto.Ticker.Should().Be(ticker);
        dto.Sector.Should().Be("Technology");
        dto.Market.Should().Be("NASDAQ");
        dto.Currency.Should().Be("USD");

        await using var conn = new MySqlConnector.MySqlConnection(Fixture.ConnectionString);
        var row = await Dapper.SqlMapper.QuerySingleAsync(conn,
            "SELECT Name, Ticker, Currency, IdStatus FROM Companies WHERE IdCompany = @id;",
            new { id = dto.IdCompany });
        ((string)row.Name).Should().Be("Nvidia Corp.");
        ((string)row.Ticker).Should().Be(ticker);
        ((string)row.Currency).Should().Be("USD");
        ((short)row.IdStatus).Should().Be((short)BigSchool.Domain.Enums.EntityStatus.Active);
    }

    [Fact]
    public async Task Post_DuplicateTicker_Returns409()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);

        // El ticker AAPL existe en el seed (IdCompany 1).
        var response = await client.PostAsJsonAsync("/api/v1/companies", new
        {
            name = "Apple Duplicada", ticker = "AAPL", sector = (string?)null, market = (string?)null, currency = "USD"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        env!.Errors.Should().Contain(e => e.Code == "DUPLICATE_TICKER");
    }

    [Theory]
    [InlineData("", "VALIDTKR", "USD")]                 // name vacío
    [InlineData("Empresa", "", "USD")]                  // ticker vacío
    [InlineData("Empresa", "TOOOOOLONGTICKER", "USD")]  // ticker > 10
    public async Task Post_InvalidPayload_Returns400(string name, string ticker, string currency)
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);

        var response = await client.PostAsJsonAsync("/api/v1/companies", new { name, ticker, sector = (string?)null, market = (string?)null, currency });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        env!.Errors.Should().Contain(e => e.Code == "VALIDATION_ERROR");
    }

    [Fact]
    public async Task Post_WithoutToken_Returns401()
    {
        var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/companies", new
        {
            name = "X", ticker = "X", sector = (string?)null, market = (string?)null, currency = "USD"
        });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

- [x] **Step 2: Ejecutar**

Run: `dotnet test src/backend/tests/BigSchool.Integration.Tests/BigSchool.Integration.Tests.csproj --filter "FullyQualifiedName~CreateCompanyTests" -c Release`
Expected: PASS los 6 casos (1 exhaustivo + 1 conflicto + 3 Theory + 1 de 401).

- [x] **Step 3: Commit**

```bash
git add src/backend/tests/BigSchool.Integration.Tests/Investments/CreateCompanyTests.cs
git commit -m "test: cobertura E2E de POST /companies (exhaustivo, 409, 400, 401)

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 10: E2E `AddValuationTests` — exhaustivo + 404 + 409 + 400 + 401

**Files:**
- Create: `src/backend/tests/BigSchool.Integration.Tests/Investments/AddValuationTests.cs`

- [x] **Step 1: Escribir el fichero**

Crear `src/backend/tests/BigSchool.Integration.Tests/Investments/AddValuationTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Investments;

[Collection(IntegrationCollection.Name)]
public class AddValuationTests : CompanyEndpointTestBase
{
    public AddValuationTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    private async Task<int> CreateUsdCompanyAsync(HttpClient client)
    {
        var ticker = $"V{Guid.NewGuid():N}".Substring(0, 8).ToUpperInvariant();
        return await CreateCompanyViaApiAsync(client, new
        {
            name = "Test Co.", ticker, sector = (string?)null, market = (string?)null, currency = "USD"
        });
    }

    [Fact]
    public async Task Post_Valuation_ReturnsDtoInCompanyCurrency_AndPersists()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        var companyId = await CreateUsdCompanyAsync(client);

        var response = await client.PostAsJsonAsync($"/api/v1/companies/{companyId}/valuations", new
        {
            price = 195.5000m, date = "2026-02-15", source = "manual"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = (await response.Content.ReadFromJsonAsync<ApiEnvelope<ValuationResponse>>())!.Data!;
        dto.IdValuation.Should().BeGreaterThan(0);
        dto.IdCompany.Should().Be(companyId);
        dto.Price.Should().Be(195.5000m);
        dto.Currency.Should().Be("USD");          // hereda la moneda de la empresa
        dto.Date.Should().Be("2026-02-15");
        dto.Source.Should().Be("manual");

        await using var conn = new MySqlConnector.MySqlConnection(Fixture.ConnectionString);
        var row = await Dapper.SqlMapper.QuerySingleAsync(conn,
            "SELECT Price, PriceCurrency, IdCompany FROM Valuations WHERE IdValuation = @id;",
            new { id = dto.IdValuation });
        ((decimal)row.Price).Should().Be(195.5000m);
        ((string)row.PriceCurrency).Should().Be("USD");
        ((int)row.IdCompany).Should().Be(companyId);
    }

    [Fact]
    public async Task Post_Valuation_DuplicateDate_Returns409()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        var companyId = await CreateUsdCompanyAsync(client);
        await client.PostAsJsonAsync($"/api/v1/companies/{companyId}/valuations", new { price = 100m, date = "2026-02-15", source = (string?)null });

        var response = await client.PostAsJsonAsync($"/api/v1/companies/{companyId}/valuations", new { price = 110m, date = "2026-02-15", source = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        env!.Errors.Should().Contain(e => e.Code == "DUPLICATE_VALUATION");
    }

    [Fact]
    public async Task Post_Valuation_NonExistentCompany_Returns404()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);

        var response = await client.PostAsJsonAsync("/api/v1/companies/999999/valuations", new { price = 100m, date = "2026-02-15", source = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        env!.Errors.Should().Contain(e => e.Code == "ENTITY_NOT_FOUND");
    }

    [Fact]
    public async Task Post_Valuation_NonPositivePrice_Returns400()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        var companyId = await CreateUsdCompanyAsync(client);

        var response = await client.PostAsJsonAsync($"/api/v1/companies/{companyId}/valuations", new { price = 0m, date = "2026-02-15", source = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        env!.Errors.Should().Contain(e => e.Code == "VALIDATION_ERROR" && e.Field == "Price");
    }

    [Fact]
    public async Task Post_Valuation_WithoutToken_Returns401()
    {
        var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/companies/1/valuations", new { price = 100m, date = "2026-02-15", source = (string?)null });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

- [x] **Step 2: Ejecutar**

Run: `dotnet test src/backend/tests/BigSchool.Integration.Tests/BigSchool.Integration.Tests.csproj --filter "FullyQualifiedName~AddValuationTests" -c Release`
Expected: PASS los 5 tests.

- [x] **Step 3: Commit**

```bash
git add src/backend/tests/BigSchool.Integration.Tests/Investments/AddValuationTests.cs
git commit -m "test: cobertura E2E de POST /companies/{id}/valuations (exhaustivo, 409, 404, 400, 401)

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 11: E2E `GetCompaniesTests` + `GetCompanyByIdTests`

**Files:**
- Create: `src/backend/tests/BigSchool.Integration.Tests/Investments/GetCompaniesTests.cs`
- Create: `src/backend/tests/BigSchool.Integration.Tests/Investments/GetCompanyByIdTests.cs`

- [x] **Step 1: Escribir `GetCompaniesTests`**

Crear `src/backend/tests/BigSchool.Integration.Tests/Investments/GetCompaniesTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Investments;

[Collection(IntegrationCollection.Name)]
public class GetCompaniesTests : CompanyEndpointTestBase
{
    public GetCompaniesTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Get_ReturnsSeededCompanies_PlusCreated()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        var ticker = $"G{Guid.NewGuid():N}".Substring(0, 8).ToUpperInvariant();
        var createdId = await CreateCompanyViaApiAsync(client, new
        {
            name = "Created Co.", ticker, sector = (string?)null, market = (string?)null, currency = "EUR"
        });

        var response = await client.GetAsync("/api/v1/companies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<CompanyListItemResponse>>>();
        env!.Data!.Should().HaveCount(SeededCompaniesCount + 1);
        env.Data!.Should().Contain(c => c.IdCompany == createdId && c.Ticker == ticker);
        // La empresa AAPL semilla trae su última cotización (2026-03-02 = 210).
        var aapl = env.Data!.Single(c => c.Ticker == "AAPL");
        aapl.LastPrice.Should().Be(210.0000m);
        aapl.LastValuationDate.Should().Be("2026-03-02");
    }

    [Fact]
    public async Task Get_FilterByMarket_ReturnsOnlyThatMarket()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);

        // Seed: AAPL y MSFT son NASDAQ.
        var response = await client.GetAsync("/api/v1/companies?market=NASDAQ");

        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<CompanyListItemResponse>>>();
        env!.Data!.Should().OnlyContain(c => c.Market == "NASDAQ");
        env.Data!.Should().Contain(c => c.Ticker == "AAPL");
        env.Data!.Should().Contain(c => c.Ticker == "MSFT");
    }

    [Fact]
    public async Task Get_WithoutToken_Returns401()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/v1/companies");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

- [x] **Step 2: Escribir `GetCompanyByIdTests`**

Crear `src/backend/tests/BigSchool.Integration.Tests/Investments/GetCompanyByIdTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Investments;

[Collection(IntegrationCollection.Name)]
public class GetCompanyByIdTests : CompanyEndpointTestBase
{
    public GetCompanyByIdTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetById_ExistingCompany_ReturnsDetail_WithLastValuation()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        var ticker = $"D{Guid.NewGuid():N}".Substring(0, 8).ToUpperInvariant();
        var id = await CreateCompanyViaApiAsync(client, new
        {
            name = "Detail Co.", ticker, sector = "Energy", market = "LSE", currency = "GBP"
        });
        await client.PostAsJsonAsync($"/api/v1/companies/{id}/valuations", new { price = 10m, date = "2026-01-10", source = (string?)null });
        await client.PostAsJsonAsync($"/api/v1/companies/{id}/valuations", new { price = 12m, date = "2026-03-10", source = (string?)null });

        var response = await client.GetAsync($"/api/v1/companies/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = (await response.Content.ReadFromJsonAsync<ApiEnvelope<CompanyListItemResponse>>())!.Data!;
        dto.IdCompany.Should().Be(id);
        dto.Ticker.Should().Be(ticker);
        dto.Currency.Should().Be("GBP");
        dto.LastPrice.Should().Be(12.0000m);              // la más reciente
        dto.LastValuationDate.Should().Be("2026-03-10");
    }

    [Fact]
    public async Task GetById_NonExistent_Returns404()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);

        var response = await client.GetAsync("/api/v1/companies/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        env!.Errors.Should().Contain(e => e.Code == "ENTITY_NOT_FOUND");
    }

    [Fact]
    public async Task GetById_WithoutToken_Returns401()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/v1/companies/1");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

- [x] **Step 3: Ejecutar**

Run: `dotnet test src/backend/tests/BigSchool.Integration.Tests/BigSchool.Integration.Tests.csproj --filter "FullyQualifiedName~GetCompaniesTests|FullyQualifiedName~GetCompanyByIdTests" -c Release`
Expected: PASS los 6 tests (3 + 3).

- [x] **Step 4: Commit**

```bash
git add src/backend/tests/BigSchool.Integration.Tests/Investments/GetCompaniesTests.cs src/backend/tests/BigSchool.Integration.Tests/Investments/GetCompanyByIdTests.cs
git commit -m "test: cobertura E2E de GET /companies y GET /companies/{id} (lista, filtro, última cotización, 404, 401)

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 12: E2E `GetCompanyValuationsTests`

**Files:**
- Create: `src/backend/tests/BigSchool.Integration.Tests/Investments/GetCompanyValuationsTests.cs`

- [ ] **Step 1: Escribir el fichero**

Crear `src/backend/tests/BigSchool.Integration.Tests/Investments/GetCompanyValuationsTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Investments;

[Collection(IntegrationCollection.Name)]
public class GetCompanyValuationsTests : CompanyEndpointTestBase
{
    public GetCompanyValuationsTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetValuations_ReturnsHistory_OrderedByDateDesc()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        var ticker = $"H{Guid.NewGuid():N}".Substring(0, 8).ToUpperInvariant();
        var id = await CreateCompanyViaApiAsync(client, new
        {
            name = "History Co.", ticker, sector = (string?)null, market = (string?)null, currency = "USD"
        });
        await client.PostAsJsonAsync($"/api/v1/companies/{id}/valuations", new { price = 100m, date = "2026-01-10", source = (string?)null });
        await client.PostAsJsonAsync($"/api/v1/companies/{id}/valuations", new { price = 120m, date = "2026-03-10", source = (string?)null });

        var response = await client.GetAsync($"/api/v1/companies/{id}/valuations");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = (await response.Content.ReadFromJsonAsync<ApiEnvelope<List<ValuationListItemResponse>>>())!.Data!;
        list.Should().HaveCount(2);
        list[0].Date.Should().Be("2026-03-10");          // orden fecha desc
        list[0].Price.Should().Be(120.0000m);
        list[0].PriceCurrency.Should().Be("USD");
        list.Should().OnlyContain(v => v.IdCompany == id);
    }

    [Fact]
    public async Task GetValuations_NonExistentCompany_ReturnsEmptyList()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);

        var response = await client.GetAsync("/api/v1/companies/999999/valuations");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = (await response.Content.ReadFromJsonAsync<ApiEnvelope<List<ValuationListItemResponse>>>())!.Data!;
        list.Should().BeEmpty();
    }

    [Fact]
    public async Task GetValuations_WithoutToken_Returns401()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/v1/companies/1/valuations");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

- [ ] **Step 2: Ejecutar**

Run: `dotnet test src/backend/tests/BigSchool.Integration.Tests/BigSchool.Integration.Tests.csproj --filter "FullyQualifiedName~GetCompanyValuationsTests" -c Release`
Expected: PASS los 3 tests.

- [ ] **Step 3: Commit**

```bash
git add src/backend/tests/BigSchool.Integration.Tests/Investments/GetCompanyValuationsTests.cs
git commit -m "test: cobertura E2E de GET /companies/{id}/valuations (histórico ordenado, vacío, 401)

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 13: Verificación global, documentación y PR

**Files:**
- Modify: `docs/02-backend-design.md` (nota de `Valuation.Price` como `Money` owned, sin `MoneyConversion`)
- Modify: `docs/diario.md` (entrada Plan 3A)

- [ ] **Step 1: Ejecutar TODA la suite de integración**

Run: `dotnet test src/backend/tests/BigSchool.Integration.Tests/BigSchool.Integration.Tests.csproj -c Release`
Expected: PASS todos — los 37 previos (Auth + Transactions + Persistence/Services) + los nuevos de Investments (CreateCompany 6 + AddValuation 5 + GetCompanies 3 + GetCompanyById 3 + GetCompanyValuations 3 = 20).

- [ ] **Step 2: Ejecutar las suites unitarias**

Run: `dotnet test src/backend/tests/BigSchool.Domain.Tests/BigSchool.Domain.Tests.csproj -c Release` y `dotnet test src/backend/tests/BigSchool.Application.Tests/BigSchool.Application.Tests.csproj -c Release`
Expected: PASS todos (incluidos `CompanyTests`, `CreateCompanyCommandHandlerTests`, `AddValuationCommandHandlerTests`).

- [ ] **Step 3: Actualizar `docs/02-backend-design.md`**

En la sección `#### Valuations`, sustituir la nota de Plan 3 (`> **Plan 3 (multimoneda)**: cada Valuation snapshotea...`) por:

```markdown
> **Plan 3A (implementado)**: `Valuation.Price` es un VO `Money` (owned type EF → columnas `Price` DECIMAL(18,4) + `PriceCurrency` CHAR(3)) en la **moneda de la empresa**. El catálogo (`Companies`/`Valuations`) es **global** (sin `IdUser`); la conversión a la moneda base del usuario **no** se snapshotea aquí: se calcula por-usuario en las queries de cartera (Plan 3B). Índice único `(IdCompany, Date)`.
```

Y en `#### Holdings`, dejar la nota de `AvgBuyPrice` como referencia a Plan 3B (sin cambios; se implementa en 3B).

- [ ] **Step 4: Añadir la entrada del diario**

En `docs/diario.md`, antes de la línea `*Añadir nuevas entradas al final del documento con fecha y fase.*`, insertar:

```markdown
## 2026-06-21 — BC Inversiones · Plan 3A: Catálogo (Company + Valuation)

### Fase: Implementación

**Módulo**: backend

**Actividades realizadas:**
- Plan `docs/superpowers/plans/009-2026-06-21-backend-inversiones-plan-3a-companies.md` (13 tareas), primer plan del BC Inversiones (spec `002-2026-06-21-backend-inversiones-design.md`).
- Dominio: `Company` (AR global) + `Valuation` (entidad hija, `Money` en moneda de la empresa) con `AddValuation` y unicidad `(IdCompany, Date)`; excepciones `DuplicateTickerDomainException` y `DuplicateValuationDomainException` (→409).
- Infra: `CompanyConfiguration`/`ValuationConfiguration` (owned `Price`, FK shadow `IdCompany`, índices únicos), DbSets, seed de 4 empresas (USD/EUR/GBP) + 8 valoraciones, migración `CreateCompanies`.
- Application: `ICompanyRepository`/`CompanyRepository`; commands `CreateCompany`/`AddValuation`; queries Dapper `GetCompanies`/`GetCompanyById` (con última cotización) y `GetCompanyValuations`.
- WebApi: `CompaniesController` (5 endpoints `[Authorize]`).
- Tests: unitarios Domain (`CompanyTests`) y Application (2 handlers); E2E por endpoint (CreateCompany 6, AddValuation 5, GetCompanies 3, GetCompanyById 3, GetCompanyValuations 3).

**Decisiones / Problemas encontrados:**
- Catálogo **global** (sin `IdUser`): `Valuation.Price` no snapshotea a base de usuario; la conversión es por-usuario en Plan 3B.
- `ResetAsync` del fixture preserva las 4 empresas semilla (IdCompany 1-4) y limpia solo las creadas por tests.
- Seed del owned type `Price` vía `OwnsOne(...).HasData` con FK shadow `ValuationIdValuation`.

**Resultado / Estado:**
- Plan 3A completado. Suite de integración en verde (37 previos + 20 nuevos). Catálogo de inversiones operativo end-to-end.

**Siguiente paso:**
- [ ] Plan 3B — Portfolio + Holding + Disposal + ventas FIFO + performance (consume el catálogo de 3A).
```

- [ ] **Step 5: Commit de documentación**

```bash
git add docs/02-backend-design.md docs/diario.md
git commit -m "docs: actualizar diseño backend (Valuation.Price como Money) y diario (Plan 3A)

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

- [ ] **Step 6: Push + PR**

```bash
git push -u origin feature/spec-bc-inversiones
gh pr create --base develop --title "feat: BC Inversiones Plan 3A — catálogo Company + Valuation" --body "Spec del BC Inversiones (002) + Plan 3A: catálogo global de empresas y cotizaciones.

## Resumen
- Dominio: Company (AR global) + Valuation (hija, Money en moneda de la empresa), unicidad (IdCompany, Date).
- Infra: EF configs, seed (4 empresas/8 valoraciones), migración CreateCompanies.
- Application: CQRS (CreateCompany, AddValuation; GetCompanies, GetCompanyById, GetCompanyValuations).
- WebApi: CompaniesController (5 endpoints).
- Tests: unitarios + E2E por endpoint (20 nuevos).
- Docs: spec 002, diseño backend, diario.

🤖 Generated with [Claude Code](https://claude.com/claude-code)"
```

> **Nota**: la rama `feature/spec-bc-inversiones` ya contiene la spec (002) y la actualización previa del diario. Plan 3A se construye sobre ella. Si se prefiere separar la spec del código, crear una rama `feature/backend-inversiones-3a` desde `develop` tras mergear la spec.

---

## Self-Review

**1. Cobertura de la spec (sección 12, alcance Plan 3A):**
- `Company` (AR) + `Valuation` (hija): Task 1. ✅
- Migración + seed: Task 2. ✅
- CQRS (`CreateCompany`, `AddValuation`, `GetCompanies`, `GetCompanyById`, `GetCompanyValuations`): Tasks 4-6. ✅
- `CompaniesController`: Task 7. ✅
- Tests unitarios + E2E: Tasks 1, 4, 5 (unit) y 8-12 (E2E). ✅
- Multimoneda: `Valuation.Price` = `Money` en moneda de empresa, sin conversión (catálogo global). ✅ (la conversión a base queda explícitamente para Plan 3B)
- Reglas E2E de `AGENTS.md`: un fichero por endpoint, exhaustivo con persistencia física (Tasks 9, 10), 401/400/404/409. ✅

**2. Placeholders:** ninguno; cada paso lleva código completo y comando con resultado esperado.

**3. Consistencia de tipos:** `CompanyDto`/`ValuationDto` (command, `Currency` tipado) vs `CompanyListItemDto`/`ValuationListItemDto` (query Dapper, `string`). `ICompanyRepository.GetByTickerAsync`/`GetByIdWithValuationsAsync` definidos en Task 3 y usados en Tasks 4-5. Códigos de error: `DUPLICATE_TICKER` (409), `DUPLICATE_VALUATION` (409), `ENTITY_NOT_FOUND` (404), `VALIDATION_ERROR` (400). Seed: 4 empresas (IdCompany 1-4), 8 valoraciones (IdValuation 1-8); `ResetAsync` y `SeededCompaniesCount` alineados con ese umbral. Última cotización AAPL = 210 @ 2026-03-02 (coincide con seed IdValuation 2).
