# Backend — Cimientos Multimoneda (Plan 2A) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Construir el modelo de dominio multimoneda transversal (`Currency`, `Money`, `MoneyConversion`), añadir la moneda base por usuario (`Users.BaseCurrency`) y la infraestructura de tipos de cambio (entidad `ExchangeRate` + tabla cache, `IExchangeRateProvider` + `ExchangeRateApiClient` con proveedor externo Frankfurter + Dapper), de forma reutilizable tal cual por el BC Transaction (Plan 2B) y por Inversiones (Plan 3). Diseño aprobado: `docs/superpowers/specs/2026-06-17-backend-finanzas-multicurrency-design.md`.

**Architecture:** Clean Architecture + DDD. `Currency` es un enum (`: short`) cuyo NOMBRE es el ISO 4217 alpha-3 y se persiste como `CHAR(3)` con un `ValueConverter` compartido. `Money` y `MoneyConversion` son Value Objects (records inmutables con factory validadora); `MoneyConversion` es el snapshot de conversión (Original + Rate + Base + RateDate) que será un EF *owned type* en Plan 2B. La tabla `ExchangeRates` es **reference data**: EF posee solo el esquema (migración + `ExchangeRateConfiguration`), pero el `ExchangeRateApiClient` lee/escribe el cache con **Dapper** vía `IDbConnectionFactory`, desacoplado del `SaveChanges`/UoW de negocio. La llamada al proveedor externo vive solo en Infrastructure; el dominio recibe el `Rate` ya resuelto.

**Tech Stack:** .NET 8, EF Core 8.0.11 + Pomelo MySQL 8.0.2 (`DateOnly` nativo), Dapper 2.1.35, `IHttpClientFactory`, FluentAssertions + xUnit + Moq (unit), MySQL real de docker-compose (BD dedicada `bigschool_test` migrada por EF) + WireMock.Net (integración). DI con Autofac (`AsImplementedInterfaces` auto-registra los servicios por capa).

**Dependencias previas:** Plan 1 (auth) completado. Existen: `BaseEntity`, `User` (AR), `EntityStatus`/`MainCategory` (enums persistidos con `HasConversion<short>`), `BigSchoolDbContext` (`ApplyConfigurationsFromAssembly`, `SeedSubCategories`), `IDbConnectionFactory` + `DbConnectionMySqlFactory`, `AppSettings` (con `RagServiceSettings` como patrón de settings), `Directory.Build.props` (versiones centralizadas), migración `InitialCreate`.

**Importante — Plan 2B depende de este plan.** Plan 2B (BC Transaction) consume `Money`, `MoneyConversion`, `Currency`, `IExchangeRateProvider` y `Users.BaseCurrency`. Este plan NO crea la entidad `Transaction` ni su CQRS: solo los cimientos.

---

## Decisiones de Diseño (aprobadas — de la spec)

1. **Moneda base por usuario** (`Users.BaseCurrency`, `CHAR(3)`, NOT NULL, DEFAULT 'EUR'). Sin reconversión retroactiva si el usuario la cambia (el histórico conserva su snapshot).
2. **Modelo de conversión por snapshot**: `MoneyConversion` guarda Original + Rate + Base + RateDate. La entidad calcula `Base.Amount = Original.Amount * Rate` en la factory. El dominio NO hace llamadas externas: recibe el `Rate` ya resuelto. Si `Original.Currency == BaseCurrency` → `Rate = 1`.
3. **`Currency` es enum en código, no tabla** (coherente con `EntityStatus`/`MainCategory`). Set reducido para la demo: EUR, USD, GBP, CHF, JPY. Persistido como `CHAR(3)` (alpha-3 = nombre del enum) con `ValueConverter` compartido.
4. **Tipos de cambio: proveedor externo + cache.** `ExchangeRate` es entidad plana (NO `BaseEntity`, NO AR, NO repositorio, sin `DomainEvents` ni `IdStatus`, fuera del Global Query Filter). EF mapea solo el esquema; el cliente lee/escribe con Dapper (UPSERT idempotente race-safe sobre `UNIQUE(FromCurrency, ToCurrency, RateDate)`). Proveedor: **Frankfurter (ECB)** — gratis, sin API key, histórico por fecha.
5. **Simplificaciones TFM**: `DECIMAL(18,2)` importes; `DECIMAL(18,6)` tipos de cambio; sin contemplar monedas con distinto nº de decimales (JPY=0); cache por día (`RateDate` granularidad `DATE`), no intradía; seed de unos pocos pares fijos para demo determinista sin red.

---

## Resumen de Tareas

| # | Tarea | Capa | Descripción |
|---|-------|------|-------------|
| 1 | Enum `Currency` | Domain | Enum `: short` ISO 4217 + tests |
| 2 | VO `Money` | Domain | Record + factory `Create` con invariantes + tests |
| 3 | VO `MoneyConversion` | Domain | Record + factory `Create` (calcula Base) + tests |
| 4 | `CurrencyConverter` + `Users.BaseCurrency` | Domain/Infra | ValueConverter compartido, campo en `User`, config, migración |
| 5 | Entidad `ExchangeRate` + config + migración | Domain/Infra | Entidad plana, `ExchangeRateConfiguration`, tabla + seed |
| 6 | `IExchangeRateProvider` + settings + HttpClient | Application/WebApi | Interfaz, `ExchangeRateSettings`, registro `AddHttpClient` |
| 7 | `ExchangeRateApiClient` | Infrastructure | Cache Dapper + Frankfurter (anti-corruption) + unit tests |
| 8 | Tests de integración | Integration.Tests | Migración aplica + cache hit/miss con WireMock |
| 9 | Documentación (ADR-006 + design + diario) | docs | Cierre documental del plan |

---

### Task 1: Enum `Currency`

**Files:**
- Create: `src/backend/src/BigSchool.Domain/Enums/Currency.cs`
- Test: `src/backend/tests/BigSchool.Domain.Tests/Enums/CurrencyTests.cs`

- [x] **Step 1: Escribir el test que falla**

```csharp
// src/backend/tests/BigSchool.Domain.Tests/Enums/CurrencyTests.cs
using BigSchool.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace BigSchool.Domain.Tests.Enums;

public class CurrencyTests
{
    [Theory]
    [InlineData(Currency.EUR, "EUR")]
    [InlineData(Currency.USD, "USD")]
    [InlineData(Currency.GBP, "GBP")]
    [InlineData(Currency.CHF, "CHF")]
    [InlineData(Currency.JPY, "JPY")]
    public void EnumName_IsIso4217Alpha3(Currency currency, string expectedAlpha3)
    {
        currency.ToString().Should().Be(expectedAlpha3);
    }

    [Theory]
    [InlineData(Currency.EUR, (short)978)]
    [InlineData(Currency.USD, (short)840)]
    [InlineData(Currency.JPY, (short)392)]
    public void EnumValue_IsIso4217Numeric(Currency currency, short expectedNumeric)
    {
        ((short)currency).Should().Be(expectedNumeric);
    }

    [Fact]
    public void Parse_FromAlpha3_RoundTrips()
    {
        Enum.Parse<Currency>("USD").Should().Be(Currency.USD);
    }
}
```

- [x] **Step 2: Ejecutar el test y ver que falla**

Run: `dotnet test src/backend/tests/BigSchool.Domain.Tests --filter "FullyQualifiedName~CurrencyTests"`
Expected: FAIL de compilación — `Currency` no existe.

- [x] **Step 3: Crear el enum**

```csharp
// src/backend/src/BigSchool.Domain/Enums/Currency.cs
namespace BigSchool.Domain.Enums;

/// <summary>
/// Monedas soportadas. El NOMBRE del enum es el código ISO 4217 alpha-3 (se persiste como CHAR(3)).
/// El valor numérico es el código ISO 4217 numérico (informativo). Set reducido para la demo; ampliable.
/// </summary>
public enum Currency : short
{
    EUR = 978,
    USD = 840,
    GBP = 826,
    CHF = 756,
    JPY = 392
}
```

- [x] **Step 4: Ejecutar el test y ver que pasa**

Run: `dotnet test src/backend/tests/BigSchool.Domain.Tests --filter "FullyQualifiedName~CurrencyTests"`
Expected: PASS (3+ tests).

- [x] **Step 5: Commit**

```bash
git add -A && git commit -m "feat: añadir enum Currency (ISO 4217) para soporte multimoneda"
```

---

### Task 2: Value Object `Money`

**Files:**
- Create: `src/backend/src/BigSchool.Domain/ValueObjects/Money.cs`
- Test: `src/backend/tests/BigSchool.Domain.Tests/ValueObjects/MoneyTests.cs`

**Nota de diseño:** `Money` es un record inmutable. El constructor primario (posicional) lo usa EF para rehidratar datos ya válidos (owned type en Plan 2B); el dominio crea instancias siempre vía `Money.Create`, que valida invariantes (redondeo a 2 decimales). La negatividad se valida en la entidad `Transaction` en Plan 2B (no se añade aquí — YAGNI).

- [x] **Step 1: Escribir el test que falla**

```csharp
// src/backend/tests/BigSchool.Domain.Tests/ValueObjects/MoneyTests.cs
using BigSchool.Domain.Enums;
using BigSchool.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace BigSchool.Domain.Tests.ValueObjects;

public class MoneyTests
{
    [Fact]
    public void Create_WithValidData_SetsAmountAndCurrency()
    {
        var money = Money.Create(100.50m, Currency.EUR);

        money.Amount.Should().Be(100.50m);
        money.Currency.Should().Be(Currency.EUR);
    }

    [Fact]
    public void Create_RoundsToTwoDecimals_BankersRounding()
    {
        Money.Create(10.005m, Currency.EUR).Amount.Should().Be(10.00m);
        Money.Create(10.015m, Currency.EUR).Amount.Should().Be(10.02m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.01)]
    [InlineData(999999.99)]
    public void Create_AllowsZeroAndPositive(decimal amount)
    {
        var act = () => Money.Create(amount, Currency.USD);
        act.Should().NotThrow();
    }

    [Fact]
    public void Equality_IsByValue()
    {
        Money.Create(10m, Currency.EUR).Should().Be(Money.Create(10m, Currency.EUR));
        Money.Create(10m, Currency.EUR).Should().NotBe(Money.Create(10m, Currency.USD));
    }
}
```

- [x] **Step 2: Ejecutar el test y ver que falla**

Run: `dotnet test src/backend/tests/BigSchool.Domain.Tests --filter "FullyQualifiedName~MoneyTests"`
Expected: FAIL de compilación — `Money` no existe.

- [x] **Step 3: Crear el Value Object**

```csharp
// src/backend/src/BigSchool.Domain/ValueObjects/Money.cs
using BigSchool.Domain.Enums;

namespace BigSchool.Domain.ValueObjects;

/// <summary>
/// Value Object de importe monetario. Inmutable, igualdad por valor.
/// El dominio crea instancias vía <see cref="Create"/>; el constructor posicional lo usa EF (owned type).
/// </summary>
public sealed record Money(decimal Amount, Currency Currency)
{
    /// <summary>Escala de los importes monetarios (simplificación TFM: 2 decimales para todas las monedas).</summary>
    public const int Scale = 2;

    /// <summary>Crea un importe válido, redondeando a la escala de columna con redondeo bancario.</summary>
    public static Money Create(decimal amount, Currency currency)
    {
        var rounded = Math.Round(amount, Scale, MidpointRounding.ToEven);
        return new Money(rounded, currency);
    }
}
```

- [x] **Step 4: Ejecutar el test y ver que pasa**

Run: `dotnet test src/backend/tests/BigSchool.Domain.Tests --filter "FullyQualifiedName~MoneyTests"`
Expected: PASS.

- [x] **Step 5: Commit**

```bash
git add -A && git commit -m "feat: añadir Value Object Money con factory validadora"
```

---

### Task 3: Value Object `MoneyConversion`

**Files:**
- Create: `src/backend/src/BigSchool.Domain/ValueObjects/MoneyConversion.cs`
- Test: `src/backend/tests/BigSchool.Domain.Tests/ValueObjects/MoneyConversionTests.cs`

**Nota de diseño:** `MoneyConversion.Create(Money original, Currency baseCurrency, decimal rate, DateOnly rateDate)` calcula `Base = Money.Create(original.Amount * rate, baseCurrency)`. Si `original.Currency == baseCurrency` exige `rate == 1` (invariante). En Plan 2B será un EF owned type anidado (`MoneyConversion` posee dos `Money`).

- [x] **Step 1: Escribir el test que falla**

```csharp
// src/backend/tests/BigSchool.Domain.Tests/ValueObjects/MoneyConversionTests.cs
using BigSchool.Domain.Enums;
using BigSchool.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace BigSchool.Domain.Tests.ValueObjects;

public class MoneyConversionTests
{
    private static readonly DateOnly RateDate = new(2026, 6, 17);

    [Fact]
    public void Create_DifferentCurrency_ComputesBaseAmount()
    {
        var original = Money.Create(100m, Currency.USD);

        var conversion = MoneyConversion.Create(original, Currency.EUR, rate: 0.92m, RateDate);

        conversion.Original.Should().Be(Money.Create(100m, Currency.USD));
        conversion.Rate.Should().Be(0.92m);
        conversion.Base.Should().Be(Money.Create(92.00m, Currency.EUR));
        conversion.RateDate.Should().Be(RateDate);
    }

    [Fact]
    public void Create_SameCurrency_RequiresRateOne_AndBaseEqualsOriginal()
    {
        var original = Money.Create(50m, Currency.EUR);

        var conversion = MoneyConversion.Create(original, Currency.EUR, rate: 1m, RateDate);

        conversion.Base.Amount.Should().Be(50.00m);
        conversion.Base.Currency.Should().Be(Currency.EUR);
    }

    [Fact]
    public void Create_SameCurrency_WithRateNotOne_Throws()
    {
        var original = Money.Create(50m, Currency.EUR);

        var act = () => MoneyConversion.Create(original, Currency.EUR, rate: 1.1m, RateDate);

        act.Should().Throw<ArgumentException>().WithMessage("*moneda base*");
    }

    [Fact]
    public void Create_WithNonPositiveRate_Throws()
    {
        var original = Money.Create(10m, Currency.USD);

        var act = () => MoneyConversion.Create(original, Currency.EUR, rate: 0m, RateDate);

        act.Should().Throw<ArgumentException>();
    }
}
```

- [x] **Step 2: Ejecutar el test y ver que falla**

Run: `dotnet test src/backend/tests/BigSchool.Domain.Tests --filter "FullyQualifiedName~MoneyConversionTests"`
Expected: FAIL de compilación — `MoneyConversion` no existe.

- [x] **Step 3: Crear el Value Object**

```csharp
// src/backend/src/BigSchool.Domain/ValueObjects/MoneyConversion.cs
using BigSchool.Domain.Enums;

namespace BigSchool.Domain.ValueObjects;

/// <summary>
/// Snapshot de conversión monetaria: importe original, tipo aplicado, importe convertido a la moneda
/// base del usuario y fecha del tipo. Reutilizable en Transaction (Plan 2B), Holding y Valuation (Plan 3).
/// La capa Application resuelve el <paramref name="rate"/>; el dominio nunca llama a servicios externos.
/// </summary>
public sealed record MoneyConversion(Money Original, decimal Rate, Money Base, DateOnly RateDate)
{
    public static MoneyConversion Create(Money original, Currency baseCurrency, decimal rate, DateOnly rateDate)
    {
        if (rate <= 0m)
            throw new ArgumentException("El tipo de cambio debe ser positivo.", nameof(rate));

        if (original.Currency == baseCurrency && rate != 1m)
            throw new ArgumentException(
                "Si la moneda original es la moneda base, el tipo de cambio debe ser 1.", nameof(rate));

        var baseMoney = Money.Create(original.Amount * rate, baseCurrency);
        return new MoneyConversion(original, rate, baseMoney, rateDate);
    }
}
```

- [x] **Step 4: Ejecutar el test y ver que pasa**

Run: `dotnet test src/backend/tests/BigSchool.Domain.Tests --filter "FullyQualifiedName~MoneyConversionTests"`
Expected: PASS.

- [x] **Step 5: Commit**

```bash
git add -A && git commit -m "feat: añadir Value Object MoneyConversion (snapshot de conversión)"
```

---

### Task 4: `CurrencyConverter` compartido + `Users.BaseCurrency` + migración

**Files:**
- Create: `src/backend/src/BigSchool.Infrastructure/Persistence/Converters/CurrencyConverter.cs`
- Modify: `src/backend/src/BigSchool.Domain/Entities/User.cs`
- Modify: `src/backend/src/BigSchool.Infrastructure/Persistence/Configurations/UserConfiguration.cs`
- Modify: `src/backend/tests/BigSchool.Domain.Tests/Entities/UserTests.cs`
- Create (auto, EF): `src/backend/src/BigSchool.Infrastructure/Persistence/Migrations/<timestamp>_AlterUsersAddBaseCurrency.cs`

**Nota de diseño:** El `ValueConverter<Currency,string>` (alpha-3) se define **una vez** y se reutiliza en `UserConfiguration`, `ExchangeRateConfiguration` (Task 5) y `TransactionConfiguration` (Plan 2B). `User.Create` gana un parámetro opcional `Currency baseCurrency = Currency.EUR` para no romper llamadas existentes (`RegisterCommandHandler` no cambia).

- [x] **Step 1: Crear el converter compartido**

```csharp
// src/backend/src/BigSchool.Infrastructure/Persistence/Converters/CurrencyConverter.cs
using BigSchool.Domain.Enums;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BigSchool.Infrastructure.Persistence.Converters;

/// <summary>
/// Conversor EF reutilizable: Currency (enum) -> CHAR(3) usando el nombre ISO 4217 alpha-3.
/// Se reutiliza en Users.BaseCurrency, ExchangeRates y el owned type Money de Transaction (Plan 2B).
/// </summary>
public static class CurrencyConverter
{
    public static readonly ValueConverter<Currency, string> CharIso = new(
        v => v.ToString(),
        v => Enum.Parse<Currency>(v));
}
```

- [x] **Step 2: Escribir el test de dominio que falla**

Añadir a `src/backend/tests/BigSchool.Domain.Tests/Entities/UserTests.cs`:

```csharp
    [Fact]
    public void Create_WithoutBaseCurrency_DefaultsToEur()
    {
        var user = User.Create("test@example.com", "hashedpwd", "salted", "John Doe");

        user.BaseCurrency.Should().Be(Currency.EUR);
    }

    [Fact]
    public void Create_WithExplicitBaseCurrency_SetsIt()
    {
        var user = User.Create("test@example.com", "hashedpwd", "salted", "John Doe", Currency.USD);

        user.BaseCurrency.Should().Be(Currency.USD);
    }
```

(El `using BigSchool.Domain.Enums;` ya está presente en el fichero.)

- [x] **Step 3: Ejecutar el test y ver que falla**

Run: `dotnet test src/backend/tests/BigSchool.Domain.Tests --filter "FullyQualifiedName~UserTests"`
Expected: FAIL de compilación — `User` no tiene `BaseCurrency` ni overload con `Currency`.

- [x] **Step 4: Añadir `BaseCurrency` a `User`**

En `src/backend/src/BigSchool.Domain/Entities/User.cs`:

1. Añadir la propiedad junto a las demás (tras `FullName`):

```csharp
    public Currency BaseCurrency { get; private set; }
```

2. Añadir el parámetro al constructor privado y asignarlo:

```csharp
    private User(string email, string passwordHash, string passwordSalt,
        string fullName, Currency baseCurrency, EntityStatus idStatus, DateTime createdAt)
    {
        Email = email;
        PasswordHash = passwordHash;
        PasswordSalt = passwordSalt;
        FullName = fullName;
        BaseCurrency = baseCurrency;
        IdStatus = idStatus;
        CreatedAt = createdAt;
    }
```

3. Añadir el parámetro opcional a la factory `Create` y pasarlo:

```csharp
    public static User Create(string email, string passwordHash, string passwordSalt,
        string fullName, Currency baseCurrency = Currency.EUR)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash is required.", nameof(passwordHash));
        if (string.IsNullOrWhiteSpace(passwordSalt))
            throw new ArgumentException("Password salt is required.", nameof(passwordSalt));
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full name is required.", nameof(fullName));

        return new User(
            email.Trim().ToLowerInvariant(),
            passwordHash,
            passwordSalt,
            fullName.Trim(),
            baseCurrency,
            EntityStatus.Active,
            DateTime.UtcNow);
    }
```

- [x] **Step 5: Ejecutar el test de dominio y ver que pasa**

Run: `dotnet test src/backend/tests/BigSchool.Domain.Tests --filter "FullyQualifiedName~UserTests"`
Expected: PASS (incluidos los tests existentes de `User`).

- [x] **Step 6: Configurar la columna en `UserConfiguration`**

En `src/backend/src/BigSchool.Infrastructure/Persistence/Configurations/UserConfiguration.cs`, añadir el `using` y, dentro de `ConfigureProperties`, mapear la columna tras `FullName`:

```csharp
using BigSchool.Infrastructure.Persistence.Converters;
```

```csharp
        builder.Property(u => u.BaseCurrency)
            .IsRequired()
            .HasConversion(CurrencyConverter.CharIso)
            .HasColumnType("char(3)")
            .HasDefaultValue(Currency.EUR);
```

**Fallback** si `HasDefaultValue` con converter diera problemas en Pomelo: usar `.HasDefaultValueSql("'EUR'")` en lugar de `.HasDefaultValue(Currency.EUR)`.

- [x] **Step 7: Generar la migración**

Run (desde `src/backend`):
```bash
dotnet ef migrations add AlterUsersAddBaseCurrency \
  --project src/BigSchool.Infrastructure \
  --startup-project src/BigSchool.WebApi \
  --output-dir Persistence/Migrations
```
Expected: se crea `Persistence/Migrations/<timestamp>_AlterUsersAddBaseCurrency.cs` con `AddColumn<string>("BaseCurrency", "Users", type: "char(3)", ... defaultValue: "EUR")`.

**Fallback** si `dotnet ef` no encuentra el `DbContext` (Autofac no lo registra en `builder.Services`): crear `src/BigSchool.Infrastructure/Persistence/BigSchoolDbContextFactory.cs` implementando `IDesignTimeDbContextFactory<BigSchoolDbContext>` que construya `DbContextOptions` con `UseMySql(connString, ServerVersion.AutoDetect(connString))` leyendo `appsettings.json` del WebApi y pase una implementación mínima de `IMediator`. Si `InitialCreate` se generó sin factory, no será necesario.

- [x] **Step 8: Verificar build de toda la solución**

Run: `dotnet build src/backend/Backend.slnx`
Expected: 0 errors.

- [x] **Step 9: Commit**

```bash
git add -A && git commit -m "feat: añadir Users.BaseCurrency y converter de Currency compartido"
```

---

### Task 5: Entidad `ExchangeRate` + configuración EF + migración + seed

**Files:**
- Create: `src/backend/src/BigSchool.Domain/Entities/ExchangeRate.cs`
- Create: `src/backend/src/BigSchool.Infrastructure/Persistence/Configurations/ExchangeRateConfiguration.cs`
- Modify: `src/backend/src/BigSchool.Infrastructure/Persistence/Extensions/SeedDataExtensions.cs`
- Modify: `src/backend/src/BigSchool.Infrastructure/Persistence/BigSchoolDbContext.cs`
- Create (auto, EF): `src/backend/src/BigSchool.Infrastructure/Persistence/Migrations/<timestamp>_CreateExchangeRates.cs`

**Nota de diseño:** `ExchangeRate` NO hereda de `BaseEntity` ni implementa `IAggregateRoot`: es reference data plana. NO se añade `DbSet<ExchangeRate>` al contexto (el acceso es siempre vía Dapper); EF la incluye en el modelo porque `ApplyConfigurationsFromAssembly` recoge `ExchangeRateConfiguration`. Al no tener `IdStatus`, queda fuera del Global Query Filter automáticamente.

- [x] **Step 1: Crear la entidad**

```csharp
// src/backend/src/BigSchool.Domain/Entities/ExchangeRate.cs
using BigSchool.Domain.Enums;

namespace BigSchool.Domain.Entities;

/// <summary>
/// Tipo de cambio cacheado (reference data). NO es Aggregate Root ni BaseEntity: sin DomainEvents,
/// sin IdStatus, sin repositorio. Solo lo lee/escribe ExchangeRateApiClient vía Dapper.
/// </summary>
public class ExchangeRate
{
    public int IdExchangeRate { get; private set; }
    public Currency FromCurrency { get; private set; }
    public Currency ToCurrency { get; private set; }
    public decimal Rate { get; private set; }
    public DateOnly RateDate { get; private set; }
    public string? Source { get; private set; }
    public DateTime FetchedAt { get; private set; }

    protected ExchangeRate() { } // EF Core / Dapper
}
```

- [x] **Step 2: Crear la configuración EF (esquema)**

```csharp
// src/backend/src/BigSchool.Infrastructure/Persistence/Configurations/ExchangeRateConfiguration.cs
using BigSchool.Domain.Entities;
using BigSchool.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BigSchool.Infrastructure.Persistence.Configurations;

public class ExchangeRateConfiguration : IEntityTypeConfiguration<ExchangeRate>
{
    public void Configure(EntityTypeBuilder<ExchangeRate> b)
    {
        b.ToTable("ExchangeRates");
        b.HasKey(e => e.IdExchangeRate);

        b.Property(e => e.IdExchangeRate).ValueGeneratedOnAdd();
        b.Property(e => e.FromCurrency).HasConversion(CurrencyConverter.CharIso).HasColumnType("char(3)");
        b.Property(e => e.ToCurrency).HasConversion(CurrencyConverter.CharIso).HasColumnType("char(3)");
        b.Property(e => e.Rate).HasColumnType("decimal(18,6)");
        b.Property(e => e.RateDate).HasColumnType("date"); // DateOnly nativo en Pomelo 8 / EF 8
        b.Property(e => e.Source).HasMaxLength(100);
        b.Property(e => e.FetchedAt).IsRequired();

        b.HasIndex(e => new { e.FromCurrency, e.ToCurrency, e.RateDate }).IsUnique();
    }
}
```

**Fallback** si Pomelo diera problemas con `DateOnly`: añadir `b.Property(e => e.RateDate).HasConversion<DateOnly, DateTime>(d => d.ToDateTime(TimeOnly.MinValue), dt => DateOnly.FromDateTime(dt));`

- [x] **Step 3: Añadir el seed de tipos fijos**

En `src/backend/src/BigSchool.Infrastructure/Persistence/Extensions/SeedDataExtensions.cs`, añadir un método tras `SeedSubCategories`:

```csharp
    public static ModelBuilder SeedExchangeRates(this ModelBuilder modelBuilder)
    {
        // Tipos fijos para demo determinista (sin red). RateDate = 2026-01-01. Source = "seed".
        modelBuilder.Entity<Domain.Entities.ExchangeRate>().HasData(
            new { IdExchangeRate = 1, FromCurrency = Currency.USD, ToCurrency = Currency.EUR, Rate = 0.920000m, RateDate = new DateOnly(2026, 1, 1), Source = (string?)"seed", FetchedAt = SeedDate },
            new { IdExchangeRate = 2, FromCurrency = Currency.GBP, ToCurrency = Currency.EUR, Rate = 1.170000m, RateDate = new DateOnly(2026, 1, 1), Source = (string?)"seed", FetchedAt = SeedDate },
            new { IdExchangeRate = 3, FromCurrency = Currency.CHF, ToCurrency = Currency.EUR, Rate = 1.060000m, RateDate = new DateOnly(2026, 1, 1), Source = (string?)"seed", FetchedAt = SeedDate },
            new { IdExchangeRate = 4, FromCurrency = Currency.JPY, ToCurrency = Currency.EUR, Rate = 0.006100m, RateDate = new DateOnly(2026, 1, 1), Source = (string?)"seed", FetchedAt = SeedDate }
        );

        return modelBuilder;
    }
```

(El `using BigSchool.Domain.Enums;` ya está al inicio del fichero.)

- [x] **Step 4: Llamar al seed desde el contexto**

En `src/backend/src/BigSchool.Infrastructure/Persistence/BigSchoolDbContext.cs`, dentro de `OnModelCreating`, tras `modelBuilder.SeedSubCategories();`:

```csharp
        modelBuilder.SeedExchangeRates();
```

- [x] **Step 5: Generar la migración**

Run (desde `src/backend`):
```bash
dotnet ef migrations add CreateExchangeRates \
  --project src/BigSchool.Infrastructure \
  --startup-project src/BigSchool.WebApi \
  --output-dir Persistence/Migrations
```
Expected: migración con `CreateTable("ExchangeRates", ...)`, índice único `(FromCurrency, ToCurrency, RateDate)` y 4 `InsertData` del seed.

- [x] **Step 6: Verificar build**

Run: `dotnet build src/backend/Backend.slnx`
Expected: 0 errors.

- [x] **Step 7: Commit**

```bash
git add -A && git commit -m "feat: añadir tabla ExchangeRates (reference data) con seed de tipos fijos"
```

---

### Task 6: `IExchangeRateProvider` + `ExchangeRateSettings` + registro HttpClient

**Files:**
- Create: `src/backend/src/BigSchool.Application/Interfaces/Services/IExchangeRateProvider.cs`
- Modify: `src/backend/src/BigSchool.Application/Configuration/AppSettings.cs`
- Modify: `src/backend/src/BigSchool.WebApi/Program.cs`
- Modify: `src/backend/src/BigSchool.WebApi/appsettings.json`

**Nota de diseño:** El `ExchangeRateApiClient` (Task 7) implementa `IExchangeRateProvider` → Autofac lo auto-registra por `AsImplementedInterfaces` en la capa Infrastructure. Para inyectarle `HttpClient` registramos `IHttpClientFactory` con `builder.Services.AddHttpClient()` (MS DI, puenteado por Autofac); el cliente recibe `IHttpClientFactory`. No usamos typed-client para evitar doble registro con Autofac.

- [ ] **Step 1: Crear la interfaz**

```csharp
// src/backend/src/BigSchool.Application/Interfaces/Services/IExchangeRateProvider.cs
using BigSchool.Domain.Enums;

namespace BigSchool.Application.Interfaces.Services;

public interface IExchangeRateProvider
{
    /// <summary>
    /// Devuelve el tipo de cambio <paramref name="from"/> -> <paramref name="to"/> para la fecha dada.
    /// Atajo: from == to devuelve 1 sin llamada. Resuelve cache (Dapper) -> API externa -> cache.
    /// </summary>
    Task<decimal> GetRateAsync(Currency from, Currency to, DateOnly date, CancellationToken ct = default);
}
```

- [ ] **Step 2: Añadir `ExchangeRateSettings` a `AppSettings`**

En `src/backend/src/BigSchool.Application/Configuration/AppSettings.cs`:

1. Añadir la propiedad a `AppSettings`:

```csharp
    public required ExchangeRateSettings ExchangeRate { get; set; }
```

2. Añadir la clase al final del fichero:

```csharp
public class ExchangeRateSettings
{
    public required string BaseUrl { get; set; }
}
```

- [ ] **Step 3: Bindear settings y registrar HttpClient en `Program.cs`**

En `src/backend/src/BigSchool.WebApi/Program.cs`:

1. Dentro del `builder.Services.Configure<AppSettings>(...)`, añadir tras la línea de `RagService`:

```csharp
        options.ExchangeRate = builder.Configuration.GetSection("ExchangeRate").Get<ExchangeRateSettings>()
            ?? new ExchangeRateSettings { BaseUrl = "https://api.frankfurter.app" };
```

2. Registrar el factory de HttpClient (junto al resto de `builder.Services.Add...`, p.ej. antes de `AddControllers`):

```csharp
    builder.Services.AddHttpClient();
```

- [ ] **Step 4: Añadir la sección a `appsettings.json`**

En `src/backend/src/BigSchool.WebApi/appsettings.json`, añadir a nivel raíz (junto a `RagService`):

```json
  "ExchangeRate": {
    "BaseUrl": "https://api.frankfurter.app"
  }
```

- [ ] **Step 5: Verificar build**

Run: `dotnet build src/backend/Backend.slnx`
Expected: 0 errors.

- [ ] **Step 6: Commit**

```bash
git add -A && git commit -m "feat: añadir IExchangeRateProvider y settings del proveedor de tipos de cambio"
```

---

### Task 7: `ExchangeRateApiClient` (cache Dapper + Frankfurter) + unit tests

**Files:**
- Create: `src/backend/src/BigSchool.Infrastructure/Services/ExchangeRateApiClient.cs`
- Create: `src/backend/src/BigSchool.Infrastructure/Services/FrankfurterResponse.cs`
- Test: `src/backend/tests/BigSchool.Application.Tests/Services/ExchangeRateApiClientTests.cs`

**Nota de diseño:** El cliente: (1) `from == to` → `1m` sin IO; (2) lee cache con Dapper; (3) si *miss*, llama a Frankfurter (`GET {BaseUrl}/{yyyy-MM-dd}?from=FROM&to=TO`), parsea `rates[TO]`; (4) UPSERT idempotente; (5) devuelve el tipo. Fallback si la API falla en *miss*: último tipo conocido (`ORDER BY RateDate DESC`); si tampoco hay, excepción controlada. Los unit tests cubren solo el atajo `from == to` (sin IO); el camino con cache/API se cubre en integración (Task 8).

- [ ] **Step 1: Escribir el test que falla (atajo from == to)**

```csharp
// src/backend/tests/BigSchool.Application.Tests/Services/ExchangeRateApiClientTests.cs
using BigSchool.Application.Configuration;
using BigSchool.Application.Interfaces;
using BigSchool.Domain.Enums;
using BigSchool.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace BigSchool.Application.Tests.Services;

public class ExchangeRateApiClientTests
{
    private static IOptions<AppSettings> Settings() => Options.Create(new AppSettings
    {
        ConnectionString = "ignored",
        Jwt = new JwtSettings { Secret = "x", Issuer = "i", Audience = "a" },
        RagService = new RagServiceSettings { BaseUrl = "http://localhost" },
        ExchangeRate = new ExchangeRateSettings { BaseUrl = "https://api.frankfurter.app" }
    });

    [Fact]
    public async Task GetRateAsync_SameCurrency_ReturnsOne_WithoutDbOrHttp()
    {
        var dbFactory = new Mock<IDbConnectionFactory>(MockBehavior.Strict);   // ninguna llamada permitida
        var httpFactory = new Mock<IHttpClientFactory>(MockBehavior.Strict);   // ninguna llamada permitida

        var client = new ExchangeRateApiClient(dbFactory.Object, httpFactory.Object, Settings());

        var rate = await client.GetRateAsync(Currency.EUR, Currency.EUR, new DateOnly(2026, 6, 17));

        rate.Should().Be(1m);
        dbFactory.VerifyNoOtherCalls();
        httpFactory.VerifyNoOtherCalls();
    }
}
```

- [ ] **Step 2: Ejecutar el test y ver que falla**

Run: `dotnet test src/backend/tests/BigSchool.Application.Tests --filter "FullyQualifiedName~ExchangeRateApiClientTests"`
Expected: FAIL de compilación — `ExchangeRateApiClient` no existe.

- [ ] **Step 3: Crear el DTO de respuesta de Frankfurter**

```csharp
// src/backend/src/BigSchool.Infrastructure/Services/FrankfurterResponse.cs
using System.Text.Json.Serialization;

namespace BigSchool.Infrastructure.Services;

/// <summary>DTO de la respuesta de Frankfurter: { "amount":1.0, "base":"USD", "date":"2026-06-17", "rates":{"EUR":0.92} }.</summary>
public sealed class FrankfurterResponse
{
    [JsonPropertyName("amount")] public decimal Amount { get; set; }
    [JsonPropertyName("base")] public string Base { get; set; } = string.Empty;
    [JsonPropertyName("date")] public string Date { get; set; } = string.Empty;
    [JsonPropertyName("rates")] public Dictionary<string, decimal> Rates { get; set; } = new();
}
```

- [ ] **Step 4: Crear el cliente**

```csharp
// src/backend/src/BigSchool.Infrastructure/Services/ExchangeRateApiClient.cs
using System.Net.Http.Json;
using BigSchool.Application.Configuration;
using BigSchool.Application.Interfaces;
using BigSchool.Application.Interfaces.Services;
using BigSchool.Domain.Enums;
using Dapper;
using Microsoft.Extensions.Options;

namespace BigSchool.Infrastructure.Services;

/// <summary>
/// Anti-corruption layer de tipos de cambio. Cachea en ExchangeRates (Dapper, desacoplado del UoW
/// de negocio) y consulta Frankfurter (ECB) en caso de miss.
/// </summary>
public class ExchangeRateApiClient : IExchangeRateProvider
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ExchangeRateSettings _settings;

    public ExchangeRateApiClient(
        IDbConnectionFactory dbFactory,
        IHttpClientFactory httpFactory,
        IOptions<AppSettings> settings)
    {
        _dbFactory = dbFactory;
        _httpFactory = httpFactory;
        _settings = settings.Value.ExchangeRate;
    }

    public async Task<decimal> GetRateAsync(Currency from, Currency to, DateOnly date, CancellationToken ct = default)
    {
        if (from == to)
            return 1m;

        var cached = await ReadCacheAsync(from, to, date);
        if (cached is not null)
            return cached.Value;

        decimal rate;
        try
        {
            rate = await FetchFromProviderAsync(from, to, date, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Fallback: último tipo conocido para ese par si la API está caída.
            var last = await ReadLastKnownAsync(from, to);
            if (last is not null)
                return last.Value;
            throw new InvalidOperationException(
                $"No se pudo obtener el tipo de cambio {from}->{to} para {date:yyyy-MM-dd} y no hay valor cacheado.", ex);
        }

        await UpsertCacheAsync(from, to, rate, date, "frankfurter");
        return rate;
    }

    private async Task<decimal?> ReadCacheAsync(Currency from, Currency to, DateOnly date)
    {
        const string sql = """
            SELECT Rate FROM ExchangeRates
            WHERE FromCurrency = @From AND ToCurrency = @To AND RateDate = @Date
            LIMIT 1;
            """;
        using var conn = _dbFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<decimal?>(sql,
            new { From = from.ToString(), To = to.ToString(), Date = date.ToDateTime(TimeOnly.MinValue).Date });
    }

    private async Task<decimal?> ReadLastKnownAsync(Currency from, Currency to)
    {
        const string sql = """
            SELECT Rate FROM ExchangeRates
            WHERE FromCurrency = @From AND ToCurrency = @To
            ORDER BY RateDate DESC LIMIT 1;
            """;
        using var conn = _dbFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<decimal?>(sql,
            new { From = from.ToString(), To = to.ToString() });
    }

    private async Task UpsertCacheAsync(Currency from, Currency to, decimal rate, DateOnly date, string source)
    {
        const string sql = """
            INSERT INTO ExchangeRates (FromCurrency, ToCurrency, Rate, RateDate, Source, FetchedAt)
            VALUES (@From, @To, @Rate, @Date, @Source, @Now)
            ON DUPLICATE KEY UPDATE Rate = @Rate, Source = @Source, FetchedAt = @Now;
            """;
        using var conn = _dbFactory.CreateConnection();
        await conn.ExecuteAsync(sql, new
        {
            From = from.ToString(),
            To = to.ToString(),
            Rate = rate,
            Date = date.ToDateTime(TimeOnly.MinValue).Date,
            Source = source,
            Now = DateTime.UtcNow
        });
    }

    private async Task<decimal> FetchFromProviderAsync(Currency from, Currency to, DateOnly date, CancellationToken ct)
    {
        var http = _httpFactory.CreateClient();
        var url = $"{_settings.BaseUrl.TrimEnd('/')}/{date:yyyy-MM-dd}?from={from}&to={to}";
        var response = await http.GetFromJsonAsync<FrankfurterResponse>(url, ct)
            ?? throw new HttpRequestException($"Respuesta vacía del proveedor de tipos para {url}.");

        if (!response.Rates.TryGetValue(to.ToString(), out var rate))
            throw new HttpRequestException($"El proveedor no devolvió tipo para {to} en {url}.");

        return rate;
    }
}
```

- [ ] **Step 5: Ejecutar el test y ver que pasa**

Run: `dotnet test src/backend/tests/BigSchool.Application.Tests --filter "FullyQualifiedName~ExchangeRateApiClientTests"`
Expected: PASS.

- [ ] **Step 6: Verificar build de la solución**

Run: `dotnet build src/backend/Backend.slnx`
Expected: 0 errors.

- [ ] **Step 7: Commit**

```bash
git add -A && git commit -m "feat: añadir ExchangeRateApiClient con cache Dapper y proveedor Frankfurter"
```

---

### Task 8: Tests de integración (migración + cache hit/miss con WireMock)

**Files:**
- Modify: `src/backend/Directory.Build.props` (añadir versión WireMock + MySqlConnector)
- Modify: `src/backend/tests/BigSchool.Integration.Tests/BigSchool.Integration.Tests.csproj`
- Create: `src/backend/tests/BigSchool.Integration.Tests/Fixtures/MySqlDatabaseFixture.cs`
- Create: `src/backend/tests/BigSchool.Integration.Tests/Fixtures/IntegrationCollection.cs`
- Create: `src/backend/tests/BigSchool.Integration.Tests/Persistence/ExchangeRateSeedTests.cs`
- Create: `src/backend/tests/BigSchool.Integration.Tests/Services/ExchangeRateApiClientIntegrationTests.cs`

**Nota de diseño (estrategia de integración para TODA la suite):** Reutilizamos el **servidor MySQL ya levantado por `infra/docker-compose`** (localhost:3306), pero sobre una **BD dedicada `bigschool_test`** que EF migra desde cero (NO usamos `bigschool`: sus tablas las creó `init.sql` y chocarían con las migraciones). El ciclo de vida lo gobierna un **`ICollectionFixture` a nivel de suite** (`MySqlDatabaseFixture` + `IntegrationCollection`): se crea **una sola vez** antes del primer test y su `DisposeAsync` corre **una sola vez** tras el último test de la colección (da igual que lances 1, 2 o todos los tests). Así:
- **`InitializeAsync`** → `DROP DATABASE IF EXISTS bigschool_test` + `CREATE DATABASE` (idempotente, auto-sana un run previo cortado) + `Database.MigrateAsync()` **una vez**.
- **`DisposeAsync`** → `DROP DATABASE IF EXISTS bigschool_test` (limpieza final, garantizada por xUnit salvo crash del proceso, que el drop-at-start cubre).
- **`ResetAsync()`** → entre tests, limpia datos mutables conservando esquema + seed global (en Plan 2A solo `DELETE FROM ExchangeRates WHERE Source <> 'seed'`; Plan 2B amplía con `Transactions`/`Users`).

Al meter todas las clases de integración en **una misma colección**, xUnit las ejecuta **en serie** (no en paralelo) → seguro para una BD compartida. La cadena de conexión sale de la env var `BIGSCHOOL_TEST_MYSQL` (con default a `localhost:3306` root) para alinearla con el `.env` de compose sin hardcodear credenciales en el repo.

Con esto se prueba: (a) la tabla `ExchangeRates` existe con el seed (migraciones aplicadas por el fixture); (b) cache *miss* → el cliente llama a WireMock (simula Frankfurter), guarda en cache y devuelve el tipo; (c) cache *hit* → segunda llamada NO toca WireMock.

**Requisito:** el servicio `mysql` de `infra/docker-compose` debe estar levantado antes de correr los tests de integración (no son CI-friendly sin compose; es una decisión consciente para esta entrega de TFM).

- [ ] **Step 1: Añadir las versiones a `Directory.Build.props`**

En `src/backend/Directory.Build.props`, dentro del `<PropertyGroup>` de versiones, añadir:

```xml
    <WireMockVersion>1.6.7</WireMockVersion>
    <MySqlConnectorVersion>2.3.7</MySqlConnectorVersion>
```

- [ ] **Step 2: Ajustar paquetes del csproj de integración**

En `src/backend/tests/BigSchool.Integration.Tests/BigSchool.Integration.Tests.csproj`:

1. **Eliminar** la referencia a Testcontainers (ya no levantamos contenedor desde el test; reusamos el de compose):

```xml
    <!-- QUITAR: <PackageReference Include="Testcontainers" Version="$(TestcontainersVersion)" /> -->
```

2. **Añadir** dentro del `<ItemGroup>` de `PackageReference`:

```xml
    <PackageReference Include="WireMock.Net" Version="$(WireMockVersion)" />
    <PackageReference Include="Dapper" Version="$(DapperVersion)" />
    <PackageReference Include="MySqlConnector" Version="$(MySqlConnectorVersion)" />
    <PackageReference Include="Moq" Version="$(MoqVersion)" />
```

(Conservar `Microsoft.AspNetCore.Mvc.Testing`: lo usa el E2E de Plan 2B.)

- [ ] **Step 3: Crear el fixture compartido + la colección de integración**

```csharp
// src/backend/tests/BigSchool.Integration.Tests/Fixtures/MySqlDatabaseFixture.cs
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
        ?? "Server=localhost;Port=3306;User ID=root;Password=root;";

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

    /// <summary>Reset entre tests: conserva esquema + seed global, limpia datos mutables.</summary>
    public async Task ResetAsync()
    {
        await using var conn = new MySqlConnection(ConnectionString);
        await conn.OpenAsync();
        // Plan 2A solo tiene ExchangeRates como dato mutable. Plan 2B amplía este reset (Transactions, Users).
        await conn.ExecuteAsync("DELETE FROM ExchangeRates WHERE Source <> 'seed';");
    }
}
```

```csharp
// src/backend/tests/BigSchool.Integration.Tests/Fixtures/IntegrationCollection.cs
using Xunit;

namespace BigSchool.Integration.Tests.Fixtures;

/// <summary>
/// Agrupa TODAS las clases de integración en una colección que comparte un único MySqlDatabaseFixture.
/// Al estar en la misma colección, xUnit las ejecuta en serie (no en paralelo): seguro para una BD compartida.
/// </summary>
[CollectionDefinition(Name)]
public class IntegrationCollection : ICollectionFixture<MySqlDatabaseFixture>
{
    public const string Name = "Integration";
}
```

- [ ] **Step 4: Test del seed de `ExchangeRates` (migraciones ya aplicadas por el fixture)**

```csharp
// src/backend/tests/BigSchool.Integration.Tests/Persistence/ExchangeRateSeedTests.cs
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
```

- [ ] **Step 5: Test de cache hit/miss con WireMock**

```csharp
// src/backend/tests/BigSchool.Integration.Tests/Services/ExchangeRateApiClientIntegrationTests.cs
using BigSchool.Application.Configuration;
using BigSchool.Application.Interfaces;
using BigSchool.Domain.Enums;
using BigSchool.Infrastructure.Persistence;
using BigSchool.Infrastructure.Services;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace BigSchool.Integration.Tests.Services;

[Collection(IntegrationCollection.Name)]
public class ExchangeRateApiClientIntegrationTests : IAsyncLifetime
{
    private readonly MySqlDatabaseFixture _fixture;
    private WireMockServer _wireMock = null!;

    public ExchangeRateApiClientIntegrationTests(MySqlDatabaseFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        // La BD ya está migrada por el fixture de la suite; solo limpiamos datos mutables y arrancamos el fake.
        await _fixture.ResetAsync();
        _wireMock = WireMockServer.Start();
    }

    public Task DisposeAsync()
    {
        _wireMock.Stop();
        return Task.CompletedTask;
    }

    private AppSettings BuildSettings() => new()
    {
        ConnectionString = _fixture.ConnectionString,
        Jwt = new JwtSettings { Secret = "x", Issuer = "i", Audience = "a" },
        RagService = new RagServiceSettings { BaseUrl = "http://localhost" },
        ExchangeRate = new ExchangeRateSettings { BaseUrl = _wireMock.Url! }
    };

    private ExchangeRateApiClient CreateClient()
    {
        var settings = Options.Create(BuildSettings());
        var dbFactory = new DbConnectionMySqlFactory(settings);

        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient());

        return new ExchangeRateApiClient(dbFactory, httpFactory.Object, settings);
    }

    [Fact]
    public async Task GetRateAsync_CacheMissThenHit_CallsProviderOnce()
    {
        // Par no sembrado para forzar miss en la primera llamada.
        var date = new DateOnly(2026, 6, 10);
        _wireMock
            .Given(Request.Create().WithPath($"/{date:yyyy-MM-dd}").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody($$"""{"amount":1.0,"base":"USD","date":"{{date:yyyy-MM-dd}}","rates":{"GBP":0.85}}"""));

        var client = CreateClient();

        var first = await client.GetRateAsync(Currency.USD, Currency.GBP, date);   // miss -> provider
        var second = await client.GetRateAsync(Currency.USD, Currency.GBP, date);  // hit -> cache

        first.Should().Be(0.85m);
        second.Should().Be(0.85m);
        _wireMock.LogEntries.Should().HaveCount(1); // el proveedor se llamó una sola vez
    }
}
```

- [ ] **Step 6: Ejecutar los tests de integración**

Prerequisito: el servicio `mysql` de `infra/docker-compose` debe estar levantado (`docker compose -f infra/docker-compose.yml -f infra/docker-compose.override.yml up -d mysql`). Si las credenciales del `.env` difieren del default, exportar `BIGSCHOOL_TEST_MYSQL` (p. ej. `Server=localhost;Port=3306;User ID=root;Password=<MYSQL_ROOT_PASSWORD>;`).

Run: `dotnet test src/backend/tests/BigSchool.Integration.Tests`
Expected: PASS. El fixture crea/migra/destruye `bigschool_test` una sola vez; no toca la BD `bigschool` de desarrollo.

- [ ] **Step 7: Commit**

```bash
git add -A && git commit -m "test: integración de ExchangeRates (migración + cache hit/miss con WireMock)"
```

---

### Task 9: Documentación (ADR-006 + design + diario)

**Files:**
- Modify: `docs/01-arquitectura.md`
- Modify: `docs/02-backend-design.md`
- Modify: `docs/diario.md`

- [ ] **Step 1: Añadir ADR-006 en `docs/01-arquitectura.md`**

Añadir al final de la sección de ADRs un nuevo apartado **ADR-006: Soporte multimoneda**, cubriendo: moneda base por usuario (`Users.BaseCurrency`); modelo de conversión por snapshot (`MoneyConversion`: original + tipo + convertido + fecha, sin reconversión retroactiva); `Currency` como enum persistido `CHAR(3)`; proveedor externo (Frankfurter/ECB) + cache `ExchangeRates` con Dapper desacoplado del UoW; y las simplificaciones TFM (set reducido de divisas, `DECIMAL(18,2)`/`(18,6)`, cache por día). Mantener el formato de los ADR existentes (Contexto / Decisión / Consecuencias).

- [ ] **Step 2: Actualizar `docs/02-backend-design.md`**

- `Users`: añadir fila `BaseCurrency | CHAR(3) | NOT NULL, DEFAULT 'EUR'`.
- Nueva tabla `ExchangeRates` (columnas de la spec §5.1: IdExchangeRate, FromCurrency, ToCurrency, Rate DECIMAL(18,6), RateDate DATE, Source, FetchedAt; `UNIQUE(FromCurrency, ToCurrency, RateDate)`), marcada como reference data (no AR, no soft-delete).
- Sección Enums: añadir `Currency` (ISO 4217 alpha-3, set reducido).
- Sección Value Objects: añadir `Money` y `MoneyConversion` (columnas del snapshot: OriginalAmount, OriginalCurrency, ExchangeRate, BaseAmount, BaseCurrency, RateDate).
- Sección interfaces/services: añadir `IExchangeRateProvider` / `ExchangeRateApiClient`.
- Nota: prever `Currency` + snapshot en `Holdings`/`Valuations` para Plan 3.

- [ ] **Step 3: Actualizar `docs/diario.md`**

Añadir entrada con fecha indicando que se han implementado los cimientos multimoneda (Plan 2A): `Currency`/`Money`/`MoneyConversion`, `Users.BaseCurrency` + migración, tabla `ExchangeRates` + seed, `IExchangeRateProvider`/`ExchangeRateApiClient` (Frankfurter + cache Dapper), y que queda pendiente el BC Transaction (Plan 2B).

- [ ] **Step 4: Commit**

```bash
git add -A && git commit -m "docs: ADR-006 multimoneda, actualizar diseño backend y diario (Plan 2A)"
```

---

## Verificación final del Plan 2A

- [ ] **Build completo**

Run: `dotnet build src/backend/Backend.slnx`
Expected: 0 errors.

- [ ] **Toda la suite de tests**

Run: `dotnet test src/backend/Backend.slnx`
Expected: todos los tests PASS (los de integración requieren Docker).

- [ ] **Checklist de cobertura de la spec**

- [ ] `Currency` enum (spec §3.1) — Task 1
- [ ] `Money` VO (spec §3.2) — Task 2
- [ ] `MoneyConversion` VO (spec §3.3) — Task 3
- [ ] `Users.BaseCurrency` + migración (spec §4) — Task 4
- [ ] `ExchangeRate` + config + tabla + seed (spec §5.1, §5.3) — Task 5
- [ ] `IExchangeRateProvider` (spec §5.1) — Task 6
- [ ] `ExchangeRateApiClient` (Dapper UPSERT + Frankfurter, spec §5.1, §5.2) — Task 7
- [ ] Tests integración cache hit/miss + migración (spec §9) — Task 8
- [ ] ADR-006 + docs (spec §8) — Task 9

**Pendiente para Plan 2B:** entidad `Transaction` (AR completa con `MoneyConversion` owned type), `TransactionConfiguration`, migración `Transactions`, `ITransactionRepository`, CQRS (Create/Update/Delete + queries Dapper consolidadas en base), validators, endpoints `/api/v1/transactions` y `/api/v1/categories`, flujo de conversión en `CreateTransactionHandler`.
