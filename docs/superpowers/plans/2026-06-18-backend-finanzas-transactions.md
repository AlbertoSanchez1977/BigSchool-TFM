# Backend — BC Finanzas Personales: Transaction (Plan 2B) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementar el Bounded Context de Finanzas Personales sobre el agregado `Transaction`: entidad completa con `MoneyConversion` (snapshot multimoneda), persistencia EF (owned type + migración), repositorio, CQRS (commands EF Core con flujo de conversión vía `IExchangeRateProvider`, queries Dapper consolidadas en moneda base) y endpoints `/api/v1/transactions` y `/api/v1/categories`. Diseño aprobado: `docs/superpowers/specs/2026-06-17-backend-finanzas-multicurrency-design.md` (§6, §7).

**Architecture:** Clean Architecture + CQRS + DDD. `Transaction` es Aggregate Root independiente con factory `Create`, `Update`, soft-delete y `TransactionCreatedEvent`. La conversión vive en Application: el handler lee `BaseCurrency` del usuario, resuelve `rate` con `IExchangeRateProvider` (creado en Plan 2A) y pasa el `rate` a la factory; el dominio calcula `BaseAmount` y construye el `MoneyConversion`. Commands usan EF Core (UoW + Domain Events); Queries usan Dapper (`IDbConnectionFactory`) y **consolidan siempre sobre `BaseAmount`**.

**Tech Stack:** .NET 8, EF Core 8.0.11 + Pomelo MySQL 8.0.2 (owned types, `DateOnly`), Dapper 2.1.35, MediatR 12, FluentValidation 11, FluentAssertions + xUnit + Moq, MySQL real de docker-compose (BD `bigschool_test`) + `WebApplicationFactory<Program>` (E2E de integración).

**Dependencias previas (Plan 2A debe estar mergeado):** `Currency` (enum), `Money`, `MoneyConversion` (VOs en `BigSchool.Domain.ValueObjects`), `Users.BaseCurrency`, `IExchangeRateProvider`, `CurrencyConverter.CharIso`. Además ya existen de Plan 1: `BaseEntity` (DomainEvents), `IAggregateRoot`, `IDomainEvent`, `EFRepository<T,Y>`, `IRepository<T,Y>`, `BigSchoolDbContext` (`ApplyConfigurationsFromAssembly`, Global Query Filter por `IdStatus`), `IDbConnectionFactory`, `MainCategory`/`TransactionType` (enums), `SubCategory` (hija de `User`), `ApiResponse`/`ApiResponse<T>`/`MetaData`, `ExceptionHandlingMiddleware` (mapea `ValidationException`→400, `NotFoundException`→404, `ConflictException`→409), `ValidationBehavior`, patrón controller (`AuthController`), `IJwtService.GenerateToken(userId, email)` (acuña el JWT real). Además, de **Plan 2A (Task 8)** ya existe el fixture compartido de integración `MySqlDatabaseFixture` + `IntegrationCollection` (un solo crear/migrar/destruir por ejecución sobre `bigschool_test`, con `ResetAsync()` entre tests), que este plan **amplía** para incluir `Transactions`/`Users`.

---

## Decisiones de Diseño (aprobadas — de la spec)

1. **`Transaction` es AR independiente** (no hija de `User`); se relaciona con el usuario por `IdUser` (FK escalar, sin navegación), igual de simple que el resto del modelo.
2. **Snapshot multimoneda**: la transacción guarda un `MoneyConversion` (owned type anidado). Columnas: `OriginalAmount, OriginalCurrency, ExchangeRate, BaseAmount, BaseCurrency, RateDate`. `BaseAmount` se calcula en la factory (`Original.Amount * Rate`).
3. **Flujo de conversión en Application** (spec §6): el handler lee `BaseCurrency`, `rate = (currency == base) ? 1 : await provider.GetRateAsync(currency, base, date, ct)`, llama a `Transaction.Create(...)`, `SaveChanges` + evento. El dominio permanece puro (rate inyectado).
4. **Commands con EF Core; Queries con Dapper consolidando sobre `BaseAmount`** (moneda base del usuario). Opcional: desglose por moneda original (fuera de alcance mínimo — YAGNI).
5. **Endpoint de creación añade `currency` al body** con default a la moneda base del usuario si se omite.
6. **Validaciones de dominio**: importe > 0, fecha no nula, categoría válida (`MainCategory`), `SubCategory` opcional. Soft-delete vía `IdStatus = Deleted` (Global Query Filter ya lo excluye).
7. **Convención SQL/Dapper (obligatoria en este plan)**: todas las queries Dapper se escriben con el **SQL como `private const string` a nivel de clase en UPPERCASE terminado en `_QUERY`** y los **parámetros con `DynamicParameters`** (no objetos anónimos). Ver `src/backend/AGENTS.md` › *Dapper y SQL*. Los bloques de código de las Tasks 6 y 7 ya están escritos según esta convención. Además, la Task 11 (final) refactoriza `ExchangeRateApiClient` (Plan 2A) a esta misma convención — deuda registrada en el Anexo A de Plan 2A.

---

## Modelo de datos `Transactions` (resumen)

| Campo | Tipo | Restricciones |
|---|---|---|
| IdTransaction | INT | PK, AUTO_INCREMENT |
| IdUser | INT | NOT NULL, FK Users |
| Type | SMALLINT | NOT NULL (TransactionType) |
| IdMainCategory | INT | NOT NULL (MainCategory) |
| IdSubCategory | INT | NULL, FK SubCategories |
| Description | VARCHAR(255) | NULL |
| TransactionDate | DATE | NOT NULL |
| OriginalAmount | DECIMAL(18,2) | NOT NULL |
| OriginalCurrency | CHAR(3) | NOT NULL |
| ExchangeRate | DECIMAL(18,6) | NOT NULL |
| BaseAmount | DECIMAL(18,2) | NOT NULL |
| BaseCurrency | CHAR(3) | NOT NULL |
| RateDate | DATE | NOT NULL |
| IdStatus | SMALLINT | NOT NULL (EntityStatus, soft-delete) |
| CreatedAt | DATETIME | NOT NULL |
| UpdatedAt | DATETIME | NULL |

---

## Resumen de Tareas

| # | Tarea | Capa | Descripción |
|---|-------|------|-------------|
| 1 | Entidad `Transaction` (AR) + evento | Domain | Factory `Create`/`Update`/`Delete` con `MoneyConversion` + `TransactionCreatedEvent` + tests |
| 2 | `TransactionConfiguration` (owned type) + DbSet + migración | Infra | Mapeo owned anidado, FK, filtro, tabla `Transactions` |
| 3 | `ITransactionRepository` + `TransactionRepository` | App/Infra | Repo EF (Add + GetById incl. soft-delete) |
| 4 | `CreateTransaction` (command + handler + validator) | App | Flujo de conversión vía `IExchangeRateProvider` + tests |
| 5 | `UpdateTransaction` + `DeleteTransaction` | App | Update (re-conversión) y soft-delete + tests |
| 6 | Queries Dapper (consolidadas en base) | App | GetById, GetTransactions (filtros+paginación), Summary, MonthlyChart |
| 7 | `GetCategories` query | App | MainCategory (enum) + SubCategories del usuario |
| 8 | `TransactionsController` + `CategoriesController` | WebApi | Endpoints REST con envelope `ApiResponse` |
| 9 | Tests E2E de endpoints (happy path + 401) | Integration.Tests | `WebApplicationFactory` + JWT real + verificación física en MySQL |
| 10 | Documentación (design + diario) | docs | Cierre documental Plan 2B |
| 11 | Refactor `ExchangeRateApiClient` a convención SQL/Dapper | Infra | `const` UPPERCASE `_QUERY` + `DynamicParameters` (deuda Anexo A Plan 2A) |

---

### Task 1: Entidad `Transaction` (AR) + `TransactionCreatedEvent`

**Files:**
- Rewrite: `src/backend/src/BigSchool.Domain/Entities/Transaction.cs` (actualmente vacío)
- Create: `src/backend/src/BigSchool.Domain/Events/TransactionCreatedEvent.cs`
- Test: `src/backend/tests/BigSchool.Domain.Tests/Entities/TransactionTests.cs`

**Nota de diseño:** `Transaction.Create(int userId, TransactionType type, MainCategory category, int? subCategoryId, string? description, Money original, Currency baseCurrency, decimal rate, DateOnly transactionDate, DateOnly rateDate)`. La factory valida (importe > 0, userId > 0) y construye el `MoneyConversion` vía `MoneyConversion.Create(original, baseCurrency, rate, rateDate)`. `Update` permite cambiar categoría/descr./fecha/importe re-construyendo el snapshot (recibe `rate` ya resuelto). `Delete` marca `IdStatus = Deleted`. Implementa `IAggregateRoot` y hereda `BaseEntity`.

- [x] **Step 1: Escribir el test que falla**

```csharp
// src/backend/tests/BigSchool.Domain.Tests/Entities/TransactionTests.cs
using BigSchool.Domain.Entities;
using BigSchool.Domain.Enums;
using BigSchool.Domain.Events;
using BigSchool.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace BigSchool.Domain.Tests.Entities;

public class TransactionTests
{
    private static readonly DateOnly TxDate = new(2026, 6, 17);

    private static Transaction CreateUsd()
        => Transaction.Create(
            userId: 1,
            type: TransactionType.Expense,
            category: MainCategory.Luxuries,
            subCategoryId: 11,
            description: "Cena en Londres",
            original: Money.Create(100m, Currency.USD),
            baseCurrency: Currency.EUR,
            rate: 0.92m,
            transactionDate: TxDate,
            rateDate: TxDate);

    [Fact]
    public void Create_DifferentCurrency_BuildsConversionSnapshot()
    {
        var tx = CreateUsd();

        tx.IdUser.Should().Be(1);
        tx.Type.Should().Be(TransactionType.Expense);
        tx.IdMainCategory.Should().Be(MainCategory.Luxuries);
        tx.IdSubCategory.Should().Be(11);
        tx.TransactionDate.Should().Be(TxDate);
        tx.Conversion.Original.Should().Be(Money.Create(100m, Currency.USD));
        tx.Conversion.Rate.Should().Be(0.92m);
        tx.Conversion.Base.Should().Be(Money.Create(92.00m, Currency.EUR));
        tx.IdStatus.Should().Be(EntityStatus.Active);
    }

    [Fact]
    public void Create_SameCurrency_RateOne_BaseEqualsOriginal()
    {
        var tx = Transaction.Create(1, TransactionType.Income, MainCategory.Salary, null, "Nómina",
            Money.Create(2000m, Currency.EUR), Currency.EUR, rate: 1m, TxDate, TxDate);

        tx.Conversion.Base.Should().Be(Money.Create(2000m, Currency.EUR));
    }

    [Fact]
    public void Create_RaisesTransactionCreatedEvent()
    {
        var tx = CreateUsd();

        tx.DomainEvents.Should().ContainSingle(e => e is TransactionCreatedEvent);
    }

    [Fact]
    public void Create_WithNonPositiveAmount_Throws()
    {
        var act = () => Transaction.Create(1, TransactionType.Expense, MainCategory.Luxuries, null, null,
            Money.Create(0m, Currency.EUR), Currency.EUR, 1m, TxDate, TxDate);

        act.Should().Throw<ArgumentException>().WithMessage("*importe*");
    }

    [Fact]
    public void Update_RebuildsSnapshot_AndSetsUpdatedAt()
    {
        var tx = CreateUsd();

        tx.Update(TransactionType.Expense, MainCategory.EssentialExpenses, null, "Corregido",
            Money.Create(50m, Currency.USD), Currency.EUR, rate: 0.90m, TxDate, TxDate);

        tx.IdMainCategory.Should().Be(MainCategory.EssentialExpenses);
        tx.Conversion.Base.Should().Be(Money.Create(45.00m, Currency.EUR));
        tx.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Delete_SetsStatusDeleted()
    {
        var tx = CreateUsd();

        tx.Delete();

        tx.IdStatus.Should().Be(EntityStatus.Deleted);
    }
}
```

- [x] **Step 2: Ejecutar el test y ver que falla**

Run: `dotnet test src/backend/tests/BigSchool.Domain.Tests --filter "FullyQualifiedName~TransactionTests"`
Expected: FAIL de compilación — `Transaction`/`TransactionCreatedEvent` no existen.

- [x] **Step 3: Crear el evento de dominio**

**Convención de eventos de dominio (obligatoria):** los `DomainEvent` reciben **la entidad de dominio completa** (es una referencia/puntero), no parámetros individuales. El handler extrae del agregado lo que necesite. Solo si un dato necesario no estuviera en la entidad se añadiría como parámetro extra del record. Esto mantiene el evento desacoplado de la forma concreta del consumidor.

```csharp
// src/backend/src/BigSchool.Domain/Events/TransactionCreatedEvent.cs
using BigSchool.Domain.Entities;

namespace BigSchool.Domain.Events;

public sealed record TransactionCreatedEvent(Transaction Transaction) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
```

- [x] **Step 4: Implementar la entidad `Transaction`**

```csharp
// src/backend/src/BigSchool.Domain/Entities/Transaction.cs
using BigSchool.Domain.Enums;
using BigSchool.Domain.Events;
using BigSchool.Domain.ValueObjects;

namespace BigSchool.Domain.Entities;

public class Transaction : BaseEntity, IAggregateRoot
{
    public int IdTransaction { get; private set; }
    public int IdUser { get; private set; }
    public TransactionType Type { get; private set; }
    public MainCategory IdMainCategory { get; private set; }
    public int? IdSubCategory { get; private set; }
    public string? Description { get; private set; }
    public DateOnly TransactionDate { get; private set; }
    public MoneyConversion Conversion { get; private set; } = null!;
    public EntityStatus IdStatus { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    protected Transaction() { } // EF Core

    private Transaction(
        int userId,
        TransactionType type,
        MainCategory category,
        int? subCategoryId,
        string? description,
        DateOnly transactionDate,
        MoneyConversion conversion,
        EntityStatus idStatus,
        DateTime createdAt)
    {
        IdUser = userId;
        Type = type;
        IdMainCategory = category;
        IdSubCategory = subCategoryId;
        Description = description;
        TransactionDate = transactionDate;
        Conversion = conversion;
        IdStatus = idStatus;
        CreatedAt = createdAt;
    }

    public static Transaction Create(
        int userId,
        TransactionType type,
        MainCategory category,
        int? subCategoryId,
        string? description,
        Money original,
        Currency baseCurrency,
        decimal rate,
        DateOnly transactionDate,
        DateOnly rateDate)
    {
        if (userId <= 0)
            throw new ArgumentException("El identificador de usuario es obligatorio.", nameof(userId));
        if (original.Amount <= 0m)
            throw new ArgumentException("El importe debe ser mayor que cero.", nameof(original));

        var transaction = new Transaction(
            userId,
            type,
            category,
            subCategoryId,
            description?.Trim(),
            transactionDate,
            MoneyConversion.Create(original, baseCurrency, rate, rateDate),
            EntityStatus.Active,
            DateTime.UtcNow);

        transaction.RaiseDomainEvent(new TransactionCreatedEvent(transaction));

        return transaction;
    }

    public void Update(
        TransactionType type,
        MainCategory category,
        int? subCategoryId,
        string? description,
        Money original,
        Currency baseCurrency,
        decimal rate,
        DateOnly transactionDate,
        DateOnly rateDate)
    {
        if (original.Amount <= 0m)
            throw new ArgumentException("El importe debe ser mayor que cero.", nameof(original));

        Type = type;
        IdMainCategory = category;
        IdSubCategory = subCategoryId;
        Description = description?.Trim();
        TransactionDate = transactionDate;
        Conversion = MoneyConversion.Create(original, baseCurrency, rate, rateDate);
        UpdatedAt = DateTime.UtcNow;
    }

    public void Delete()
    {
        IdStatus = EntityStatus.Deleted;
        UpdatedAt = DateTime.UtcNow;
    }
}
```

- [x] **Step 5: Ejecutar el test y ver que pasa**

Run: `dotnet test src/backend/tests/BigSchool.Domain.Tests --filter "FullyQualifiedName~TransactionTests"`
Expected: PASS.

- [x] **Step 6: Commit**

```bash
git add -A && git commit -m "feat: implementar agregado Transaction con snapshot MoneyConversion y evento"
```

---

### Task 2: `TransactionConfiguration` (owned type) + DbSet + migración

**Files:**
- Create: `src/backend/src/BigSchool.Infrastructure/Persistence/Configurations/TransactionConfiguration.cs`
- Modify: `src/backend/src/BigSchool.Infrastructure/Persistence/BigSchoolDbContext.cs`
- Create (auto, EF): `src/backend/src/BigSchool.Infrastructure/Persistence/Migrations/<timestamp>_CreateTransactions.cs`

**Nota de diseño:** `Conversion` es owned type anidado: `OwnsOne(t => t.Conversion, ...)` que a su vez `OwnsOne` sobre `Original` y `Base` (cada uno → columnas `*Amount`/`*Currency`). El `Currency` se mapea con `CurrencyConverter.CharIso`. La FK a `Users` es escalar (`IdUser`), sin navegación. Global Query Filter por `IdStatus != Deleted` (igual que `User`). Se añade `DbSet<Transaction>` al contexto.

- [x] **Step 1: Crear la configuración**

```csharp
// src/backend/src/BigSchool.Infrastructure/Persistence/Configurations/TransactionConfiguration.cs
using BigSchool.Domain.Entities;
using BigSchool.Domain.Enums;
using BigSchool.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BigSchool.Infrastructure.Persistence.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> b)
    {
        b.ToTable("Transactions");
        b.HasKey(t => t.IdTransaction);

        b.Property(t => t.IdTransaction).ValueGeneratedOnAdd();
        b.Property(t => t.IdUser).IsRequired();
        b.Property(t => t.Type).IsRequired().HasConversion<short>();
        b.Property(t => t.IdMainCategory).IsRequired().HasConversion<int>();
        b.Property(t => t.IdSubCategory);
        b.Property(t => t.Description).HasMaxLength(255);
        b.Property(t => t.TransactionDate).HasColumnType("date").IsRequired();
        b.Property(t => t.IdStatus).IsRequired().HasDefaultValue(EntityStatus.Active).HasConversion<short>();
        b.Property(t => t.CreatedAt).IsRequired();
        b.Property(t => t.UpdatedAt);

        ConfigureConversion(b);
        ConfigureRelationships(b);

        b.HasIndex(t => new { t.IdUser, t.TransactionDate });
        b.HasQueryFilter(t => t.IdStatus != EntityStatus.Deleted);

        b.Ignore(t => t.DomainEvents);
    }

    private static void ConfigureConversion(EntityTypeBuilder<Transaction> b)
    {
        b.OwnsOne(t => t.Conversion, conv =>
        {
            conv.Property(c => c.Rate).HasColumnName("ExchangeRate").HasColumnType("decimal(18,6)");
            conv.Property(c => c.RateDate).HasColumnName("RateDate").HasColumnType("date");

            conv.OwnsOne(c => c.Original, orig =>
            {
                orig.Property(m => m.Amount).HasColumnName("OriginalAmount").HasColumnType("decimal(18,2)");
                orig.Property(m => m.Currency).HasColumnName("OriginalCurrency")
                    .HasConversion(CurrencyConverter.CharIso).HasColumnType("char(3)");
            });

            conv.OwnsOne(c => c.Base, baseMoney =>
            {
                baseMoney.Property(m => m.Amount).HasColumnName("BaseAmount").HasColumnType("decimal(18,2)");
                baseMoney.Property(m => m.Currency).HasColumnName("BaseCurrency")
                    .HasConversion(CurrencyConverter.CharIso).HasColumnType("char(3)");
            });
        });

        b.Navigation(t => t.Conversion).IsRequired();
    }

    private static void ConfigureRelationships(EntityTypeBuilder<Transaction> b)
    {
        // FK escalar a Users (sin navegación) y a SubCategories (opcional).
        b.HasOne<User>().WithMany().HasForeignKey(t => t.IdUser)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne<SubCategory>().WithMany().HasForeignKey(t => t.IdSubCategory)
            .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
    }
}
```

- [x] **Step 2: Registrar el DbSet en el contexto**

En `src/backend/src/BigSchool.Infrastructure/Persistence/BigSchoolDbContext.cs`, sustituir la línea comentada `// public DbSet<Transaction> Transactions => Set<Transaction>();` por:

```csharp
    public DbSet<Transaction> Transactions => Set<Transaction>();
```

- [x] **Step 3: Generar la migración**

Run (desde `src/backend`):
```bash
dotnet ef migrations add CreateTransactions \
  --project src/BigSchool.Infrastructure \
  --startup-project src/BigSchool.WebApi \
  --output-dir Persistence/Migrations
```
Expected: `CreateTable("Transactions", ...)` con las columnas del snapshot (`OriginalAmount`, `OriginalCurrency`, `ExchangeRate`, `BaseAmount`, `BaseCurrency`, `RateDate`), FKs a `Users` y `SubCategories`, índice `(IdUser, TransactionDate)`.

- [x] **Step 4: Verificar build**

Run: `dotnet build src/backend/Backend.slnx`
Expected: 0 errors.

- [x] **Step 5: Commit**

```bash
git add -A && git commit -m "feat: configurar persistencia de Transactions (owned MoneyConversion) y migración"
```

---

### Task 3: `ITransactionRepository` + `TransactionRepository`

**Files:**
- Create: `src/backend/src/BigSchool.Application/Interfaces/Repositories/ITransactionRepository.cs`
- Create: `src/backend/src/BigSchool.Infrastructure/Persistence/Repositories/TransactionRepository.cs`

**Nota de diseño:** El repo se usa solo para escrituras (commands). Hereda de `EFRepository<Transaction, int>` (aporta `AddAsync`, `GetByIdAsync`, `UnitOfWork`). `GetByIdAsync` con el Global Query Filter ya excluye borradas. Las lecturas de listados/reportes van por Dapper (Tasks 6-7), no por el repo.

- [x] **Step 1: Crear la interfaz**

```csharp
// src/backend/src/BigSchool.Application/Interfaces/Repositories/ITransactionRepository.cs
using BigSchool.Application.Interfaces;
using BigSchool.Domain.Entities;

namespace BigSchool.Application.Interfaces.Repositories;

public interface ITransactionRepository : IRepository<Transaction, int>
{
}
```

- [x] **Step 2: Crear la implementación**

```csharp
// src/backend/src/BigSchool.Infrastructure/Persistence/Repositories/TransactionRepository.cs
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Domain.Entities;

namespace BigSchool.Infrastructure.Persistence.Repositories;

public class TransactionRepository : EFRepository<Transaction, int>, ITransactionRepository
{
    public TransactionRepository(BigSchoolDbContext context) : base(context)
    {
    }
}
```

- [x] **Step 3: Verificar build**

Run: `dotnet build src/backend/Backend.slnx`
Expected: 0 errors (Autofac auto-registra `ITransactionRepository` → `TransactionRepository` por `AsImplementedInterfaces`).

- [x] **Step 4: Commit**

```bash
git add -A && git commit -m "feat: añadir ITransactionRepository y repositorio EF de Transaction"
```

---

### Task 4: `CreateTransaction` (command + handler + validator)

**Files:**
- Create: `src/backend/src/BigSchool.Application/Commands/Transactions/Create/CreateTransactionCommand.cs`
- Create: `src/backend/src/BigSchool.Application/Commands/Transactions/Create/CreateTransactionCommandHandler.cs`
- Create: `src/backend/src/BigSchool.Application/Commands/Transactions/Create/CreateTransactionCommandValidator.cs`
- Create: `src/backend/src/BigSchool.Application/DTOs/Transactions/TransactionDto.cs`
- Test: `src/backend/tests/BigSchool.Application.Tests/Commands/Transactions/CreateTransactionCommandHandlerTests.cs`

**Nota de diseño:** El command lleva `IdUser` (lo inyecta el controller desde el JWT, no el body), `Type`, `IdMainCategory`, `IdSubCategory?`, `Description?`, `TransactionDate`, `Amount`, `Currency?` (default a la base del usuario si null). El handler: carga el usuario (`IUserRepository.GetByIdAsync`), determina `currency = command.Currency ?? user.BaseCurrency`, `rate = (currency == base) ? 1 : await provider.GetRateAsync(currency, base, txDate, ct)`, `Transaction.Create(...)`, `AddAsync` + `SaveChangesAsync()`. Si el usuario no existe → `NotFoundException`.

- [x] **Step 1: Crear el DTO de respuesta**

```csharp
// src/backend/src/BigSchool.Application/DTOs/Transactions/TransactionDto.cs
using BigSchool.Domain.Enums;

namespace BigSchool.Application.DTOs.Transactions;

public record TransactionDto(
    int IdTransaction,
    TransactionType Type,
    MainCategory IdMainCategory,
    int? IdSubCategory,
    string? Description,
    DateOnly TransactionDate,
    decimal OriginalAmount,
    Currency OriginalCurrency,
    decimal ExchangeRate,
    decimal BaseAmount,
    Currency BaseCurrency,
    DateOnly RateDate);
```

- [x] **Step 2: Escribir el test del handler que falla**

```csharp
// src/backend/tests/BigSchool.Application.Tests/Commands/Transactions/CreateTransactionCommandHandlerTests.cs
using BigSchool.Application.Commands.Transactions.Create;
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Application.Interfaces.Services;
using BigSchool.Domain.Entities;
using BigSchool.Domain.Enums;
using BigSchool.Domain.Exceptions;
using BigSchool.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace BigSchool.Application.Tests.Commands.Transactions;

public class CreateTransactionCommandHandlerTests
{
    private readonly Mock<ITransactionRepository> _txRepo = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IExchangeRateProvider> _rates = new();
    private readonly CreateTransactionCommandHandler _handler;

    public CreateTransactionCommandHandlerTests()
    {
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<bool>())).ReturnsAsync(1);
        _txRepo.Setup(r => r.UnitOfWork).Returns(uow.Object);

        _handler = new CreateTransactionCommandHandler(_txRepo.Object, _userRepo.Object, _rates.Object);
    }

    private static User UserWithBase(Currency baseCurrency)
        => User.Create("u@test.com", "h", "s", "User", baseCurrency);

    [Fact]
    public async Task Handle_ForeignCurrency_ResolvesRateAndConverts()
    {
        _userRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserWithBase(Currency.EUR));
        _rates.Setup(r => r.GetRateAsync(Currency.USD, Currency.EUR, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0.92m);

        var command = new CreateTransactionCommand(
            IdUser: 1, Type: TransactionType.Expense, IdMainCategory: MainCategory.Luxuries,
            IdSubCategory: null, Description: "Cena", TransactionDate: new DateOnly(2026, 6, 17),
            Amount: 100m, Currency: Currency.USD);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.OriginalCurrency.Should().Be(Currency.USD);
        result.BaseAmount.Should().Be(92.00m);
        result.BaseCurrency.Should().Be(Currency.EUR);
        _txRepo.Verify(r => r.AddAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoCurrency_DefaultsToUserBaseCurrency_RateOne()
    {
        _userRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserWithBase(Currency.EUR));

        var command = new CreateTransactionCommand(
            IdUser: 1, Type: TransactionType.Income, IdMainCategory: MainCategory.Salary,
            IdSubCategory: null, Description: "Nómina", TransactionDate: new DateOnly(2026, 6, 1),
            Amount: 2000m, Currency: null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.OriginalCurrency.Should().Be(Currency.EUR);
        result.ExchangeRate.Should().Be(1m);
        result.BaseAmount.Should().Be(2000m);
        _rates.Verify(r => r.GetRateAsync(It.IsAny<Currency>(), It.IsAny<Currency>(),
            It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UnknownUser_ThrowsNotFound()
    {
        _userRepo.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var command = new CreateTransactionCommand(
            IdUser: 99, Type: TransactionType.Expense, IdMainCategory: MainCategory.Luxuries,
            IdSubCategory: null, Description: null, TransactionDate: new DateOnly(2026, 6, 17),
            Amount: 10m, Currency: Currency.EUR);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
```

- [x] **Step 3: Ejecutar el test y ver que falla**

Run: `dotnet test src/backend/tests/BigSchool.Application.Tests --filter "FullyQualifiedName~CreateTransactionCommandHandlerTests"`
Expected: FAIL de compilación.

- [x] **Step 4: Crear el command**

```csharp
// src/backend/src/BigSchool.Application/Commands/Transactions/Create/CreateTransactionCommand.cs
using BigSchool.Application.DTOs.Transactions;
using BigSchool.Domain.Enums;
using MediatR;

namespace BigSchool.Application.Commands.Transactions.Create;

public record CreateTransactionCommand(
    int IdUser,
    TransactionType Type,
    MainCategory IdMainCategory,
    int? IdSubCategory,
    string? Description,
    DateOnly TransactionDate,
    decimal Amount,
    Currency? Currency) : IRequest<TransactionDto>;
```

- [x] **Step 5: Crear el validator**

```csharp
// src/backend/src/BigSchool.Application/Commands/Transactions/Create/CreateTransactionCommandValidator.cs
using FluentValidation;

namespace BigSchool.Application.Commands.Transactions.Create;

public class CreateTransactionCommandValidator : AbstractValidator<CreateTransactionCommand>
{
    public CreateTransactionCommandValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("El importe debe ser mayor que cero.");

        RuleFor(x => x.IdMainCategory)
            .IsInEnum().WithMessage("La categoría no es válida.");

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("El tipo de transacción no es válido.");

        RuleFor(x => x.TransactionDate)
            .NotEmpty().WithMessage("La fecha es obligatoria.");

        RuleFor(x => x.Description)
            .MaximumLength(255);
    }
}
```

- [x] **Step 6: Crear el handler**

```csharp
// src/backend/src/BigSchool.Application/Commands/Transactions/Create/CreateTransactionCommandHandler.cs
using BigSchool.Application.DTOs.Transactions;
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Application.Interfaces.Services;
using BigSchool.Domain.Entities;
using BigSchool.Domain.Exceptions;
using BigSchool.Domain.ValueObjects;
using MediatR;

namespace BigSchool.Application.Commands.Transactions.Create;

public class CreateTransactionCommandHandler : IRequestHandler<CreateTransactionCommand, TransactionDto>
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IExchangeRateProvider _exchangeRateProvider;

    public CreateTransactionCommandHandler(
        ITransactionRepository transactionRepository,
        IUserRepository userRepository,
        IExchangeRateProvider exchangeRateProvider)
    {
        _transactionRepository = transactionRepository;
        _userRepository = userRepository;
        _exchangeRateProvider = exchangeRateProvider;
    }

    public async Task<TransactionDto> Handle(CreateTransactionCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.IdUser, cancellationToken)
            ?? throw new NotFoundException(nameof(User), request.IdUser);

        var currency = request.Currency ?? user.BaseCurrency;
        var rate = currency == user.BaseCurrency
            ? 1m
            : await _exchangeRateProvider.GetRateAsync(currency, user.BaseCurrency, request.TransactionDate, cancellationToken);

        var transaction = Transaction.Create(
            request.IdUser,
            request.Type,
            request.IdMainCategory,
            request.IdSubCategory,
            request.Description,
            Money.Create(request.Amount, currency),
            user.BaseCurrency,
            rate,
            request.TransactionDate,
            request.TransactionDate);

        await _transactionRepository.AddAsync(transaction, cancellationToken);
        await _transactionRepository.UnitOfWork.SaveChangesAsync();

        var c = transaction.Conversion;
        return new TransactionDto(
            transaction.IdTransaction, transaction.Type, transaction.IdMainCategory,
            transaction.IdSubCategory, transaction.Description, transaction.TransactionDate,
            c.Original.Amount, c.Original.Currency, c.Rate, c.Base.Amount, c.Base.Currency, c.RateDate);
    }
}
```

**Nota:** verificar la firma de `NotFoundException`. Si su constructor no es `(string entity, object key)`, ajustar la llamada a la firma real (ver `src/BigSchool.Domain/Exceptions/NotFoundException.cs`).

- [x] **Step 7: Ejecutar el test y ver que pasa**

Run: `dotnet test src/backend/tests/BigSchool.Application.Tests --filter "FullyQualifiedName~CreateTransactionCommandHandlerTests"`
Expected: PASS.

- [x] **Step 8: Commit**

```bash
git add -A && git commit -m "feat: añadir CreateTransaction (flujo de conversión multimoneda)"
```

---

### Task 5: `UpdateTransaction` + `DeleteTransaction`

**Files:**
- Create: `src/backend/src/BigSchool.Application/Commands/Transactions/Update/UpdateTransactionCommand.cs`
- Create: `src/backend/src/BigSchool.Application/Commands/Transactions/Update/UpdateTransactionCommandHandler.cs`
- Create: `src/backend/src/BigSchool.Application/Commands/Transactions/Update/UpdateTransactionCommandValidator.cs`
- Create: `src/backend/src/BigSchool.Application/Commands/Transactions/Delete/DeleteTransactionCommand.cs`
- Create: `src/backend/src/BigSchool.Application/Commands/Transactions/Delete/DeleteTransactionCommandHandler.cs`
- Test: `src/backend/tests/BigSchool.Application.Tests/Commands/Transactions/UpdateTransactionCommandHandlerTests.cs`
- Test: `src/backend/tests/BigSchool.Application.Tests/Commands/Transactions/DeleteTransactionCommandHandlerTests.cs`

**Nota de diseño:** Update re-resuelve el `rate` (igual que Create) y llama a `transaction.Update(...)`. Delete carga la transacción y llama a `transaction.Delete()` (soft-delete). Ambos validan pertenencia: `transaction.IdUser == command.IdUser`, si no → `NotFoundException` (no se filtra por usuario en el repo; se comprueba en el handler). Si la transacción no existe → `NotFoundException`.

**Convención de tests (repo):** **un fichero de test por clase bajo prueba**, con el nombre de la clase + `Tests` (p. ej. `UpdateTransactionCommandHandlerTests` para `UpdateTransactionCommandHandler`). No se agrupan dos handlers en un mismo fichero. Coherente con los tests existentes (`LoginCommandHandlerTests`, `RegisterCommandHandlerTests`, `RefreshTokenCommandHandlerTests`…) y con la Task 4 (`CreateTransactionCommandHandlerTests`).

- [x] **Step 1: Escribir los tests que fallan (un fichero por handler)**

```csharp
// src/backend/tests/BigSchool.Application.Tests/Commands/Transactions/UpdateTransactionCommandHandlerTests.cs
using BigSchool.Application.Commands.Transactions.Update;
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

namespace BigSchool.Application.Tests.Commands.Transactions;

public class UpdateTransactionCommandHandlerTests
{
    private readonly Mock<ITransactionRepository> _txRepo = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IExchangeRateProvider> _rates = new();
    private readonly UpdateTransactionCommandHandler _handler;

    public UpdateTransactionCommandHandlerTests()
    {
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<bool>())).ReturnsAsync(1);
        _txRepo.Setup(r => r.UnitOfWork).Returns(uow.Object);
        _handler = new UpdateTransactionCommandHandler(_txRepo.Object, _userRepo.Object, _rates.Object);
    }

    private static Transaction ExistingTx(int userId = 1)
        => Transaction.Create(userId, TransactionType.Expense, MainCategory.Luxuries, null, "old",
            Money.Create(100m, Currency.EUR), Currency.EUR, 1m, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 1));

    [Fact]
    public async Task Update_ChangesAmountAndRebuildsConversion()
    {
        var tx = ExistingTx();
        _txRepo.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(tx);
        _userRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(User.Create("u@test.com", "h", "s", "User", Currency.EUR));

        var command = new UpdateTransactionCommand(
            IdTransaction: 5, IdUser: 1, Type: TransactionType.Expense,
            IdMainCategory: MainCategory.EssentialExpenses, IdSubCategory: null, Description: "new",
            TransactionDate: new DateOnly(2026, 6, 2), Amount: 60m, Currency: Currency.EUR);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.BaseAmount.Should().Be(60m);
        result.IdMainCategory.Should().Be(MainCategory.EssentialExpenses);
    }

    [Fact]
    public async Task Update_OtherUsersTransaction_ThrowsNotFound()
    {
        _txRepo.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(ExistingTx(userId: 2));

        var command = new UpdateTransactionCommand(5, 1, TransactionType.Expense, MainCategory.Luxuries,
            null, null, new DateOnly(2026, 6, 2), 60m, Currency.EUR);

        await _handler.Invoking(h => h.Handle(command, CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();
    }
}
```

```csharp
// src/backend/tests/BigSchool.Application.Tests/Commands/Transactions/DeleteTransactionCommandHandlerTests.cs
using BigSchool.Application.Commands.Transactions.Delete;
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Domain.Entities;
using BigSchool.Domain.Enums;
using BigSchool.Domain.Interfaces;
using BigSchool.Domain.ValueObjects;
using FluentAssertions;
using Moq;
using Xunit;

namespace BigSchool.Application.Tests.Commands.Transactions;

public class DeleteTransactionCommandHandlerTests
{
    private readonly Mock<ITransactionRepository> _txRepo = new();
    private readonly DeleteTransactionCommandHandler _handler;

    public DeleteTransactionCommandHandlerTests()
    {
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<bool>())).ReturnsAsync(1);
        _txRepo.Setup(r => r.UnitOfWork).Returns(uow.Object);
        _handler = new DeleteTransactionCommandHandler(_txRepo.Object);
    }

    private static Transaction ExistingTx(int userId = 1)
        => Transaction.Create(userId, TransactionType.Expense, MainCategory.Luxuries, null, "old",
            Money.Create(100m, Currency.EUR), Currency.EUR, 1m, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 1));

    [Fact]
    public async Task Delete_SoftDeletesTransaction()
    {
        var tx = ExistingTx();
        _txRepo.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(tx);

        await _handler.Handle(new DeleteTransactionCommand(IdTransaction: 5, IdUser: 1), CancellationToken.None);

        tx.IdStatus.Should().Be(EntityStatus.Deleted);
    }
}
```

- [x] **Step 2: Ejecutar los tests y ver que fallan**

Run: `dotnet test src/backend/tests/BigSchool.Application.Tests --filter "FullyQualifiedName~UpdateTransactionCommandHandlerTests|FullyQualifiedName~DeleteTransactionCommandHandlerTests"`
Expected: FAIL de compilación.

- [x] **Step 3: Crear `UpdateTransactionCommand` + validator**

```csharp
// src/backend/src/BigSchool.Application/Commands/Transactions/Update/UpdateTransactionCommand.cs
using BigSchool.Application.DTOs.Transactions;
using BigSchool.Domain.Enums;
using MediatR;

namespace BigSchool.Application.Commands.Transactions.Update;

public record UpdateTransactionCommand(
    int IdTransaction,
    int IdUser,
    TransactionType Type,
    MainCategory IdMainCategory,
    int? IdSubCategory,
    string? Description,
    DateOnly TransactionDate,
    decimal Amount,
    Currency? Currency) : IRequest<TransactionDto>;
```

```csharp
// src/backend/src/BigSchool.Application/Commands/Transactions/Update/UpdateTransactionCommandValidator.cs
using FluentValidation;

namespace BigSchool.Application.Commands.Transactions.Update;

public class UpdateTransactionCommandValidator : AbstractValidator<UpdateTransactionCommand>
{
    public UpdateTransactionCommandValidator()
    {
        RuleFor(x => x.IdTransaction).GreaterThan(0);
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("El importe debe ser mayor que cero.");
        RuleFor(x => x.IdMainCategory).IsInEnum().WithMessage("La categoría no es válida.");
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.TransactionDate).NotEmpty();
        RuleFor(x => x.Description).MaximumLength(255);
    }
}
```

- [x] **Step 4: Crear `UpdateTransactionCommandHandler`**

```csharp
// src/backend/src/BigSchool.Application/Commands/Transactions/Update/UpdateTransactionCommandHandler.cs
using BigSchool.Application.DTOs.Transactions;
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Application.Interfaces.Services;
using BigSchool.Domain.Entities;
using BigSchool.Domain.Exceptions;
using BigSchool.Domain.ValueObjects;
using MediatR;

namespace BigSchool.Application.Commands.Transactions.Update;

public class UpdateTransactionCommandHandler : IRequestHandler<UpdateTransactionCommand, TransactionDto>
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IExchangeRateProvider _exchangeRateProvider;

    public UpdateTransactionCommandHandler(
        ITransactionRepository transactionRepository,
        IUserRepository userRepository,
        IExchangeRateProvider exchangeRateProvider)
    {
        _transactionRepository = transactionRepository;
        _userRepository = userRepository;
        _exchangeRateProvider = exchangeRateProvider;
    }

    public async Task<TransactionDto> Handle(UpdateTransactionCommand request, CancellationToken cancellationToken)
    {
        var transaction = await _transactionRepository.GetByIdAsync(request.IdTransaction, cancellationToken);
        if (transaction is null || transaction.IdUser != request.IdUser)
            throw new NotFoundException(nameof(Transaction), request.IdTransaction);

        var user = await _userRepository.GetByIdAsync(request.IdUser, cancellationToken)
            ?? throw new NotFoundException(nameof(User), request.IdUser);

        var currency = request.Currency ?? user.BaseCurrency;
        var rate = currency == user.BaseCurrency
            ? 1m
            : await _exchangeRateProvider.GetRateAsync(currency, user.BaseCurrency, request.TransactionDate, cancellationToken);

        transaction.Update(
            request.Type, request.IdMainCategory, request.IdSubCategory, request.Description,
            Money.Create(request.Amount, currency), user.BaseCurrency, rate,
            request.TransactionDate, request.TransactionDate);

        await _transactionRepository.UnitOfWork.SaveChangesAsync();

        var c = transaction.Conversion;
        return new TransactionDto(
            transaction.IdTransaction, transaction.Type, transaction.IdMainCategory,
            transaction.IdSubCategory, transaction.Description, transaction.TransactionDate,
            c.Original.Amount, c.Original.Currency, c.Rate, c.Base.Amount, c.Base.Currency, c.RateDate);
    }
}
```

- [x] **Step 5: Crear `DeleteTransactionCommand` + handler**

```csharp
// src/backend/src/BigSchool.Application/Commands/Transactions/Delete/DeleteTransactionCommand.cs
using MediatR;

namespace BigSchool.Application.Commands.Transactions.Delete;

public record DeleteTransactionCommand(int IdTransaction, int IdUser) : IRequest;
```

```csharp
// src/backend/src/BigSchool.Application/Commands/Transactions/Delete/DeleteTransactionCommandHandler.cs
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Domain.Entities;
using BigSchool.Domain.Exceptions;
using MediatR;

namespace BigSchool.Application.Commands.Transactions.Delete;

public class DeleteTransactionCommandHandler : IRequestHandler<DeleteTransactionCommand>
{
    private readonly ITransactionRepository _transactionRepository;

    public DeleteTransactionCommandHandler(ITransactionRepository transactionRepository)
    {
        _transactionRepository = transactionRepository;
    }

    public async Task Handle(DeleteTransactionCommand request, CancellationToken cancellationToken)
    {
        var transaction = await _transactionRepository.GetByIdAsync(request.IdTransaction, cancellationToken);
        if (transaction is null || transaction.IdUser != request.IdUser)
            throw new NotFoundException(nameof(Transaction), request.IdTransaction);

        transaction.Delete();
        await _transactionRepository.UnitOfWork.SaveChangesAsync();
    }
}
```

- [x] **Step 6: Ejecutar los tests y ver que pasan**

Run: `dotnet test src/backend/tests/BigSchool.Application.Tests --filter "FullyQualifiedName~UpdateTransactionCommandHandlerTests|FullyQualifiedName~DeleteTransactionCommandHandlerTests"`
Expected: PASS.

- [x] **Step 7: Commit**

```bash
git add -A && git commit -m "feat: añadir UpdateTransaction y DeleteTransaction (soft-delete)"
```

---

### Task 6: Queries Dapper (consolidadas en moneda base)

**Files:**
- Create: `src/backend/src/BigSchool.Application/Queries/Transactions/GetTransactionById/GetTransactionByIdQuery.cs` (+ Handler)
- Create: `src/backend/src/BigSchool.Application/Queries/Transactions/GetTransactions/GetTransactionsQuery.cs` (+ Handler + `TransactionListItemDto`)
- Create: `src/backend/src/BigSchool.Application/Queries/Transactions/GetTransactionSummary/GetTransactionSummaryQuery.cs` (+ Handler + `TransactionSummaryDto`)
- Create: `src/backend/src/BigSchool.Application/Queries/Transactions/GetMonthlyChart/GetMonthlyChartQuery.cs` (+ Handler + `MonthlyChartPointDto`)
- Create: `src/backend/src/BigSchool.Application/Common/PagedResult.cs`

**Nota de diseño:** Las queries usan Dapper vía `IDbConnectionFactory`, **filtran por `IdUser` y por estado no borrado** (`IdStatus <> @StatusDeleted`, parametrizado desde `EntityStatus.Deleted` — sin número mágico; el Global Query Filter de EF no aplica a Dapper) y **agregan sobre `BaseAmount`**. `GetTransactions` pagina (`MetaData`). El mapeo de columnas `Type`/`IdStatus` es a `short`; las fechas a `DateOnly` (Dapper 2.1 soporta `DateOnly` con MySqlConnector; si diera problema, mapear a `DateTime` y convertir).

**Convención SQL/Dapper (ver Decisión 7 y `AGENTS.md`):** cada handler declara su SQL como `private const string ..._QUERY` (UPPERCASE) a nivel de clase y pasa los parámetros con `DynamicParameters`. Los bloques siguientes ya la aplican.

- [x] **Step 1: Crear `PagedResult<T>`**

```csharp
// src/backend/src/BigSchool.Application/Common/PagedResult.cs
namespace BigSchool.Application.Common;

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
```

- [x] **Step 2: Escribir un test de query que falle (GetTransactionSummary, lógica de DTO/SQL params)**

Las queries Dapper se validan principalmente en integración (Task 9). Aquí cubrimos que el handler construye el DTO y los parámetros correctos usando un `IDbConnectionFactory` real en memoria es inviable con MySQL; por tanto **este Step se cubre en integración**. Crear solo un test de contrato del `GetTransactionsQuery` (que la paginación normaliza valores):

```csharp
// src/backend/tests/BigSchool.Application.Tests/Queries/Transactions/GetTransactionsQueryTests.cs
using BigSchool.Application.Queries.Transactions.GetTransactions;
using FluentAssertions;
using Xunit;

namespace BigSchool.Application.Tests.Queries.Transactions;

public class GetTransactionsQueryTests
{
    [Theory]
    [InlineData(0, 1)]   // page mínimo 1
    [InlineData(-5, 1)]
    [InlineData(3, 3)]
    public void NormalizePage_ClampsToMinimumOne(int input, int expected)
    {
        GetTransactionsQuery.NormalizePage(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(0, 20)]   // default
    [InlineData(500, 100)] // máximo 100
    [InlineData(25, 25)]
    public void NormalizePageSize_ClampsBetween1And100(int input, int expected)
    {
        GetTransactionsQuery.NormalizePageSize(input).Should().Be(expected);
    }
}
```

- [x] **Step 3: Ejecutar y ver que falla**

Run: `dotnet test src/backend/tests/BigSchool.Application.Tests --filter "FullyQualifiedName~GetTransactionsQueryTests"`
Expected: FAIL de compilación.

- [x] **Step 4: Crear `GetTransactionById`**

```csharp
// src/backend/src/BigSchool.Application/Queries/Transactions/GetTransactionById/GetTransactionByIdQuery.cs
using BigSchool.Application.DTOs.Transactions;
using MediatR;

namespace BigSchool.Application.Queries.Transactions.GetTransactionById;

public record GetTransactionByIdQuery(int IdTransaction, int IdUser) : IRequest<TransactionDto?>;
```

```csharp
// src/backend/src/BigSchool.Application/Queries/Transactions/GetTransactionById/GetTransactionByIdQueryHandler.cs
using BigSchool.Application.DTOs.Transactions;
using BigSchool.Application.Interfaces;
using BigSchool.Domain.Enums;
using Dapper;
using MediatR;

namespace BigSchool.Application.Queries.Transactions.GetTransactionById;

public class GetTransactionByIdQueryHandler : IRequestHandler<GetTransactionByIdQuery, TransactionDto?>
{
    private readonly IDbConnectionFactory _dbFactory;

    public GetTransactionByIdQueryHandler(IDbConnectionFactory dbFactory) => _dbFactory = dbFactory;

    private const string GETTRANSACTIONBYID_QUERY = @"SELECT IdTransaction, Type, IdMainCategory, IdSubCategory, Description, TransactionDate,
                                                             OriginalAmount, OriginalCurrency, ExchangeRate, BaseAmount, BaseCurrency, RateDate
                                                      FROM Transactions
                                                      WHERE IdTransaction = @IdTransaction AND IdUser = @IdUser AND IdStatus <> @StatusDeleted
                                                      LIMIT 1;";

    public async Task<TransactionDto?> Handle(GetTransactionByIdQuery request, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@IdTransaction", request.IdTransaction);
        parameters.Add("@IdUser", request.IdUser);
        parameters.Add("@StatusDeleted", EntityStatus.Deleted);

        using var conn = _dbFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<TransactionDto>(GETTRANSACTIONBYID_QUERY, parameters);
    }
}
```

**Nota Dapper/enums:** `TransactionDto` usa enums `TransactionType`/`MainCategory`/`Currency`. `Type`/`IdMainCategory` se almacenan numéricos → Dapper los mapea al enum directamente. `OriginalCurrency`/`BaseCurrency` son `CHAR(3)` (alpha-3) → Dapper NO los convierte automáticamente a `Currency`. Solución: en el `SELECT` no devolver el `CHAR(3)` a la propiedad enum; en su lugar seleccionar `OriginalCurrency AS OriginalCurrencyCode` y mapear. **Para evitar fricción**, los DTOs de query usan `string` para las monedas. Crear los DTOs de query con monedas `string` (ver Steps siguientes) y, si se quiere el enum, parsearlo en el handler. Ajustar `GetTransactionById` para devolver un DTO de query con `string` monedas en vez de `TransactionDto`:

Reemplazar el tipo de retorno por `TransactionListItemDto` (definido en el Step 5) y el `SELECT` deja las monedas como `string`. (El `TransactionDto` con enums se mantiene solo para las respuestas de los commands, donde se construye desde el dominio.)

- [x] **Step 5: Crear `GetTransactions` (paginación + filtros) y `TransactionListItemDto`**

```csharp
// src/backend/src/BigSchool.Application/DTOs/Transactions/TransactionListItemDto.cs
namespace BigSchool.Application.DTOs.Transactions;

public record TransactionListItemDto(
    int IdTransaction,
    short Type,
    int IdMainCategory,
    int? IdSubCategory,
    string? Description,
    DateOnly TransactionDate,
    decimal OriginalAmount,
    string OriginalCurrency,
    decimal ExchangeRate,
    decimal BaseAmount,
    string BaseCurrency,
    DateOnly RateDate);
```

```csharp
// src/backend/src/BigSchool.Application/Queries/Transactions/GetTransactions/GetTransactionsQuery.cs
using BigSchool.Application.Common;
using BigSchool.Application.DTOs.Transactions;
using BigSchool.Domain.Enums;
using MediatR;

namespace BigSchool.Application.Queries.Transactions.GetTransactions;

public record GetTransactionsQuery(
    int IdUser,
    TransactionType? Type,
    MainCategory? IdMainCategory,
    DateOnly? From,
    DateOnly? To,
    int Page,
    int PageSize) : IRequest<PagedResult<TransactionListItemDto>>
{
    public static int NormalizePage(int page) => page < 1 ? 1 : page;
    public static int NormalizePageSize(int pageSize) => pageSize switch
    {
        <= 0 => 20,
        > 100 => 100,
        _ => pageSize
    };
}
```

```csharp
// src/backend/src/BigSchool.Application/Queries/Transactions/GetTransactions/GetTransactionsQueryHandler.cs
using BigSchool.Application.Common;
using BigSchool.Application.DTOs.Transactions;
using BigSchool.Application.Interfaces;
using BigSchool.Domain.Enums;
using Dapper;
using MediatR;

namespace BigSchool.Application.Queries.Transactions.GetTransactions;

public class GetTransactionsQueryHandler
    : IRequestHandler<GetTransactionsQuery, PagedResult<TransactionListItemDto>>
{
    private readonly IDbConnectionFactory _dbFactory;

    public GetTransactionsQueryHandler(IDbConnectionFactory dbFactory) => _dbFactory = dbFactory;

    // Fragmento WHERE reutilizado por el COUNT y el SELECT paginado (const concatenable en compilación).
    private const string TRANSACTIONS_WHERE = @"WHERE IdUser = @IdUser AND IdStatus <> @StatusDeleted
                                                  AND (@Type IS NULL OR Type = @Type)
                                                  AND (@IdMainCategory IS NULL OR IdMainCategory = @IdMainCategory)
                                                  AND (@From IS NULL OR TransactionDate >= @From)
                                                  AND (@To IS NULL OR TransactionDate <= @To)";

    private const string GETTRANSACTIONS_QUERY = @"SELECT COUNT(*) FROM Transactions " + TRANSACTIONS_WHERE + @";
            SELECT IdTransaction, Type, IdMainCategory, IdSubCategory, Description, TransactionDate,
                   OriginalAmount, OriginalCurrency, ExchangeRate, BaseAmount, BaseCurrency, RateDate
            FROM Transactions " + TRANSACTIONS_WHERE + @"
            ORDER BY TransactionDate DESC, IdTransaction DESC
            LIMIT @PageSize OFFSET @Offset;";

    public async Task<PagedResult<TransactionListItemDto>> Handle(
        GetTransactionsQuery request, CancellationToken cancellationToken)
    {
        var page = GetTransactionsQuery.NormalizePage(request.Page);
        var pageSize = GetTransactionsQuery.NormalizePageSize(request.PageSize);

        var parameters = new DynamicParameters();
        parameters.Add("@IdUser", request.IdUser);
        parameters.Add("@StatusDeleted", EntityStatus.Deleted);
        parameters.Add("@Type", request.Type.HasValue ? (short?)request.Type.Value : null);
        parameters.Add("@IdMainCategory", request.IdMainCategory.HasValue ? (int?)request.IdMainCategory.Value : null);
        parameters.Add("@From", request.From);
        parameters.Add("@To", request.To);
        parameters.Add("@PageSize", pageSize);
        parameters.Add("@Offset", (page - 1) * pageSize);

        using var conn = _dbFactory.CreateConnection();
        using var multi = await conn.QueryMultipleAsync(GETTRANSACTIONS_QUERY, parameters);
        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<TransactionListItemDto>()).ToList();

        return new PagedResult<TransactionListItemDto>(items, page, pageSize, total);
    }
}
```

- [x] **Step 6: Refactor de `GetTransactionById` para devolver `TransactionListItemDto`**

Actualizar `GetTransactionByIdQuery` a `IRequest<TransactionListItemDto?>` y el handler a `QuerySingleOrDefaultAsync<TransactionListItemDto>` (mismo `SELECT` del Step 4, monedas como `string`).

- [x] **Step 7: Crear `GetTransactionSummary` (consolidado en base)**

```csharp
// src/backend/src/BigSchool.Application/Queries/Transactions/GetTransactionSummary/TransactionSummaryDto.cs
namespace BigSchool.Application.Queries.Transactions.GetTransactionSummary;

public record TransactionSummaryDto(decimal TotalIncome, decimal TotalExpense, decimal Balance, string BaseCurrency);
```

```csharp
// src/backend/src/BigSchool.Application/Queries/Transactions/GetTransactionSummary/GetTransactionSummaryQuery.cs
using MediatR;

namespace BigSchool.Application.Queries.Transactions.GetTransactionSummary;

public record GetTransactionSummaryQuery(int IdUser, DateOnly? From, DateOnly? To) : IRequest<TransactionSummaryDto>;
```

```csharp
// src/backend/src/BigSchool.Application/Queries/Transactions/GetTransactionSummary/GetTransactionSummaryQueryHandler.cs
using BigSchool.Application.Interfaces;
using BigSchool.Domain.Enums;
using Dapper;
using MediatR;

namespace BigSchool.Application.Queries.Transactions.GetTransactionSummary;

public class GetTransactionSummaryQueryHandler : IRequestHandler<GetTransactionSummaryQuery, TransactionSummaryDto>
{
    private readonly IDbConnectionFactory _dbFactory;

    public GetTransactionSummaryQueryHandler(IDbConnectionFactory dbFactory) => _dbFactory = dbFactory;

    // Consolidación SIEMPRE sobre BaseAmount (moneda base del usuario).
    private const string GETTRANSACTIONSUMMARY_QUERY = @"SELECT
                COALESCE(SUM(CASE WHEN Type = @Income  THEN BaseAmount ELSE 0 END), 0) AS TotalIncome,
                COALESCE(SUM(CASE WHEN Type = @Expense THEN BaseAmount ELSE 0 END), 0) AS TotalExpense,
                COALESCE(MAX(BaseCurrency), '') AS BaseCurrency
            FROM Transactions
            WHERE IdUser = @IdUser AND IdStatus <> @StatusDeleted
              AND (@From IS NULL OR TransactionDate >= @From)
              AND (@To IS NULL OR TransactionDate <= @To);";

    public async Task<TransactionSummaryDto> Handle(GetTransactionSummaryQuery request, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@IdUser", request.IdUser);
        parameters.Add("@StatusDeleted", EntityStatus.Deleted);
        parameters.Add("@Income", (short)TransactionType.Income);
        parameters.Add("@Expense", (short)TransactionType.Expense);
        parameters.Add("@From", request.From);
        parameters.Add("@To", request.To);

        using var conn = _dbFactory.CreateConnection();
        var row = await conn.QuerySingleAsync<(decimal TotalIncome, decimal TotalExpense, string BaseCurrency)>(
            GETTRANSACTIONSUMMARY_QUERY, parameters);

        return new TransactionSummaryDto(
            row.TotalIncome, row.TotalExpense, row.TotalIncome - row.TotalExpense, row.BaseCurrency);
    }
}
```

- [x] **Step 8: Crear `GetMonthlyChart`**

```csharp
// src/backend/src/BigSchool.Application/Queries/Transactions/GetMonthlyChart/MonthlyChartPointDto.cs
namespace BigSchool.Application.Queries.Transactions.GetMonthlyChart;

public record MonthlyChartPointDto(int Year, int Month, decimal Income, decimal Expense);
```

```csharp
// src/backend/src/BigSchool.Application/Queries/Transactions/GetMonthlyChart/GetMonthlyChartQuery.cs
using MediatR;

namespace BigSchool.Application.Queries.Transactions.GetMonthlyChart;

public record GetMonthlyChartQuery(int IdUser, int Year) : IRequest<IReadOnlyList<MonthlyChartPointDto>>;
```

```csharp
// src/backend/src/BigSchool.Application/Queries/Transactions/GetMonthlyChart/GetMonthlyChartQueryHandler.cs
using BigSchool.Application.Interfaces;
using BigSchool.Domain.Enums;
using Dapper;
using MediatR;

namespace BigSchool.Application.Queries.Transactions.GetMonthlyChart;

public class GetMonthlyChartQueryHandler
    : IRequestHandler<GetMonthlyChartQuery, IReadOnlyList<MonthlyChartPointDto>>
{
    private readonly IDbConnectionFactory _dbFactory;

    public GetMonthlyChartQueryHandler(IDbConnectionFactory dbFactory) => _dbFactory = dbFactory;

    private const string GETMONTHLYCHART_QUERY = @"SELECT
                YEAR(TransactionDate)  AS Year,
                MONTH(TransactionDate) AS Month,
                COALESCE(SUM(CASE WHEN Type = @Income  THEN BaseAmount ELSE 0 END), 0) AS Income,
                COALESCE(SUM(CASE WHEN Type = @Expense THEN BaseAmount ELSE 0 END), 0) AS Expense
            FROM Transactions
            WHERE IdUser = @IdUser AND IdStatus <> @StatusDeleted AND YEAR(TransactionDate) = @Year
            GROUP BY YEAR(TransactionDate), MONTH(TransactionDate)
            ORDER BY Month;";

    public async Task<IReadOnlyList<MonthlyChartPointDto>> Handle(
        GetMonthlyChartQuery request, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@IdUser", request.IdUser);
        parameters.Add("@StatusDeleted", EntityStatus.Deleted);
        parameters.Add("@Year", request.Year);
        parameters.Add("@Income", (short)TransactionType.Income);
        parameters.Add("@Expense", (short)TransactionType.Expense);

        using var conn = _dbFactory.CreateConnection();
        var rows = await conn.QueryAsync<MonthlyChartPointDto>(GETMONTHLYCHART_QUERY, parameters);
        return rows.ToList();
    }
}
```

- [x] **Step 9: Ejecutar los tests de query y build**

Run: `dotnet test src/backend/tests/BigSchool.Application.Tests --filter "FullyQualifiedName~GetTransactionsQueryTests"`
Expected: PASS.
Run: `dotnet build src/backend/Backend.slnx`
Expected: 0 errors.

- [x] **Step 10: Commit**

```bash
git add -A && git commit -m "feat: añadir queries Dapper de transacciones consolidadas en moneda base"
```

---

### Task 7: `GetCategories` query

**Files:**
- Create: `src/backend/src/BigSchool.Application/Queries/Categories/GetCategories/GetCategoriesQuery.cs` (+ Handler + DTOs)

**Nota de diseño:** Devuelve las `MainCategory` (enum, estáticas) con sus `SubCategories` del usuario (las `IsDefault` con `IdUser IS NULL` + las propias del usuario), leídas por Dapper. Estructura: lista de `CategoryDto { Id, Name, SubCategories[] }`. SQL como `const` UPPERCASE `_QUERY` + `DynamicParameters` (Decisión 7 / `AGENTS.md`).

- [x] **Step 1: Crear DTOs + query + handler**

```csharp
// src/backend/src/BigSchool.Application/Queries/Categories/GetCategories/CategoryDto.cs
namespace BigSchool.Application.Queries.Categories.GetCategories;

public record SubCategoryDto(int IdSubCategory, string Name, bool IsDefault);
public record CategoryDto(int IdMainCategory, string Name, IReadOnlyList<SubCategoryDto> SubCategories);
```

```csharp
// src/backend/src/BigSchool.Application/Queries/Categories/GetCategories/GetCategoriesQuery.cs
using MediatR;

namespace BigSchool.Application.Queries.Categories.GetCategories;

public record GetCategoriesQuery(int IdUser) : IRequest<IReadOnlyList<CategoryDto>>;
```

```csharp
// src/backend/src/BigSchool.Application/Queries/Categories/GetCategories/GetCategoriesQueryHandler.cs
using BigSchool.Application.Interfaces;
using BigSchool.Domain.Enums;
using Dapper;
using MediatR;

namespace BigSchool.Application.Queries.Categories.GetCategories;

public class GetCategoriesQueryHandler : IRequestHandler<GetCategoriesQuery, IReadOnlyList<CategoryDto>>
{
    private readonly IDbConnectionFactory _dbFactory;

    public GetCategoriesQueryHandler(IDbConnectionFactory dbFactory) => _dbFactory = dbFactory;

    private const string GETCATEGORIES_QUERY = @"SELECT IdSubCategory, IdMainCategory, Name, IsDefault
                                                 FROM SubCategories
                                                 WHERE IdStatus <> @StatusDeleted AND (IdUser IS NULL OR IdUser = @IdUser)
                                                 ORDER BY IdMainCategory, Name;";

    public async Task<IReadOnlyList<CategoryDto>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@IdUser", request.IdUser);
        parameters.Add("@StatusDeleted", EntityStatus.Deleted);

        using var conn = _dbFactory.CreateConnection();
        var rows = (await conn.QueryAsync<(int IdSubCategory, int IdMainCategory, string Name, bool IsDefault)>(
            GETCATEGORIES_QUERY, parameters)).ToList();

        return Enum.GetValues<MainCategory>()
            .Select(mc => new CategoryDto(
                (int)mc,
                mc.ToString(),
                rows.Where(r => r.IdMainCategory == (int)mc)
                    .Select(r => new SubCategoryDto(r.IdSubCategory, r.Name, r.IsDefault))
                    .ToList()))
            .ToList();
    }
}
```

- [x] **Step 2: Build**

Run: `dotnet build src/backend/Backend.slnx`
Expected: 0 errors.

- [x] **Step 3: Commit**

```bash
git add -A && git commit -m "feat: añadir query GetCategories (MainCategory + subcategorías del usuario)"
```

---

### Task 8: `TransactionsController` + `CategoriesController`

**Files:**
- Create: `src/backend/src/BigSchool.WebApi/Controllers/TransactionsController.cs`
- Create: `src/backend/src/BigSchool.WebApi/Controllers/CategoriesController.cs`
- Create: `src/backend/src/BigSchool.WebApi/Common/CurrentUser.cs` (helper para extraer el userId del JWT)

**Nota de diseño:** El `IdUser` se obtiene del JWT (claim `sub` cifrado con DPAPI vía `IUserIdEncryptor.Decrypt`), NO del body. Crear un helper `CurrentUser` que descifre el claim `sub`. Los endpoints van con `[Authorize]`. El body de creación NO incluye `IdUser`; se compone el command con el userId del token. Respuestas con `ApiResponse`/`ApiResponse<T>`. Verificar el método real de `IUserIdEncryptor` (`Decrypt`/`Encrypt`) en `src/BigSchool.Application/Interfaces/Services/IUserIdEncryptor.cs`.

- [x] **Step 1: Crear el helper `CurrentUser`**

```csharp
// src/backend/src/BigSchool.WebApi/Common/CurrentUser.cs
using System.Security.Claims;
using BigSchool.Application.Interfaces.Services;
using Microsoft.IdentityModel.JsonWebTokens;

namespace BigSchool.WebApi.Common;

public static class CurrentUser
{
    /// <summary>Extrae el IdUser del claim 'sub' (cifrado con DPAPI) del usuario autenticado.</summary>
    public static int GetId(ClaimsPrincipal principal, IUserIdEncryptor encryptor)
    {
        var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("Token sin identificador de usuario.");
        return encryptor.Decrypt(sub)
            ?? throw new UnauthorizedAccessException("Identificador de usuario inválido en el token.");
    }
}
```

**Nota:** `IUserIdEncryptor.Decrypt(string)` devuelve `int?` (null si el valor cifrado es inválido); por eso el helper lanza `UnauthorizedAccessException` cuando es null (el `ExceptionHandlingMiddleware` lo mapea a 401).

- [x] **Step 2: Crear `TransactionsController`**

```csharp
// src/backend/src/BigSchool.WebApi/Controllers/TransactionsController.cs
using BigSchool.Application.Commands.Transactions.Create;
using BigSchool.Application.Commands.Transactions.Delete;
using BigSchool.Application.Commands.Transactions.Update;
using BigSchool.Application.Common;
using BigSchool.Application.DTOs.Transactions;
using BigSchool.Application.Interfaces.Services;
using BigSchool.Application.Queries.Transactions.GetMonthlyChart;
using BigSchool.Application.Queries.Transactions.GetTransactionById;
using BigSchool.Application.Queries.Transactions.GetTransactions;
using BigSchool.Application.Queries.Transactions.GetTransactionSummary;
using BigSchool.Domain.Enums;
using BigSchool.WebApi.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BigSchool.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/transactions")]
public class TransactionsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserIdEncryptor _encryptor;

    public TransactionsController(IMediator mediator, IUserIdEncryptor encryptor)
    {
        _mediator = mediator;
        _encryptor = encryptor;
    }

    private int UserId => CurrentUser.GetId(User, _encryptor);

    public record CreateTransactionRequest(
        TransactionType Type, MainCategory IdMainCategory, int? IdSubCategory,
        string? Description, DateOnly TransactionDate, decimal Amount, Currency? Currency);

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<TransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateTransactionRequest body)
    {
        var command = new CreateTransactionCommand(UserId, body.Type, body.IdMainCategory,
            body.IdSubCategory, body.Description, body.TransactionDate, body.Amount, body.Currency);
        var result = await _mediator.Send(command);
        return Ok(ApiResponse<TransactionDto>.Success(result));
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<TransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] CreateTransactionRequest body)
    {
        var command = new UpdateTransactionCommand(id, UserId, body.Type, body.IdMainCategory,
            body.IdSubCategory, body.Description, body.TransactionDate, body.Amount, body.Currency);
        var result = await _mediator.Send(command);
        return Ok(ApiResponse<TransactionDto>.Success(result));
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        await _mediator.Send(new DeleteTransactionCommand(id, UserId));
        return Ok(ApiResponse.Success());
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<TransactionListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _mediator.Send(new GetTransactionByIdQuery(id, UserId));
        return result is null ? NotFound(ApiResponse.Fail()) : Ok(ApiResponse<TransactionListItemDto>.Success(result));
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TransactionListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(
        [FromQuery] TransactionType? type, [FromQuery] MainCategory? category,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetTransactionsQuery(UserId, type, category, from, to, page, pageSize));
        var meta = new MetaData { Page = result.Page, PageSize = result.PageSize, TotalCount = result.TotalCount };
        return Ok(ApiResponse<IReadOnlyList<TransactionListItemDto>>.Success(result.Items, meta));
    }

    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponse<TransactionSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Summary([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var result = await _mediator.Send(new GetTransactionSummaryQuery(UserId, from, to));
        return Ok(ApiResponse<TransactionSummaryDto>.Success(result));
    }

    [HttpGet("monthly-chart")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<MonthlyChartPointDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MonthlyChart([FromQuery] int year)
    {
        var result = await _mediator.Send(new GetMonthlyChartQuery(UserId, year));
        return Ok(ApiResponse<IReadOnlyList<MonthlyChartPointDto>>.Success(result));
    }
}
```

- [x] **Step 3: Crear `CategoriesController`**

```csharp
// src/backend/src/BigSchool.WebApi/Controllers/CategoriesController.cs
using BigSchool.Application.Common;
using BigSchool.Application.Interfaces.Services;
using BigSchool.Application.Queries.Categories.GetCategories;
using BigSchool.WebApi.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BigSchool.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/categories")]
public class CategoriesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserIdEncryptor _encryptor;

    public CategoriesController(IMediator mediator, IUserIdEncryptor encryptor)
    {
        _mediator = mediator;
        _encryptor = encryptor;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CategoryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get()
    {
        var userId = CurrentUser.GetId(User, _encryptor);
        var result = await _mediator.Send(new GetCategoriesQuery(userId));
        return Ok(ApiResponse<IReadOnlyList<CategoryDto>>.Success(result));
    }
}
```

- [x] **Step 4: Build + arranque rápido**

Run: `dotnet build src/backend/Backend.slnx`
Expected: 0 errors.
Verificar que Swagger lista los nuevos endpoints (`dotnet run --project src/backend/src/BigSchool.WebApi` y abrir `/swagger`), o dejarlo para la verificación E2E de Task 9.

- [x] **Step 5: Commit**

```bash
git add -A && git commit -m "feat: añadir endpoints de transacciones y categorías"
```

---

### Task 9: Tests E2E de endpoints (`WebApplicationFactory` + verificación física)

**Files:**
- Modify: `src/backend/tests/BigSchool.Integration.Tests/Fixtures/MySqlDatabaseFixture.cs` (ampliar `ResetAsync` con `Transactions`/`Users`)
- Create: `src/backend/tests/BigSchool.Integration.Tests/Fixtures/BigSchoolWebAppFactory.cs`
- Create: `src/backend/tests/BigSchool.Integration.Tests/Transactions/TransactionsEndpointTests.cs`

**Nota de diseño:** E2E **desde el endpoint hacia abajo por todo el stack real** (auth JWT → controller → MediatR/validación → EF/Dapper → MySQL), vía `WebApplicationFactory<Program>` apuntando a `bigschool_test` (sobrescribe `ConnectionStrings:DefaultConnection`, la única clave de conexión que usan EF *y* Dapper). Por ser caros, **solo happy paths con verificación física en BD** (consulta Dapper tras la respuesta HTTP) **+ el error de auth esperado (401 sin token)**. El resto de casuística (validaciones, not-found, reglas de dominio, conversión de divisas) ya está cubierto en unit tests baratos (Tasks 1, 4, 5).

**Autenticación E2E (decisión):** NO usamos un `TestAuthHandler` que se salte la auth (haría falso el 401). En su lugar, un helper `AuthenticatedClient(userId, email)` resuelve el **`IJwtService` real** del propio factory y acuña un **token real** (con el `sub` cifrado por `IUserIdEncryptor`, firmado con el `Jwt:Secret` de la app), que pasa por el **pipeline de validación real** (`JwtBearer`). El test de 401 simplemente omite el header. El usuario debe existir en `bigschool_test` (lo siembra el test por EF antes de autenticar, por la FK `Transactions.IdUser`).

**Nota sobre el body:** en el happy path se **omite `currency`** (la API la default-ea a la moneda base del usuario, EUR, con `rate = 1` y sin red). Así el E2E no se acopla a la (de)serialización de enums del controller (hoy numérica). Ejercitar conversión USD→EUR por endpoint queda fuera del mínimo E2E (ya cubierto en unit + en el flujo de `CreateTransactionCommandHandler`).

- [x] **Step 1: Ampliar el reset del fixture compartido (de Plan 2A) con `Transactions`/`Users`**

En `src/backend/tests/BigSchool.Integration.Tests/Fixtures/MySqlDatabaseFixture.cs`, sustituir el cuerpo de `ResetAsync` por:

```csharp
    public async Task ResetAsync()
    {
        await using var conn = new MySqlConnection(ConnectionString);
        await conn.OpenAsync();
        // Conserva esquema + seed global (SubCategories con IdUser IS NULL, ExchangeRates 'seed'); limpia lo mutable.
        await conn.ExecuteAsync("""
            SET FOREIGN_KEY_CHECKS = 0;
            TRUNCATE TABLE Transactions;
            DELETE FROM SubCategories WHERE IdUser IS NOT NULL;
            DELETE FROM Users;
            DELETE FROM ExchangeRates WHERE Source <> 'seed';
            SET FOREIGN_KEY_CHECKS = 1;
            """);
    }
```

**Fallback** si MySqlConnector rechazara el multi-statement en un solo `ExecuteAsync`: separar en llamadas `ExecuteAsync` individuales (una por sentencia), manteniendo el `SET FOREIGN_KEY_CHECKS` al principio/fin.

- [x] **Step 2: Crear el `WebApplicationFactory` que apunta a la BD de test**

```csharp
// src/backend/tests/BigSchool.Integration.Tests/Fixtures/BigSchoolWebAppFactory.cs
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace BigSchool.Integration.Tests.Fixtures;

/// <summary>
/// Arranca la API real (pipeline completo: auth, MediatR, EF, Dapper) contra la BD de test.
/// Sobrescribe ConnectionStrings:DefaultConnection (clave única de conexión: la leen EF y Dapper).
/// </summary>
public sealed class BigSchoolWebAppFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public BigSchoolWebAppFactory(string connectionString) => _connectionString = connectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString
            });
        });
    }
}
```

**Nota:** el resto de configuración (`Jwt:Secret`/`Issuer`/`Audience`, etc.) sale del `appsettings*.json` del WebApi que el factory carga por defecto, de modo que el token acuñado por `IJwtService` y la validación `JwtBearer` comparten el mismo secreto.

- [x] **Step 3: Escribir los tests E2E (happy path + 401)**

```csharp
// src/backend/tests/BigSchool.Integration.Tests/Transactions/TransactionsEndpointTests.cs
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
        await _fixture.ResetAsync();                       // estado limpio (la suite ya migró la BD)
        _factory = new BigSchoolWebAppFactory(_fixture.ConnectionString);
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    // Siembra un usuario real en bigschool_test (necesario por la FK Transactions.IdUser) y devuelve su id + email.
    private async Task<(int Id, string Email)> SeedUserAsync(Currency baseCurrency = Currency.EUR)
    {
        var email = $"e2e-{Guid.NewGuid():N}@test.com";
        var options = new DbContextOptionsBuilder<BigSchoolDbContext>()
            .UseMySql(_fixture.ConnectionString, ServerVersion.AutoDetect(_fixture.ConnectionString))
            .Options;
        await using var ctx = new BigSchoolDbContext(options, Mock.Of<IMediator>());
        var user = User.Create(email, "h", "s", "E2E User", baseCurrency);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync(dispatchEvents: false);
        return (user.IdUser, email);
    }

    // Cliente con JWT REAL acuñado por el IJwtService de la app → pasa por la validación JwtBearer real.
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

        // Crea un ingreso (currency omitido → default a la base EUR, rate 1, sin red).
        var create = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            type = (int)TransactionType.Income,
            idMainCategory = (int)MainCategory.Salary,
            transactionDate = "2026-06-10",
            amount = 1500m
        });
        create.StatusCode.Should().Be(HttpStatusCode.OK);

        // Consulta el summary por el endpoint real.
        var summary = await client.GetFromJsonAsync<ApiEnvelope<SummaryPayload>>("/api/v1/transactions/summary");
        summary!.Data.TotalIncome.Should().Be(1500m);
        summary.Data.BaseCurrency.Should().Be("EUR");

        // Verificación FÍSICA en MySQL: la fila quedó persistida.
        await using var conn = new MySqlConnection(_fixture.ConnectionString);
        var rows = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Transactions WHERE IdUser = @userId AND IdStatus <> @statusDeleted;",
            new { userId, statusDeleted = EntityStatus.Deleted });
        rows.Should().Be(1);
    }

    [Fact]
    public async Task Get_Summary_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient(); // sin Authorization
        var response = await client.GetAsync("/api/v1/transactions/summary");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // Tipos mínimos para deserializar el envelope ApiResponse<T> (System.Text.Json es case-insensitive).
    // Ajustar los nombres a la forma real de ApiResponse<T> si difiere (p. ej. 'data'/'success').
    private record ApiEnvelope<T>(bool Success, T Data);
    private record SummaryPayload(decimal TotalIncome, decimal TotalExpense, decimal Balance, string BaseCurrency);
}
```

- [x] **Step 4: Ejecutar los tests de integración**

Prerequisito: el servicio `mysql` de `infra/docker-compose` levantado (ver Plan 2A, Task 8). El fixture compartido ya creó/migró `bigschool_test`; estos tests reusan esa BD.

Run: `dotnet test src/backend/tests/BigSchool.Integration.Tests`
Expected: PASS. Happy path (200 + summary en EUR + fila persistida) y 401 sin token.

- [x] **Step 5: Commit**

```bash
git add -A && git commit -m "test: E2E de endpoints de transacciones (happy path + 401) con JWT real"
```

---

### Task 10: Documentación (design + diario)

**Files:**
- Modify: `docs/02-backend-design.md`
- Modify: `docs/diario.md`

- [x] **Step 1: Actualizar `docs/02-backend-design.md`**

- Completar la tabla `Transactions` con las columnas del snapshot (`OriginalAmount`, `OriginalCurrency`, `ExchangeRate`, `BaseAmount`, `BaseCurrency`, `RateDate`, FKs, índice `(IdUser, TransactionDate)`).
- Listar los endpoints implementados de `/api/v1/transactions` (POST, PUT/{id}, DELETE/{id}, GET, GET/{id}, GET/summary, GET/monthly-chart) y `/api/v1/categories` (GET), indicando que el `currency` es opcional en creación (default a moneda base).
- Añadir la nota CQRS: queries consolidan sobre `BaseAmount`.

- [x] **Step 2: Actualizar `docs/diario.md`**

Añadir entrada: BC Finanzas Personales (Plan 2B) implementado — agregado `Transaction` con snapshot multimoneda, CQRS completo (Create/Update/Delete + queries consolidadas en base), endpoints REST y tests (unit + integración). Cierra el Plan 2.

- [x] **Step 3: Commit**

```bash
git add -A && git commit -m "docs: actualizar diseño backend y diario con BC Transaction (Plan 2B)"
```

---

### Task 11: Refactor de `ExchangeRateApiClient` a la convención SQL/Dapper

**Files:**
- Modify: `src/backend/src/BigSchool.Infrastructure/Services/ExchangeRateApiClient.cs`

**Nota de diseño:** `ExchangeRateApiClient` (Plan 2A, Task 7) se implementó con el SQL incrustado en línea (`const string sql` local) y parámetros como objetos anónimos, antes de consolidar la convención (ver Anexo A de Plan 2A y `AGENTS.md` › *Dapper y SQL*). Esta tarea lo alinea: **`private const string ..._QUERY` a nivel de clase (UPPERCASE) + `DynamicParameters`**. Es **estilo/consistencia, sin cambio de comportamiento**; los tests de integración de Plan 2A (Task 8: cache hit/miss + seed) son la red de seguridad y **deben seguir en verde** sin tocarlos.

- [x] **Step 1: Extraer las queries a `const` UPPERCASE `_QUERY` a nivel de clase**

En `ExchangeRateApiClient`, mover el SQL de `ReadCacheAsync`, `ReadLastKnownAsync` y `UpsertCacheAsync` a constantes de clase:

```csharp
    private const string READCACHE_QUERY = @"SELECT Rate FROM ExchangeRates
                                             WHERE FromCurrency = @From AND ToCurrency = @To AND RateDate = @Date
                                             LIMIT 1;";

    private const string READLASTKNOWN_QUERY = @"SELECT Rate FROM ExchangeRates
                                                 WHERE FromCurrency = @From AND ToCurrency = @To
                                                 ORDER BY RateDate DESC LIMIT 1;";

    private const string UPSERTCACHE_QUERY = @"INSERT INTO ExchangeRates (FromCurrency, ToCurrency, Rate, RateDate, Source, FetchedAt)
                                               VALUES (@From, @To, @Rate, @Date, @Source, @Now)
                                               ON DUPLICATE KEY UPDATE Rate = @Rate, Source = @Source, FetchedAt = @Now;";
```

- [x] **Step 2: Migrar los parámetros de objeto anónimo a `DynamicParameters`**

En cada método, sustituir el objeto anónimo por `DynamicParameters` y usar la query nombrada. Ejemplo (`ReadCacheAsync`):

```csharp
    private async Task<decimal?> ReadCacheAsync(Currency from, Currency to, DateOnly date)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@From", from.ToString());
        parameters.Add("@To", to.ToString());
        parameters.Add("@Date", date.ToDateTime(TimeOnly.MinValue).Date);

        using var conn = _dbFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<decimal?>(READCACHE_QUERY, parameters);
    }
```

Aplicar lo análogo a `ReadLastKnownAsync` (parámetros `@From`, `@To`) y `UpsertCacheAsync` (`@From`, `@To`, `@Rate`, `@Date`, `@Source`, `@Now`).

- [x] **Step 3: Verificar build + tests de integración (red de seguridad de Plan 2A)**

Prerequisito: `mysql` de docker-compose levantado.
Run: `dotnet build src/backend/Backend.slnx`
Expected: 0 errors.
Run: `dotnet test src/backend/tests/BigSchool.Integration.Tests --filter "FullyQualifiedName~ExchangeRateApiClientIntegrationTests"`
Expected: PASS (mismo comportamiento; cache miss→hit intacto).

- [x] **Step 4: Actualizar el diario**

En `docs/diario.md`, añadir una nota a la entrada de Plan 2B: `ExchangeRateApiClient` refactorizado a la convención SQL/Dapper (cierre de la deuda registrada en el Anexo A de Plan 2A).

- [x] **Step 5: Commit**

```bash
git add -A && git commit -m "refactor: alinear ExchangeRateApiClient con la convención SQL/Dapper (const _QUERY + DynamicParameters)"
```

---

## Verificación final del Plan 2B

- [ ] **Build completo**

Run: `dotnet build src/backend/Backend.slnx`
Expected: 0 errors.

- [ ] **Toda la suite de tests**

Run: `dotnet test src/backend/Backend.slnx`
Expected: todos PASS (integración requiere Docker).

- [ ] **Prueba manual E2E (Swagger)**

Run: `dotnet run --project src/backend/src/BigSchool.WebApi` y en `/swagger`:
1. `POST /api/v1/auth/register` → obtener JWT.
2. Autorizar con el Bearer.
3. `POST /api/v1/transactions` con `{ "type":1, "idMainCategory":5, "transactionDate":"2026-06-17", "amount":100, "currency":"USD" }` → 200 con `baseAmount` convertido.
4. `GET /api/v1/transactions/summary` → income/expense/balance en moneda base.
5. `GET /api/v1/categories` → categorías + subcategorías.

- [ ] **Checklist de cobertura de la spec**

- [ ] `Transaction` AR (Create/Update/Delete, MoneyConversion, evento) — Task 1 (spec §7.1)
- [ ] Owned type + migración `Transactions` — Task 2 (spec §7.2)
- [ ] Repositorio — Task 3
- [ ] `CreateTransaction` con flujo de conversión — Task 4 (spec §6, §7.4)
- [ ] `UpdateTransaction` + `DeleteTransaction` (soft) — Task 5 (spec §7.4)
- [ ] Queries consolidadas en base (GetById, GetTransactions, Summary, MonthlyChart) — Task 6 (spec §7.4)
- [ ] `GetCategories` — Task 7 (spec §7.3, §7.4)
- [ ] Endpoints `/transactions` y `/categories` + `currency` opcional — Task 8 (spec §7.5)
- [ ] Tests E2E de endpoints (happy path + 401, verificación física) — Task 9 (spec §9)
- [ ] Docs — Task 10 (spec §8)
- [ ] Refactor `ExchangeRateApiClient` a convención SQL/Dapper — Task 11 (Anexo A Plan 2A)
