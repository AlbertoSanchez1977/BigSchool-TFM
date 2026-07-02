# Backend — Monolito Modular Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reorganizar el backend (hoy layer-first) en un **monolito modular** con módulos funcionales de frontera explícita (Auth, Finanzas, Investments, Notifications-preparado) + SharedKernel, comunicación inter-módulo por IntegrationEvents sobre un bus in-memory con outbox, DI Autofac por módulo y guard tests, **sin cambiar comportamiento** (salvo la decisión Categorías→Finanzas, que solo se *reubica* aquí y se re-modela en Spec 009).

**Architecture:** Se mantienen los 4 proyectos de capa (`BigSchool.Domain/.Application/.Infrastructure/.WebApi`). Dentro de cada uno se crea una carpeta por módulo funcional + `SharedKernel`, y **dentro de cada módulo se conservan las subcarpetas por categoría** (`Entities/`, `Events/`, `Interfaces/`, `Enums/`, `ValueObjects/`, `Exceptions/` en Domain; `Commands/`, `Queries/`, `DTOs/`, `Interfaces/` en Application; `Persistence/`, `Services/` en Infrastructure). **Convención estricta: namespace = ruta de carpeta** (C#/.NET estándar; spec 005 §5): `SharedKernel/Entities/BaseEntity.cs` → `namespace BigSchool.Domain.SharedKernel.Entities`; `Auth/Entities/User.cs` → `namespace BigSchool.Domain.Auth.Entities`. La comunicación cross-módulo pasa por `IIntegrationEvent` + contratos públicos en `Application/SharedKernel/IntegrationEvents/Contracts`, publicados vía un **outbox** transaccional drenado post-commit por un `IIntegrationEventBus` in-memory. Un nuevo proyecto `BigSchool.Architecture.Tests` (NetArchTest) protege las fronteras.

**Tech Stack:** .NET 8, C#, Clean Architecture + DDD + CQRS (MediatR), EF Core + MySQL 8, Dapper, Autofac (DI modular), FluentValidation, xUnit + FluentAssertions + Moq, NetArchTest.Rules.

---

## Convenciones para el ejecutor (LEER ANTES DE EMPEZAR)

Este plan es en su mayor parte un **refactor mecánico de reubicación** (mover ficheros + cambiar la línea `namespace` según la carpeta destino + corregir `using`s). No hay cambio de comportamiento en las Tareas 1–5, 8; el criterio de éxito es **build limpio + toda la suite existente en verde**. Las Tareas 6, 7, 9 añaden código nuevo con TDD.

### La regla universal (memorízala)

> **El namespace de todo fichero `.cs` es EXACTAMENTE su ruta de carpeta bajo `src/`**, con `/` → `.`.
> Ej.: `src/BigSchool.Domain/Finanzas/Enums/TransactionType.cs` → `namespace BigSchool.Domain.Finanzas.Enums`.

Con esta regla no necesitas una tabla tipo→namespace: **mueve el fichero a la carpeta indicada y el namespace se deriva solo**. Para resolver un `using` roto: el namespace del tipo que falta = la carpeta a la que lo moviste.

### Procedimiento de cada tarea de reubicación

1. Mueve los ficheros a su carpeta destino con `git mv` (conserva historial).
2. Ajusta la línea `namespace ...;` de cada fichero movido = su nueva ruta de carpeta.
3. Compila (`dotnet build`, cwd `src/backend`). El compilador lista cada `using`/tipo roto (`CS0234`/`CS0246`).
4. Para **cada** error, añade/ajusta el `using` del consumidor al namespace = carpeta destino del tipo. No borres lógica, solo corriges `using`s (y elimina los redundantes por quedar en el mismo namespace).
5. Repite build hasta `0 Error(s)`. Corre tests. Commit.

> El churn es grande (~95 ficheros usan `BigSchool.Domain.Enums`, ~75 usan `.Entities`). Deja que **el build guíe**: distintos tipos del viejo namespace van a módulos distintos, así que no hay find-replace global válido — se resuelve error a error.

### Estructura destino (carpeta → namespace)

**`src/BigSchool.Domain/`** — cada módulo conserva sus subcarpetas por categoría:
```
SharedKernel/Entities/        → BigSchool.Domain.SharedKernel.Entities     (BaseEntity, IAggregateRoot, ExchangeRate)
SharedKernel/Events/          → .SharedKernel.Events                        (IDomainEvent)
SharedKernel/Interfaces/      → .SharedKernel.Interfaces                    (IUnitOfWork)
SharedKernel/ValueObjects/    → .SharedKernel.ValueObjects                  (Money, MoneyConversion)
SharedKernel/Enums/           → .SharedKernel.Enums                         (Currency, EntityStatus)
SharedKernel/Exceptions/      → .SharedKernel.Exceptions                    (DomainException, ConflictException, NotFoundException)
Auth/Entities/                → BigSchool.Domain.Auth.Entities              (User)
Auth/Exceptions/              → .Auth.Exceptions                            (EmailAlreadyExists…, InvalidCredentials…)
Finanzas/Entities/            → BigSchool.Domain.Finanzas.Entities          (Transaction, SubCategory)
Finanzas/Enums/               → .Finanzas.Enums                             (TransactionType, MainCategory, RecurrencePeriod)
Finanzas/Events/              → .Finanzas.Events                            (TransactionCreatedEvent)
Finanzas/Exceptions/          → .Finanzas.Exceptions                        (DuplicateSubCategory…)
Investments/Entities/         → BigSchool.Domain.Investments.Entities       (Company, Valuation, Portfolio, Holding, Disposal)
Investments/Enums/            → .Investments.Enums                          (Market, Sector)
Investments/Exceptions/       → .Investments.Exceptions                     (DuplicateTicker…, DuplicateValuation…, InsufficientShares…)
Rag/Entities/                 → BigSchool.Domain.Rag.Entities               (RagDocument)  [futuro]
```

**`src/BigSchool.Application/`**
```
SharedKernel/Common/          → BigSchool.Application.SharedKernel.Common        (ApiError, ApiResponse, PagedResult)
SharedKernel/Events/          → .SharedKernel.Events                             (DomainEventNotification)
SharedKernel/Infrastructure/  → .SharedKernel.Infrastructure                     (CustomMediatR, PublishStrategy)
SharedKernel/Behaviors/       → .SharedKernel.Behaviors                          (ValidationBehavior, NotificationExceptionBehavior, +OutboxDispatchBehavior en T7)
SharedKernel/Interfaces/      → .SharedKernel.Interfaces                         (IRepository, IDbConnectionFactory, IExchangeRateProvider)
SharedKernel/Configuration/   → .SharedKernel.Configuration                      (AppSettings + settings anidados)
SharedKernel/IntegrationEvents/     → .SharedKernel.IntegrationEvents            (T6/T7)
SharedKernel/IntegrationEvents/Contracts/ → .SharedKernel.IntegrationEvents.Contracts  (.gitkeep; contratos en Spec 007)
Auth/Commands/{Login,Refresh,Register}/ → BigSchool.Application.Auth.Commands.{…}
Auth/DTOs/                    → .Auth.DTOs                                       (AuthResponseDto)
Auth/Interfaces/              → .Auth.Interfaces                                 (IUserRepository, IJwtService, IPasswordHasher, IUserIdEncryptor)
Finanzas/Commands/{Create,Update,Delete}/ → .Finanzas.Commands.{…}
Finanzas/Queries/Transactions/{…}/  → .Finanzas.Queries.Transactions.{…}
Finanzas/Queries/Categories/{…}/    → .Finanzas.Queries.Categories.{…}
Finanzas/DTOs/                → .Finanzas.DTOs                                   (TransactionDto, TransactionListItemDto)
Finanzas/Interfaces/          → .Finanzas.Interfaces                             (ITransactionRepository)
Investments/Commands/{…}/     → .Investments.Commands.{…}
Investments/Queries/{…}/      → .Investments.Queries.{…}
Investments/DTOs/             → .Investments.DTOs
Investments/Interfaces/       → .Investments.Interfaces                          (ICompanyRepository, IPortfolioRepository)
Rag/Interfaces/               → .Rag.Interfaces                                  (IRagServiceClient)  [futuro]
```

**`src/BigSchool.Infrastructure/`**
```
SharedKernel/Persistence/     → BigSchool.Infrastructure.SharedKernel.Persistence (BigSchoolDbContext, DbConnectionMySqlFactory, DateOnlyTypeHandler, EFRepository, ExchangeRateConfiguration, +Outbox en T7)
SharedKernel/Persistence/Converters/ → .SharedKernel.Persistence.Converters
SharedKernel/Persistence/Extensions/ → .SharedKernel.Persistence.Extensions      (SeedDataExtensions)
SharedKernel/Persistence/Migrations/ → **NO CAMBIAR namespace** (ver T4 Step 2)
SharedKernel/Services/        → .SharedKernel.Services                            (ExchangeRateApiClient, FrankfurterResponse)
SharedKernel/IntegrationEvents/ → .SharedKernel.IntegrationEvents                 (T6/T7)
SharedKernel/DI/              → .SharedKernel.DI                                  (SharedKernelModule, T8)
Auth/Persistence/             → BigSchool.Infrastructure.Auth.Persistence         (UserConfiguration, UserRepository)
Auth/Services/                → .Auth.Services                                    (Argon2PasswordHasher, JwtService, DataProtectionUserIdEncryptor)
Auth/DI/                      → .Auth.DI                                          (AuthModule, T8)
Finanzas/Persistence/         → BigSchool.Infrastructure.Finanzas.Persistence     (Transaction/SubCategory Configuration, TransactionRepository)
Finanzas/DI/                  → .Finanzas.DI                                      (FinanzasModule, T8)
Investments/Persistence/      → BigSchool.Infrastructure.Investments.Persistence  (5 Configurations, Company/Portfolio Repository)
Investments/DI/               → .Investments.DI                                   (InvestmentsModule, T8)
```

**`src/BigSchool.WebApi/`**
```
Controllers/Auth/             → BigSchool.WebApi.Controllers.Auth                (AuthController)
Controllers/Finanzas/         → BigSchool.WebApi.Controllers.Finanzas            (TransactionsController, CategoriesController)
Controllers/Investments/      → BigSchool.WebApi.Controllers.Investments         (CompaniesController, PortfoliosController)
Common/, Middleware/          → sin cambios (composición/host)
```

### Recetas de verificación (referenciadas por las tareas)

- **BUILD**: cwd `src/backend` → `dotnet build` → *Expected:* `Build succeeded. 0 Error(s)`.
- **UNIT**: `dotnet test tests/BigSchool.Domain.Tests` y `dotnet test tests/BigSchool.Application.Tests` → *Expected:* `Passed!`. No requieren BD.
- **ARCH**: `dotnet test tests/BigSchool.Architecture.Tests` (existe desde la Tarea 9).
- **INTEGRATION** (requiere MySQL): arranca el MySQL de `infra/docker-compose.yml` (servicio `mysql`, `localhost:3306`) como se hace normalmente en el repo; si tus credenciales difieren, exporta `BIGSCHOOL_TEST_MYSQL` (ver `tests/BigSchool.Integration.Tests/Fixtures/MySqlDatabaseFixture.cs`). Luego `dotnet test tests/BigSchool.Integration.Tests` → *Expected:* `Passed!`.
- **FULL**: BUILD + UNIT + INTEGRATION todo verde.

> En cada tarea de reubicación hay que arreglar también los `using` de los **tests** (referencian los proyectos de producción). `dotnet build` compila la solución entera, así que los delata.

### Ramas y PRs

Cada tarea = **una rama `feature/018-modular-monolith-taskN`** (desde esta rama base `feature/005-006-backend-fundacional`, o desde la tarea previa mergeada) = un PR pequeño con checkpoint humano (skill `feature-branch-workflow`).

---

## Task 1: SharedKernel del dominio

Mueve los building blocks compartidos + `ExchangeRate` al módulo `SharedKernel` del dominio, conservando las subcarpetas por categoría. Es el cimiento del que dependen los demás módulos.

**Files (origen → carpeta destino):**
- `Entities/BaseEntity.cs`, `Entities/IAggregateRoot.cs`, `Entities/ExchangeRate.cs` → `SharedKernel/Entities/`
- `Events/IDomainEvent.cs` → `SharedKernel/Events/`
- `Interfaces/IUnitOfWork.cs` → `SharedKernel/Interfaces/`
- `ValueObjects/{Money,MoneyConversion}.cs` → `SharedKernel/ValueObjects/`
- `Enums/{Currency,EntityStatus}.cs` → `SharedKernel/Enums/`
- `Exceptions/{DomainException,ConflictException,NotFoundException}.cs` → `SharedKernel/Exceptions/`

- [x] **Step 1: Mover ficheros**

Run (cwd `src/backend/src/BigSchool.Domain`):
```bash
mkdir -p SharedKernel/Entities SharedKernel/Events SharedKernel/Interfaces SharedKernel/ValueObjects SharedKernel/Enums SharedKernel/Exceptions
git mv Entities/BaseEntity.cs Entities/IAggregateRoot.cs Entities/ExchangeRate.cs SharedKernel/Entities/
git mv Events/IDomainEvent.cs SharedKernel/Events/
git mv Interfaces/IUnitOfWork.cs SharedKernel/Interfaces/
git mv ValueObjects/Money.cs ValueObjects/MoneyConversion.cs SharedKernel/ValueObjects/
git mv Enums/Currency.cs Enums/EntityStatus.cs SharedKernel/Enums/
git mv Exceptions/DomainException.cs Exceptions/ConflictException.cs Exceptions/NotFoundException.cs SharedKernel/Exceptions/
```

- [x] **Step 2: Ajustar `namespace` = carpeta destino**

- `SharedKernel/Entities/*.cs` → `namespace BigSchool.Domain.SharedKernel.Entities;`
- `SharedKernel/Events/IDomainEvent.cs` → `namespace BigSchool.Domain.SharedKernel.Events;`
- `SharedKernel/Interfaces/IUnitOfWork.cs` → `namespace BigSchool.Domain.SharedKernel.Interfaces;`
- `SharedKernel/ValueObjects/*.cs` → `namespace BigSchool.Domain.SharedKernel.ValueObjects;`
- `SharedKernel/Enums/*.cs` → `namespace BigSchool.Domain.SharedKernel.Enums;`
- `SharedKernel/Exceptions/*.cs` → `namespace BigSchool.Domain.SharedKernel.Exceptions;`

> Corrige los `using` internos entre ficheros movidos: `MoneyConversion` (usa `Currency`) → `using BigSchool.Domain.SharedKernel.Enums;`; `BaseEntity` (usa `IDomainEvent`) → `using BigSchool.Domain.SharedKernel.Events;`; excepciones derivadas de `DomainException` comparten `.Exceptions` → elimina el `using` redundante.

- [x] **Step 3: Compilar y resolver usings (regla universal)**

Run: BUILD. Mapa de reemplazos (namespace viejo → nuevo, según el tipo usado):
- `using BigSchool.Domain.Entities;` → si el tipo era `BaseEntity`/`IAggregateRoot`/`ExchangeRate`, `using BigSchool.Domain.SharedKernel.Entities;` (conserva `.Entities` si además usaba entidades aún no movidas).
- `using BigSchool.Domain.Interfaces;` (`IUnitOfWork`) → `using BigSchool.Domain.SharedKernel.Interfaces;`
- `using BigSchool.Domain.Events;` (`IDomainEvent`) → `using BigSchool.Domain.SharedKernel.Events;` (mantén `.Events` si usaba `TransactionCreatedEvent`, aún no movido).
- `using BigSchool.Domain.ValueObjects;` → `using BigSchool.Domain.SharedKernel.ValueObjects;`
- `using BigSchool.Domain.Enums;` → si usaba `Currency`/`EntityStatus`, `using BigSchool.Domain.SharedKernel.Enums;` (mantén `.Enums` para `TransactionType`/`Market`/etc.).
- `using BigSchool.Domain.Exceptions;` → si usaba las 3 base, `using BigSchool.Domain.SharedKernel.Exceptions;` (mantén `.Exceptions` para las específicas).

Repite hasta `0 Error(s)`. Incluye ficheros de test.

- [x] **Step 4: Verificar suite completa**

Run: FULL. *Expected:* verde (sin cambio de comportamiento).

- [x] **Step 5: Commit**

```bash
git add -A
git commit -m "refactor(backend): mover building blocks del dominio a SharedKernel

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 2: Módulos de dominio (Auth / Finanzas / Investments / Rag)

Reubica entidades de negocio, enums, eventos y excepciones específicas a sus módulos, cada categoría en su subcarpeta. Al terminar, `Entities/`, `Enums/`, `Events/`, `Exceptions/` (raíz de Domain) quedan vacías y se eliminan.

**Files (origen → carpeta destino):**
- Auth: `Entities/User.cs` → `Auth/Entities/`; `Exceptions/{EmailAlreadyExistsDomainException,InvalidCredentialsDomainException}.cs` → `Auth/Exceptions/`
- Finanzas: `Entities/{Transaction,SubCategory}.cs` → `Finanzas/Entities/`; `Enums/{TransactionType,MainCategory,RecurrencePeriod}.cs` → `Finanzas/Enums/`; `Events/TransactionCreatedEvent.cs` → `Finanzas/Events/`; `Exceptions/DuplicateSubCategoryDomainException.cs` → `Finanzas/Exceptions/`
- Investments: `Entities/{Company,Valuation,Portfolio,Holding,Disposal}.cs` → `Investments/Entities/`; `Enums/{Market,Sector}.cs` → `Investments/Enums/`; `Exceptions/{DuplicateTickerDomainException,DuplicateValuationDomainException,InsufficientSharesDomainException}.cs` → `Investments/Exceptions/`
- Rag: `Entities/RagDocument.cs` → `Rag/Entities/`

- [x] **Step 1: Mover ficheros**

Run (cwd `src/backend/src/BigSchool.Domain`):
```bash
mkdir -p Auth/Entities Auth/Exceptions Finanzas/Entities Finanzas/Enums Finanzas/Events Finanzas/Exceptions Investments/Entities Investments/Enums Investments/Exceptions Rag/Entities
git mv Entities/User.cs Auth/Entities/
git mv Exceptions/EmailAlreadyExistsDomainException.cs Exceptions/InvalidCredentialsDomainException.cs Auth/Exceptions/
git mv Entities/Transaction.cs Entities/SubCategory.cs Finanzas/Entities/
git mv Enums/TransactionType.cs Enums/MainCategory.cs Enums/RecurrencePeriod.cs Finanzas/Enums/
git mv Events/TransactionCreatedEvent.cs Finanzas/Events/
git mv Exceptions/DuplicateSubCategoryDomainException.cs Finanzas/Exceptions/
git mv Entities/Company.cs Entities/Valuation.cs Entities/Portfolio.cs Entities/Holding.cs Entities/Disposal.cs Investments/Entities/
git mv Enums/Market.cs Enums/Sector.cs Investments/Enums/
git mv Exceptions/DuplicateTickerDomainException.cs Exceptions/DuplicateValuationDomainException.cs Exceptions/InsufficientSharesDomainException.cs Investments/Exceptions/
git mv Entities/RagDocument.cs Rag/Entities/
rmdir Entities Enums Events Exceptions
```
*(Si `rmdir` falla, quedó algún fichero sin mover: revísalo contra la lista de arriba.)*

- [x] **Step 2: Ajustar `namespace` = carpeta destino**

- `Auth/Entities/User.cs` → `namespace BigSchool.Domain.Auth.Entities;` · `Auth/Exceptions/*` → `namespace BigSchool.Domain.Auth.Exceptions;`
- `Finanzas/Entities/*` → `.Finanzas.Entities;` · `Finanzas/Enums/*` → `.Finanzas.Enums;` · `Finanzas/Events/*` → `.Finanzas.Events;` · `Finanzas/Exceptions/*` → `.Finanzas.Exceptions;`
- `Investments/Entities/*` → `.Investments.Entities;` · `Investments/Enums/*` → `.Investments.Enums;` · `Investments/Exceptions/*` → `.Investments.Exceptions;`
- `Rag/Entities/RagDocument.cs` → `namespace BigSchool.Domain.Rag.Entities;`

> Corrige `using`s internos entre subcarpetas del mismo módulo: p. ej. `Transaction.cs` (en `.Finanzas.Entities`) que usa `TransactionType` necesita `using BigSchool.Domain.Finanzas.Enums;`; entidades que lanzan sus excepciones de dominio → `using BigSchool.Domain.{Módulo}.Exceptions;`.

- [x] **Step 3: Compilar y resolver usings**

Run: BUILD. Namespaces destino frecuentes:
- `User` → `BigSchool.Domain.Auth.Entities`
- `Transaction`/`SubCategory` → `BigSchool.Domain.Finanzas.Entities`; `TransactionType`/`MainCategory`/`RecurrencePeriod` → `BigSchool.Domain.Finanzas.Enums`; `TransactionCreatedEvent` → `BigSchool.Domain.Finanzas.Events`
- `Company`/`Portfolio`/`Holding`/`Disposal`/`Valuation` → `BigSchool.Domain.Investments.Entities`; `Market`/`Sector` → `BigSchool.Domain.Investments.Enums`
- Excepciones específicas → `BigSchool.Domain.{Módulo}.Exceptions`

Repite hasta 0 errores (incluye tests).

- [x] **Step 4: Verificar**

Run: FULL. *Expected:* verde.

- [x] **Step 5: Commit**

```bash
git add -A
git commit -m "refactor(backend): reubicar entidades de dominio por módulo (Auth/Finanzas/Investments/Rag)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 3: SharedKernel + módulos de Application

Reubica Commands/Queries/DTOs/Interfaces por módulo y los building blocks transversales a `Application/SharedKernel`.

**Files (origen → carpeta destino):**
- SharedKernel: `Common/*` → `SharedKernel/Common/`; `Events/DomainEventNotification.cs` → `SharedKernel/Events/`; `Infrastructure/CustomMediatR.cs` → `SharedKernel/Infrastructure/`; `Behaviors/*` → `SharedKernel/Behaviors/`; `Interfaces/IRepository.cs` + `Interfaces/IDbConnectionFactory.cs` + `Interfaces/Services/IExchangeRateProvider.cs` → `SharedKernel/Interfaces/`; `Configuration/AppSettings.cs` → `SharedKernel/Configuration/`
- Auth: `Commands/Auth/*` → `Auth/Commands/`; `DTOs/Auth/*` → `Auth/DTOs/`; `Interfaces/Repositories/IUserRepository.cs` + `Interfaces/Services/{IJwtService,IPasswordHasher,IUserIdEncryptor}.cs` → `Auth/Interfaces/`
- Finanzas: `Commands/Transactions/*` → `Finanzas/Commands/`; `Queries/Transactions/*` → `Finanzas/Queries/Transactions/`; `Queries/Categories/*` → `Finanzas/Queries/Categories/`; `DTOs/Transactions/*` → `Finanzas/DTOs/`; `Interfaces/Repositories/ITransactionRepository.cs` → `Finanzas/Interfaces/`
- Investments: `Commands/Investments/*` → `Investments/Commands/`; `Queries/Investments/*` → `Investments/Queries/`; `DTOs/Investments/*` → `Investments/DTOs/`; `Interfaces/Repositories/{ICompanyRepository,IPortfolioRepository}.cs` → `Investments/Interfaces/`
- Rag: `Interfaces/Services/IRagServiceClient.cs` → `Rag/Interfaces/`

- [ ] **Step 1: Mover ficheros**

Run (cwd `src/backend/src/BigSchool.Application`):
```bash
mkdir -p SharedKernel/Common SharedKernel/Events SharedKernel/Infrastructure SharedKernel/Behaviors SharedKernel/Interfaces SharedKernel/Configuration
mkdir -p Auth/Interfaces Finanzas/Interfaces Investments/Interfaces Rag/Interfaces
# SharedKernel
git mv Common/ApiError.cs Common/ApiResponse.cs Common/PagedResult.cs SharedKernel/Common/
git mv Events/DomainEventNotification.cs SharedKernel/Events/
git mv Infrastructure/CustomMediatR.cs SharedKernel/Infrastructure/
git mv Behaviors/ValidationBehavior.cs Behaviors/NotificationExceptionBehavior.cs SharedKernel/Behaviors/
git mv Interfaces/IRepository.cs Interfaces/IDbConnectionFactory.cs Interfaces/Services/IExchangeRateProvider.cs SharedKernel/Interfaces/
git mv Configuration/AppSettings.cs SharedKernel/Configuration/
# Auth
git mv Commands/Auth Auth/Commands
git mv DTOs/Auth Auth/DTOs
git mv Interfaces/Repositories/IUserRepository.cs Interfaces/Services/IJwtService.cs Interfaces/Services/IPasswordHasher.cs Interfaces/Services/IUserIdEncryptor.cs Auth/Interfaces/
# Finanzas
git mv Commands/Transactions Finanzas/Commands
mkdir -p Finanzas/Queries && git mv Queries/Transactions Finanzas/Queries/Transactions && git mv Queries/Categories Finanzas/Queries/Categories
git mv DTOs/Transactions Finanzas/DTOs
git mv Interfaces/Repositories/ITransactionRepository.cs Finanzas/Interfaces/
# Investments
git mv Commands/Investments Investments/Commands
git mv Queries/Investments Investments/Queries
git mv DTOs/Investments Investments/DTOs
git mv Interfaces/Repositories/ICompanyRepository.cs Interfaces/Repositories/IPortfolioRepository.cs Investments/Interfaces/
# Rag
git mv Interfaces/Services/IRagServiceClient.cs Rag/Interfaces/
# limpiar vacías
rmdir Commands DTOs Events Behaviors Infrastructure Configuration Queries Interfaces/Repositories Interfaces/Services Interfaces 2>/dev/null || true
```
*(Revisa manualmente cualquier carpeta que `rmdir` no borre: puede quedar un fichero no listado.)*

- [ ] **Step 2: Ajustar `namespace` = carpeta destino**

Recuerda: la carpeta completa es el namespace. Ejemplos:
- `SharedKernel/Common/*` → `namespace BigSchool.Application.SharedKernel.Common;`
- `SharedKernel/Infrastructure/CustomMediatR.cs` → `namespace BigSchool.Application.SharedKernel.Infrastructure;` (aquí viven `CustomMediatR` **y** `PublishStrategy`)
- `SharedKernel/Behaviors/*` → `.SharedKernel.Behaviors;` · `SharedKernel/Interfaces/*` → `.SharedKernel.Interfaces;` · `SharedKernel/Configuration/AppSettings.cs` → `.SharedKernel.Configuration;` · `SharedKernel/Events/*` → `.SharedKernel.Events;`
- `Auth/Commands/Register/*` → `.Auth.Commands.Register;` (idem Login/Refresh) · `Auth/DTOs/*` → `.Auth.DTOs;` · `Auth/Interfaces/*` → `.Auth.Interfaces;`
- `Finanzas/Commands/{Create,Update,Delete}/*` → `.Finanzas.Commands.{…};` · `Finanzas/Queries/Transactions/{Sub}/*` → `.Finanzas.Queries.Transactions.{Sub};` · `Finanzas/Queries/Categories/{Sub}/*` → `.Finanzas.Queries.Categories.{Sub};` · `Finanzas/DTOs/*` → `.Finanzas.DTOs;` · `Finanzas/Interfaces/*` → `.Finanzas.Interfaces;`
- `Investments/*` análogo (`.Investments.Commands.{Sub}`, `.Investments.Queries.{Sub}`, `.Investments.DTOs`, `.Investments.Interfaces`)
- `Rag/Interfaces/IRagServiceClient.cs` → `.Rag.Interfaces;`

- [ ] **Step 3: Compilar y resolver usings (puntos calientes)**

Run: BUILD. Reemplazos habituales + puntos que hay que verificar sí o sí:
- `Program.cs`: `using BigSchool.Application.Configuration;` → `…SharedKernel.Configuration;`; `using BigSchool.Application.Infrastructure;` (CustomMediatR) → `…SharedKernel.Infrastructure;`; `using BigSchool.Application.Behaviors;` (ValidationBehavior) → `…SharedKernel.Behaviors;`. Los `typeof(AppSettings)`/`typeof(ValidationBehavior<,>)` siguen válidos.
- `BigSchoolDbContext.cs`: `using BigSchool.Application.Events;` (`DomainEventNotification<>`) → `…SharedKernel.Events;`
- `EFRepository.cs`: `using BigSchool.Application.Interfaces;` (`IRepository`) → `…SharedKernel.Interfaces;`
- Handlers/validators/controllers: cada `using BigSchool.Application.Commands.X` / `.Queries.X` / `.DTOs.X` / `.Interfaces.*` → el namespace derivado de la nueva carpeta (regla universal).
- Los handlers referencian **entidades de dominio** (`BigSchool.Domain.Entities` → ahora `BigSchool.Domain.{Módulo}.Entities`, ya arreglado en T1–T2; si algún handler quedó con el `using` viejo, corrígelo aquí).

Repite hasta 0 errores (incluye los 3 proyectos de test).

- [ ] **Step 4: Verificar**

Run: FULL. *Expected:* verde.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "refactor(backend): reubicar Application por módulo + SharedKernel

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 4: Módulos de Infrastructure

Reparte configuraciones EF, repositorios y servicios por módulo. El `BigSchoolDbContext` sigue siendo único (SharedKernel) y aplica configs con `ApplyConfigurationsFromAssembly` (mecanismo intacto).

**Files (origen → carpeta destino):**
- SharedKernel: `Persistence/{BigSchoolDbContext,DbConnectionMySqlFactory,DateOnlyTypeHandler}.cs` → `SharedKernel/Persistence/`; `Persistence/Converters/*` → `SharedKernel/Persistence/Converters/`; `Persistence/Extensions/*` → `SharedKernel/Persistence/Extensions/`; `Persistence/Migrations/*` → `SharedKernel/Persistence/Migrations/`; `Persistence/Configurations/ExchangeRateConfiguration.cs` → `SharedKernel/Persistence/`; `Persistence/Repositories/EFRepository.cs` → `SharedKernel/Persistence/`; `Services/{ExchangeRateApiClient,FrankfurterResponse}.cs` → `SharedKernel/Services/`
- Auth: `Persistence/Configurations/UserConfiguration.cs` + `Persistence/Repositories/UserRepository.cs` → `Auth/Persistence/`; `Services/{Argon2PasswordHasher,JwtService,DataProtectionUserIdEncryptor}.cs` → `Auth/Services/`
- Finanzas: `Persistence/Configurations/{TransactionConfiguration,SubCategoryConfiguration}.cs` + `Persistence/Repositories/TransactionRepository.cs` → `Finanzas/Persistence/`
- Investments: `Persistence/Configurations/{Company,Portfolio,Holding,Disposal,Valuation}Configuration.cs` + `Persistence/Repositories/{CompanyRepository,PortfolioRepository}.cs` → `Investments/Persistence/`

- [ ] **Step 1: Mover ficheros**

Run (cwd `src/backend/src/BigSchool.Infrastructure`):
```bash
mkdir -p SharedKernel/Persistence SharedKernel/Services Auth/Persistence Auth/Services Finanzas/Persistence Investments/Persistence
git mv Persistence/BigSchoolDbContext.cs Persistence/DbConnectionMySqlFactory.cs Persistence/DateOnlyTypeHandler.cs SharedKernel/Persistence/
git mv Persistence/Converters SharedKernel/Persistence/Converters
git mv Persistence/Extensions SharedKernel/Persistence/Extensions
git mv Persistence/Migrations SharedKernel/Persistence/Migrations
git mv Persistence/Configurations/ExchangeRateConfiguration.cs Persistence/Repositories/EFRepository.cs SharedKernel/Persistence/
git mv Services/ExchangeRateApiClient.cs Services/FrankfurterResponse.cs SharedKernel/Services/
git mv Persistence/Configurations/UserConfiguration.cs Persistence/Repositories/UserRepository.cs Auth/Persistence/
git mv Services/Argon2PasswordHasher.cs Services/JwtService.cs Services/DataProtectionUserIdEncryptor.cs Auth/Services/
git mv Persistence/Configurations/TransactionConfiguration.cs Persistence/Configurations/SubCategoryConfiguration.cs Persistence/Repositories/TransactionRepository.cs Finanzas/Persistence/
git mv Persistence/Configurations/CompanyConfiguration.cs Persistence/Configurations/PortfolioConfiguration.cs Persistence/Configurations/HoldingConfiguration.cs Persistence/Configurations/DisposalConfiguration.cs Persistence/Configurations/ValuationConfiguration.cs Investments/Persistence/
git mv Persistence/Repositories/CompanyRepository.cs Persistence/Repositories/PortfolioRepository.cs Investments/Persistence/
rmdir Persistence/Configurations Persistence/Repositories Persistence Services 2>/dev/null || true
```

- [ ] **Step 2: Ajustar `namespace` = carpeta destino (⚠ excepción Migrations)**

- `SharedKernel/Persistence/*.cs` (DbContext, factory, DateOnlyTypeHandler, EFRepository, ExchangeRateConfiguration) → `namespace BigSchool.Infrastructure.SharedKernel.Persistence;`
- `SharedKernel/Persistence/Converters/*` → `…Persistence.Converters;` · `SharedKernel/Persistence/Extensions/*` → `…Persistence.Extensions;`
- `SharedKernel/Services/*` → `namespace BigSchool.Infrastructure.SharedKernel.Services;`
- `Auth/Persistence/*` → `…Auth.Persistence;` · `Auth/Services/*` → `…Auth.Services;`
- `Finanzas/Persistence/*` → `…Finanzas.Persistence;`
- `Investments/Persistence/*` → `…Investments.Persistence;`
- **⚠ `SharedKernel/Persistence/Migrations/*.cs`: NO cambies el namespace.** Conservan `namespace BigSchool.Infrastructure.Persistence.Migrations` (cambiarlo puede romper el snapshot/`__EFMigrationsHistory` y provocar migraciones espurias). Solo se mueven de carpeta; el `[DbContext(typeof(BigSchoolDbContext))]` resuelve por tipo.

> `BigSchoolDbContext` mantiene `ApplyConfigurationsFromAssembly(typeof(BigSchoolDbContext).Assembly)` **sin cambios**. Verifica que esa línea no se toca. Las configs referencian entidades de dominio (`using BigSchool.Domain.{Módulo}.Entities;`) — ajústalas.

- [ ] **Step 3: Compilar y resolver usings**

Run: BUILD. Reemplazos habituales:
- `using BigSchool.Infrastructure.Persistence;` (DbContext/factory/DateOnlyTypeHandler) → `…SharedKernel.Persistence;`
- `using BigSchool.Infrastructure.Persistence.Repositories;` (EFRepository base) → `…SharedKernel.Persistence;`
- `using BigSchool.Infrastructure.Services;` → exchange rate → `…SharedKernel.Services;`; hasher/jwt/encryptor → `…Auth.Services;`
- Configs/repos que usan entidades → `using BigSchool.Domain.{Módulo}.Entities;`
- `Program.cs`: `using BigSchool.Infrastructure.Persistence;` → `…SharedKernel.Persistence;`. `typeof(BigSchoolDbContext).Assembly` sigue válido.
- Tests de integración que instancian `BigSchoolDbContext`/repos (`MySqlDatabaseFixture.cs`, etc.) → ajusta a `…SharedKernel.Persistence`.

Repite hasta 0 errores.

- [ ] **Step 4: Verificar (+ sanidad de migraciones)**

Run: FULL. *Expected:* verde. Además:
Run (cwd `src/backend`): `dotnet ef migrations has-pending-model-changes --project src/BigSchool.Infrastructure --startup-project src/BigSchool.WebApi` → *Expected:* sin cambios pendientes (el refactor NO debe generar migraciones nuevas).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "refactor(backend): reubicar Infrastructure por módulo (configs/repos/servicios)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 5: Controllers por módulo

**Files (origen → carpeta destino):**
- `AuthController.cs` → `Controllers/Auth/`
- `TransactionsController.cs`, `CategoriesController.cs` → `Controllers/Finanzas/`
- `CompaniesController.cs`, `PortfoliosController.cs` → `Controllers/Investments/`

- [ ] **Step 1: Mover ficheros**

Run (cwd `src/backend/src/BigSchool.WebApi/Controllers`):
```bash
mkdir -p Auth Finanzas Investments
git mv AuthController.cs Auth/
git mv TransactionsController.cs CategoriesController.cs Finanzas/
git mv CompaniesController.cs PortfoliosController.cs Investments/
```

- [ ] **Step 2: Ajustar `namespace` = carpeta destino**

- `Controllers/Auth/AuthController.cs` → `namespace BigSchool.WebApi.Controllers.Auth;`
- `Controllers/Finanzas/*` → `namespace BigSchool.WebApi.Controllers.Finanzas;`
- `Controllers/Investments/*` → `namespace BigSchool.WebApi.Controllers.Investments;`

> Las rutas son por atributo (`[Route("api/v1/...")]`): cambiar namespace/carpeta **no altera ningún endpoint**. Ajusta los `using` de cada controller hacia los namespaces de módulo de Application que necesite.

- [ ] **Step 3: Compilar y resolver usings**

Run: BUILD. Ajusta `using`s de los controllers a los namespaces de Application (Commands/Queries/DTOs de módulo). Repite hasta 0 errores.

- [ ] **Step 4: Verificar (smoke de rutas vía E2E)**

Run: FULL. *Expected:* verde. Los E2E ejercitan los endpoints con las mismas rutas → si pasan, no hubo regresión de contrato.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "refactor(backend): reubicar controllers por módulo

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 6: IntegrationEvents — abstracciones + bus in-memory

Introduce los contratos de eventos de integración y el bus in-memory. **No hay consumidores aún** (se estrenan en Spec 007); esta tarea entrega el plumbing + su test unitario. TDD.

**Files:**
- Create: `src/BigSchool.Application/SharedKernel/IntegrationEvents/IIntegrationEvent.cs`
- Create: `src/BigSchool.Application/SharedKernel/IntegrationEvents/IIntegrationEventHandler.cs`
- Create: `src/BigSchool.Application/SharedKernel/IntegrationEvents/IIntegrationEventBus.cs`
- Create: `src/BigSchool.Application/SharedKernel/IntegrationEvents/Contracts/.gitkeep`
- Create: `src/BigSchool.Infrastructure/SharedKernel/IntegrationEvents/InMemoryIntegrationEventBus.cs`
- Test: `tests/BigSchool.Application.Tests/SharedKernel/InMemoryIntegrationEventBusTests.cs`

- [ ] **Step 1: Escribir las abstracciones**

`IIntegrationEvent.cs`:
```csharp
namespace BigSchool.Application.SharedKernel.IntegrationEvents;

/// <summary>Evento inter-módulo. Lleva solo primitivos + identidad/fecha. Nunca entidades de dominio.</summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTime OccurredOn { get; }
}
```

`IIntegrationEventHandler.cs`:
```csharp
namespace BigSchool.Application.SharedKernel.IntegrationEvents;

public interface IIntegrationEventHandler<in TEvent> where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken);
}
```

`IIntegrationEventBus.cs`:
```csharp
namespace BigSchool.Application.SharedKernel.IntegrationEvents;

public interface IIntegrationEventBus
{
    Task PublishAsync(IIntegrationEvent @event, CancellationToken cancellationToken);
}
```

Run: `mkdir -p src/BigSchool.Application/SharedKernel/IntegrationEvents/Contracts && : > src/BigSchool.Application/SharedKernel/IntegrationEvents/Contracts/.gitkeep`

- [ ] **Step 2: Escribir el test del bus (falla)**

`InMemoryIntegrationEventBusTests.cs`:
```csharp
using BigSchool.Application.SharedKernel.IntegrationEvents;
using BigSchool.Infrastructure.SharedKernel.IntegrationEvents;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BigSchool.Application.Tests.SharedKernel;

public class InMemoryIntegrationEventBusTests
{
    private sealed record TestEvent(Guid EventId, DateTime OccurredOn, string Payload) : IIntegrationEvent;

    private sealed class TestHandler : IIntegrationEventHandler<TestEvent>
    {
        public List<TestEvent> Received { get; } = new();
        public Task HandleAsync(TestEvent @event, CancellationToken ct)
        {
            Received.Add(@event);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task PublishAsync_invoca_el_handler_registrado_para_el_tipo_concreto()
    {
        var handler = new TestHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IIntegrationEventHandler<TestEvent>>(handler);
        var provider = services.BuildServiceProvider();
        var bus = new InMemoryIntegrationEventBus(provider);
        var evt = new TestEvent(Guid.NewGuid(), DateTime.UtcNow, "hola");

        await bus.PublishAsync(evt, CancellationToken.None);

        handler.Received.Should().ContainSingle().Which.Payload.Should().Be("hola");
    }

    [Fact]
    public async Task PublishAsync_sin_handlers_es_noop()
    {
        var provider = new ServiceCollection().BuildServiceProvider();
        var bus = new InMemoryIntegrationEventBus(provider);
        var evt = new TestEvent(Guid.NewGuid(), DateTime.UtcNow, "x");

        var act = async () => await bus.PublishAsync(evt, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
```

Run: `dotnet test tests/BigSchool.Application.Tests --filter InMemoryIntegrationEventBusTests` → *Expected:* FAIL (no existe `InMemoryIntegrationEventBus`).

- [ ] **Step 3: Implementar el bus in-memory**

`InMemoryIntegrationEventBus.cs`:
```csharp
using BigSchool.Application.SharedKernel.IntegrationEvents;
using Microsoft.Extensions.DependencyInjection;

namespace BigSchool.Infrastructure.SharedKernel.IntegrationEvents;

/// <summary>
/// Bus in-process que resuelve por reflexión los IIntegrationEventHandler&lt;T&gt; del tipo concreto
/// del evento y los invoca. Interfaz idéntica a la de un bus real (RabbitMQ/SB): el día de mañana
/// solo cambia esta implementación, ni publishers ni handlers.
/// </summary>
public sealed class InMemoryIntegrationEventBus : IIntegrationEventBus
{
    private readonly IServiceProvider _serviceProvider;

    public InMemoryIntegrationEventBus(IServiceProvider serviceProvider)
        => _serviceProvider = serviceProvider;

    public async Task PublishAsync(IIntegrationEvent @event, CancellationToken cancellationToken)
    {
        var handlerType = typeof(IIntegrationEventHandler<>).MakeGenericType(@event.GetType());
        var method = handlerType.GetMethod(nameof(IIntegrationEventHandler<IIntegrationEvent>.HandleAsync))!;

        foreach (var handler in _serviceProvider.GetServices(handlerType))
        {
            await (Task)method.Invoke(handler, new object[] { @event, cancellationToken })!;
        }
    }
}
```
> `IServiceProvider.GetServices(Type)` funciona con el adaptador de Autofac. Si no compila por falta de `GetServices`, añade el paquete `Microsoft.Extensions.DependencyInjection.Abstractions` a `BigSchool.Infrastructure.csproj` (suele venir transitivo vía `Autofac.Extensions.DependencyInjection`).

- [ ] **Step 4: Verificar el test**

Run: `dotnet test tests/BigSchool.Application.Tests --filter InMemoryIntegrationEventBusTests` → *Expected:* PASS (2 tests).

- [ ] **Step 5: Verificar suite**

Run: BUILD + UNIT. *Expected:* verde.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(backend): IIntegrationEventBus + InMemoryIntegrationEventBus (sin consumidores aún)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 7: Outbox transaccional + dispatcher post-commit

Persistencia atómica de eventos de integración (tabla `OutboxMessage` en la misma UoW que el agregado) + drenado post-commit vía un pipeline behavior de MediatR. TDD con test de integración.

**Files:**
- Create: `src/BigSchool.Infrastructure/SharedKernel/IntegrationEvents/OutboxMessage.cs`
- Create: `src/BigSchool.Infrastructure/SharedKernel/Persistence/OutboxMessageConfiguration.cs`
- Create: `src/BigSchool.Application/SharedKernel/IntegrationEvents/IIntegrationEventOutbox.cs`
- Create: `src/BigSchool.Application/SharedKernel/IntegrationEvents/IOutboxDispatcher.cs`
- Create: `src/BigSchool.Infrastructure/SharedKernel/IntegrationEvents/IntegrationEventOutbox.cs`
- Create: `src/BigSchool.Infrastructure/SharedKernel/IntegrationEvents/OutboxDispatcher.cs`
- Create: `src/BigSchool.Application/SharedKernel/Behaviors/OutboxDispatchBehavior.cs`
- Modify: `src/BigSchool.Infrastructure/SharedKernel/Persistence/BigSchoolDbContext.cs` (añadir `DbSet<OutboxMessage>`)
- Modify: `src/BigSchool.WebApi/Program.cs` (registrar el behavior)
- Modify: `tests/BigSchool.Integration.Tests/Fixtures/MySqlDatabaseFixture.cs` (limpiar `OutboxMessages` en `ResetAsync`)
- Migration: `AddOutboxMessage`
- Test: `tests/BigSchool.Integration.Tests/SharedKernel/OutboxTests.cs`

- [ ] **Step 1: Entidad `OutboxMessage` (infraestructura, no AR)**

`OutboxMessage.cs`:
```csharp
namespace BigSchool.Infrastructure.SharedKernel.IntegrationEvents;

/// <summary>
/// Fila del outbox. Infraestructura pura (no AR de negocio): sin IdStatus ni Global Query Filter,
/// igual que ExchangeRate. Se inserta en la MISMA transacción que el agregado.
/// </summary>
public class OutboxMessage
{
    public long IdOutboxMessage { get; private set; }
    public Guid EventId { get; private set; }
    public string Type { get; private set; } = default!;
    public string Payload { get; private set; } = default!;
    public DateTime OccurredOn { get; private set; }
    public DateTime? ProcessedOn { get; private set; }
    public string? Error { get; private set; }

    private OutboxMessage(Guid eventId, string type, string payload, DateTime occurredOn)
    {
        EventId = eventId;
        Type = type;
        Payload = payload;
        OccurredOn = occurredOn;
    }

    private OutboxMessage() { } // EF

    public static OutboxMessage Create(Guid eventId, string type, string payload, DateTime occurredOn)
        => new(eventId, type, payload, occurredOn);

    public void MarkProcessed(DateTime processedOn) => ProcessedOn = processedOn;
    public void MarkFailed(DateTime processedOn, string error)
    {
        ProcessedOn = processedOn;
        Error = error;
    }
}
```

- [ ] **Step 2: Configuración EF de la tabla**

`OutboxMessageConfiguration.cs`:
```csharp
using BigSchool.Infrastructure.SharedKernel.IntegrationEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BigSchool.Infrastructure.SharedKernel.Persistence;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(x => x.IdOutboxMessage);
        builder.Property(x => x.EventId).IsRequired();
        builder.HasIndex(x => x.EventId).IsUnique();
        builder.Property(x => x.Type).IsRequired().HasMaxLength(512);
        builder.Property(x => x.Payload).IsRequired().HasColumnType("json");
        builder.Property(x => x.OccurredOn).IsRequired();
        builder.Property(x => x.ProcessedOn);
        builder.Property(x => x.Error).HasMaxLength(2048);
        builder.HasIndex(x => x.ProcessedOn); // acelera el drenado de pendientes
    }
}
```

- [ ] **Step 3: Registrar el DbSet en el DbContext**

En `BigSchoolDbContext.cs`, junto a los demás `DbSet` (con `using BigSchool.Infrastructure.SharedKernel.IntegrationEvents;`):
```csharp
public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
```

- [ ] **Step 4: Abstracciones en Application (los handlers no dependen de EF)**

`IIntegrationEventOutbox.cs`:
```csharp
namespace BigSchool.Application.SharedKernel.IntegrationEvents;

/// <summary>
/// Lo usa un command handler para encolar un IntegrationEvent DENTRO de su misma UoW.
/// La serialización + inserción de la fila la hace la implementación de Infrastructure;
/// el SaveChanges del agregado confirma agregado + outbox atómicamente.
/// </summary>
public interface IIntegrationEventOutbox
{
    void Add(IIntegrationEvent @event);
}
```

`IOutboxDispatcher.cs`:
```csharp
namespace BigSchool.Application.SharedKernel.IntegrationEvents;

public interface IOutboxDispatcher
{
    Task DispatchPendingAsync(CancellationToken cancellationToken);
}
```

- [ ] **Step 5: Implementaciones en Infrastructure**

`IntegrationEventOutbox.cs`:
```csharp
using System.Text.Json;
using BigSchool.Application.SharedKernel.IntegrationEvents;
using BigSchool.Infrastructure.SharedKernel.Persistence;

namespace BigSchool.Infrastructure.SharedKernel.IntegrationEvents;

public sealed class IntegrationEventOutbox : IIntegrationEventOutbox
{
    private readonly BigSchoolDbContext _dbContext;

    public IntegrationEventOutbox(BigSchoolDbContext dbContext) => _dbContext = dbContext;

    public void Add(IIntegrationEvent @event)
    {
        var type = @event.GetType().AssemblyQualifiedName!;
        var payload = JsonSerializer.Serialize(@event, @event.GetType());
        _dbContext.OutboxMessages.Add(
            OutboxMessage.Create(@event.EventId, type, payload, @event.OccurredOn));
    }
}
```

`OutboxDispatcher.cs`:
```csharp
using System.Text.Json;
using BigSchool.Application.SharedKernel.IntegrationEvents;
using BigSchool.Infrastructure.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BigSchool.Infrastructure.SharedKernel.IntegrationEvents;

/// <summary>
/// Drena el outbox post-commit (en proceso, mismo request). Evolución futura sin tocar contratos:
/// un BackgroundService que haga polling de esta misma tabla con este mismo bus/handlers.
/// </summary>
public sealed class OutboxDispatcher : IOutboxDispatcher
{
    private readonly BigSchoolDbContext _dbContext;
    private readonly IIntegrationEventBus _bus;

    public OutboxDispatcher(BigSchoolDbContext dbContext, IIntegrationEventBus bus)
    {
        _dbContext = dbContext;
        _bus = bus;
    }

    public async Task DispatchPendingAsync(CancellationToken cancellationToken)
    {
        var pending = await _dbContext.OutboxMessages
            .Where(m => m.ProcessedOn == null)
            .OrderBy(m => m.IdOutboxMessage)
            .ToListAsync(cancellationToken);

        foreach (var message in pending)
        {
            try
            {
                var eventType = Type.GetType(message.Type)
                    ?? throw new InvalidOperationException($"No se pudo resolver el tipo de evento '{message.Type}'.");
                var @event = (IIntegrationEvent)JsonSerializer.Deserialize(message.Payload, eventType)!;
                await _bus.PublishAsync(@event, cancellationToken);
                message.MarkProcessed(DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                message.MarkFailed(DateTime.UtcNow, ex.Message);
            }
        }

        await _dbContext.SaveChangesAsync(dispatchEvents: false);
    }
}
```

- [ ] **Step 6: Behavior que dispara el drenado post-commit**

`OutboxDispatchBehavior.cs` (carpeta `Application/SharedKernel/Behaviors/`):
```csharp
using BigSchool.Application.SharedKernel.IntegrationEvents;
using MediatR;

namespace BigSchool.Application.SharedKernel.Behaviors;

/// <summary>
/// Tras completar un command (que ya hizo SaveChanges de agregado+outbox atómicamente),
/// drena el outbox en el mismo request. Registrado como pipeline behavior de MediatR.
/// </summary>
public sealed class OutboxDispatchBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IOutboxDispatcher _dispatcher;

    public OutboxDispatchBehavior(IOutboxDispatcher dispatcher) => _dispatcher = dispatcher;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var response = await next();
        await _dispatcher.DispatchPendingAsync(cancellationToken);
        return response;
    }
}
```
En `Program.cs`, junto al `AddOpenBehavior` existente del `ValidationBehavior`:
```csharp
cfg.AddOpenBehavior(typeof(BigSchool.Application.SharedKernel.Behaviors.OutboxDispatchBehavior<,>));
```
> El registro por-ensamblado de Autofac (vigente hasta la Tarea 8) resuelve `IIntegrationEventOutbox`, `IOutboxDispatcher`, `IIntegrationEventBus` por `AsImplementedInterfaces`. Verifica que las 3 implementaciones quedan registradas.

- [ ] **Step 7: Crear la migración EF**

Run (cwd `src/backend`): `dotnet ef migrations add AddOutboxMessage --project src/BigSchool.Infrastructure --startup-project src/BigSchool.WebApi`
*Expected:* migración nueva en `src/BigSchool.Infrastructure/SharedKernel/Persistence/Migrations/`. Revisa el `Up()`: crea `OutboxMessages` con índice único en `EventId` e índice en `ProcessedOn`.

> EF genera la migración con el namespace `BigSchool.Infrastructure.Persistence.Migrations` heredado del historial → **déjalo así** (coherente con la excepción de Migrations en la Tarea 4).

- [ ] **Step 8: Aislar los tests — limpiar `OutboxMessages` en `ResetAsync`**

En `MySqlDatabaseFixture.ResetAsync`, dentro del bloque `FOREIGN_KEY_CHECKS = 0`, añade:
```csharp
await conn.ExecuteAsync("DELETE FROM OutboxMessages;");
```

- [ ] **Step 9: Test de integración (atomicidad + drenado + idempotencia)**

`OutboxTests.cs`:
```csharp
using BigSchool.Application.SharedKernel.IntegrationEvents;
using BigSchool.Infrastructure.SharedKernel.IntegrationEvents;
using BigSchool.Infrastructure.SharedKernel.Persistence;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace BigSchool.Integration.Tests.SharedKernel;

[Collection(nameof(IntegrationCollection))]
public class OutboxTests
{
    private readonly MySqlDatabaseFixture _db;
    public OutboxTests(MySqlDatabaseFixture db) => _db = db;

    private sealed record DummyEvent(Guid EventId, DateTime OccurredOn) : IIntegrationEvent;

    private BigSchoolDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<BigSchoolDbContext>()
            .UseMySql(_db.ConnectionString, ServerVersion.AutoDetect(_db.ConnectionString))
            .Options;
        return new BigSchoolDbContext(options, Mock.Of<IMediator>());
    }

    [Fact]
    public async Task Outbox_persiste_la_fila_en_el_mismo_SaveChanges()
    {
        await _db.ResetAsync();
        await using var ctx = NewContext();
        new IntegrationEventOutbox(ctx).Add(new DummyEvent(Guid.NewGuid(), DateTime.UtcNow));
        await ctx.SaveChangesAsync(dispatchEvents: false);

        var stored = await ctx.OutboxMessages.SingleAsync();
        stored.ProcessedOn.Should().BeNull();
    }

    [Fact]
    public async Task Dispatcher_drena_publica_una_vez_y_marca_ProcessedOn()
    {
        await _db.ResetAsync();
        var evt = new DummyEvent(Guid.NewGuid(), DateTime.UtcNow);
        await using (var ctx = NewContext())
        {
            new IntegrationEventOutbox(ctx).Add(evt);
            await ctx.SaveChangesAsync(dispatchEvents: false);
        }

        var busMock = new Mock<IIntegrationEventBus>();
        await using (var ctx = NewContext())
            await new OutboxDispatcher(ctx, busMock.Object).DispatchPendingAsync(CancellationToken.None);

        busMock.Verify(b => b.PublishAsync(
            It.Is<IIntegrationEvent>(e => e.EventId == evt.EventId), It.IsAny<CancellationToken>()),
            Times.Once);

        await using (var ctx = NewContext())
            (await ctx.OutboxMessages.SingleAsync()).ProcessedOn.Should().NotBeNull();
    }

    [Fact]
    public async Task Dispatcher_no_reprocesa_filas_ya_marcadas()
    {
        await _db.ResetAsync();
        await using (var ctx = NewContext())
        {
            new IntegrationEventOutbox(ctx).Add(new DummyEvent(Guid.NewGuid(), DateTime.UtcNow));
            await ctx.SaveChangesAsync(dispatchEvents: false);
        }
        var busMock = new Mock<IIntegrationEventBus>();
        await using (var ctx = NewContext())
            await new OutboxDispatcher(ctx, busMock.Object).DispatchPendingAsync(CancellationToken.None);
        await using (var ctx = NewContext())
            await new OutboxDispatcher(ctx, busMock.Object).DispatchPendingAsync(CancellationToken.None);

        busMock.Verify(b => b.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

Run: `dotnet test tests/BigSchool.Integration.Tests --filter OutboxTests` → *Expected:* PASS (3 tests). El fixture recrea `bigschool_test` aplicando **todas** las migraciones (incluida `AddOutboxMessage`), así que la tabla existe.

- [ ] **Step 10: Verificar suite completa**

Run: FULL. *Expected:* verde. Los E2E existentes siguen pasando: `OutboxDispatchBehavior` drena 0 filas cuando no hay eventos → sin efecto observable.

- [ ] **Step 11: Commit**

```bash
git add -A
git commit -m "feat(backend): outbox transaccional + dispatcher post-commit para IntegrationEvents

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 8: DI Autofac por módulo

Sustituye el registro por-ensamblado-de-capa de `Program.cs` por **un módulo Autofac por módulo funcional**. Comportamiento de registro equivalente (mismos tipos, mismas interfaces), pero alineado al módulo. El filtro por `Namespace.StartsWith("BigSchool.{Capa}.{Módulo}")` captura también los sub-namespaces (`.Commands`, `.Persistence`, …).

**Files:**
- Create: `src/BigSchool.Infrastructure/SharedKernel/DI/SharedKernelModule.cs`
- Create: `src/BigSchool.Infrastructure/Auth/DI/AuthModule.cs`
- Create: `src/BigSchool.Infrastructure/Finanzas/DI/FinanzasModule.cs`
- Create: `src/BigSchool.Infrastructure/Investments/DI/InvestmentsModule.cs`
- Modify: `src/BigSchool.WebApi/Program.cs`

- [ ] **Step 1: `SharedKernelModule`**

`SharedKernelModule.cs`:
```csharp
using Autofac;
using BigSchool.Application.SharedKernel.Common;
using BigSchool.Domain.SharedKernel.Interfaces;
using BigSchool.Infrastructure.SharedKernel.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Module = Autofac.Module;

namespace BigSchool.Infrastructure.SharedKernel.DI;

public sealed class SharedKernelModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        // Application.SharedKernel.* (behaviors, bus abstractions consumers, exchange rate provider, outbox, etc.)
        builder.RegisterAssemblyTypes(typeof(ApiResponse).Assembly)
            .Where(t => t.Namespace is not null && t.Namespace.StartsWith("BigSchool.Application.SharedKernel"))
            .AsImplementedInterfaces();

        // Infrastructure.SharedKernel.* (DbConnectionFactory, ExchangeRateApiClient, InMemoryIntegrationEventBus,
        // IntegrationEventOutbox, OutboxDispatcher, etc.)
        builder.RegisterAssemblyTypes(typeof(BigSchoolDbContext).Assembly)
            .Where(t => t.Namespace is not null && t.Namespace.StartsWith("BigSchool.Infrastructure.SharedKernel"))
            .AsImplementedInterfaces();

        // DbContext como sí-mismo y como IUnitOfWork (scoped)
        builder.Register(ctx =>
        {
            var optionsBuilder = new DbContextOptionsBuilder<BigSchoolDbContext>();
            var configuration = ctx.Resolve<Microsoft.Extensions.Configuration.IConfiguration>();
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            optionsBuilder.UseMySql(connectionString!, ServerVersion.AutoDetect(connectionString!));
            var mediator = ctx.Resolve<IMediator>();
            return new BigSchoolDbContext(optionsBuilder.Options, mediator);
        })
        .AsSelf()
        .As<IUnitOfWork>()
        .InstancePerLifetimeScope();
    }
}
```
> `IUnitOfWork` vive en `BigSchool.Domain.SharedKernel.Interfaces` (Tarea 1).

- [ ] **Step 2: `AuthModule`, `FinanzasModule`, `InvestmentsModule`**

`AuthModule.cs`:
```csharp
using Autofac;
using BigSchool.Application.Auth.Interfaces;
using BigSchool.Infrastructure.Auth.Persistence;
using Module = Autofac.Module;

namespace BigSchool.Infrastructure.Auth.DI;

public sealed class AuthModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterAssemblyTypes(typeof(IUserRepository).Assembly)
            .Where(t => t.Namespace is not null && t.Namespace.StartsWith("BigSchool.Application.Auth"))
            .AsImplementedInterfaces();

        builder.RegisterAssemblyTypes(typeof(UserRepository).Assembly)
            .Where(t => t.Namespace is not null && t.Namespace.StartsWith("BigSchool.Infrastructure.Auth"))
            .AsImplementedInterfaces();
    }
}
```

`FinanzasModule.cs`:
```csharp
using Autofac;
using BigSchool.Application.Finanzas.Interfaces;
using BigSchool.Infrastructure.Finanzas.Persistence;
using Module = Autofac.Module;

namespace BigSchool.Infrastructure.Finanzas.DI;

public sealed class FinanzasModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterAssemblyTypes(typeof(ITransactionRepository).Assembly)
            .Where(t => t.Namespace is not null && t.Namespace.StartsWith("BigSchool.Application.Finanzas"))
            .AsImplementedInterfaces();

        builder.RegisterAssemblyTypes(typeof(TransactionRepository).Assembly)
            .Where(t => t.Namespace is not null && t.Namespace.StartsWith("BigSchool.Infrastructure.Finanzas"))
            .AsImplementedInterfaces();
    }
}
```

`InvestmentsModule.cs`:
```csharp
using Autofac;
using BigSchool.Application.Investments.Interfaces;
using BigSchool.Infrastructure.Investments.Persistence;
using Module = Autofac.Module;

namespace BigSchool.Infrastructure.Investments.DI;

public sealed class InvestmentsModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterAssemblyTypes(typeof(ICompanyRepository).Assembly)
            .Where(t => t.Namespace is not null && t.Namespace.StartsWith("BigSchool.Application.Investments"))
            .AsImplementedInterfaces();

        builder.RegisterAssemblyTypes(typeof(CompanyRepository).Assembly)
            .Where(t => t.Namespace is not null && t.Namespace.StartsWith("BigSchool.Infrastructure.Investments"))
            .AsImplementedInterfaces();
    }
}
```
> Los "tipos ancla" (`typeof(X).Assembly`) solo localizan el ensamblado; ajusta a un tipo que exista realmente en cada módulo tras las Tareas 3–4.

- [ ] **Step 3: Componer los módulos en `Program.cs`**

Sustituye el bloque `ConfigureContainer<ContainerBuilder>` (los 4 `RegisterAssemblyTypes` por capa + el `Register` del DbContext) por:
```csharp
builder.Host.ConfigureContainer<ContainerBuilder>(containerBuilder =>
{
    containerBuilder.RegisterModule<BigSchool.Infrastructure.SharedKernel.DI.SharedKernelModule>();
    containerBuilder.RegisterModule<BigSchool.Infrastructure.Auth.DI.AuthModule>();
    containerBuilder.RegisterModule<BigSchool.Infrastructure.Finanzas.DI.FinanzasModule>();
    containerBuilder.RegisterModule<BigSchool.Infrastructure.Investments.DI.InvestmentsModule>();

    // WebApi layer (filtros/servicios propios de la capa web; los controllers los descubre MVC)
    containerBuilder.RegisterAssemblyTypes(typeof(Program).Assembly).AsImplementedInterfaces();
});
```
> El registro original de "servicios de dominio" (filtro `!Entities/!Enums/!Exceptions`) queda cubierto: el dominio ya no tiene servicios con interfaces que registrar (son entidades POCO). **Prueba de fuego: la suite E2E** (Step 4) — si un handler/repo no resolviera, los endpoints darían 500.

Mantén intacto el resto de `Program.cs` (MediatR con `ValidationBehavior` + `OutboxDispatchBehavior`, Swagger, Dapper handler, JWT, CORS, DataProtection, etc.). Ajusta `using`s a los namespaces nuevos.

- [ ] **Step 4: Verificar (la DI se prueba end-to-end)**

Run: FULL. *Expected:* verde. Los tests de integración E2E validan la DI real: register/login (Auth), POST/GET transactions (Finanzas), companies/portfolios (Investments) deben responder igual.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "refactor(backend): DI Autofac por módulo (SharedKernel/Auth/Finanzas/Investments)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 9: Guard tests de arquitectura (NetArchTest)

Nuevo proyecto de tests que falla si las fronteras entre módulos se erosionan. `ResideInNamespace`/`HaveDependencyOnAny` hacen match por prefijo → cubren los sub-namespaces (`.Entities`, `.Commands`, …).

**Files:**
- Create: `tests/BigSchool.Architecture.Tests/BigSchool.Architecture.Tests.csproj`
- Create: `tests/BigSchool.Architecture.Tests/ModuleBoundaryTests.cs`
- Create: `tests/BigSchool.Architecture.Tests/LayerDependencyTests.cs`
- Modify: `Backend.slnx`

- [ ] **Step 1: Crear el proyecto**

Run (cwd `src/backend`):
```bash
dotnet new xunit -o tests/BigSchool.Architecture.Tests
dotnet add tests/BigSchool.Architecture.Tests package NetArchTest.Rules
dotnet add tests/BigSchool.Architecture.Tests package FluentAssertions
dotnet add tests/BigSchool.Architecture.Tests reference src/BigSchool.Domain src/BigSchool.Application src/BigSchool.Infrastructure src/BigSchool.WebApi
dotnet sln Backend.slnx add tests/BigSchool.Architecture.Tests
```
*(Si `dotnet sln … add` no soporta `.slnx` en tu SDK, añade a mano en `Backend.slnx` una entrada `<Project Path="tests/BigSchool.Architecture.Tests/BigSchool.Architecture.Tests.csproj" />`.)*

- [ ] **Step 2: Test de fronteras entre módulos**

`ModuleBoundaryTests.cs`:
```csharp
using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace BigSchool.Architecture.Tests;

public class ModuleBoundaryTests
{
    private static readonly Assembly Domain = typeof(BigSchool.Domain.SharedKernel.Entities.BaseEntity).Assembly;
    private static readonly Assembly Application = typeof(BigSchool.Application.SharedKernel.Common.ApiResponse).Assembly;

    [Theory]
    [InlineData("BigSchool.Domain.Auth", new[] { "BigSchool.Domain.Finanzas", "BigSchool.Domain.Investments" })]
    [InlineData("BigSchool.Domain.Finanzas", new[] { "BigSchool.Domain.Auth", "BigSchool.Domain.Investments" })]
    [InlineData("BigSchool.Domain.Investments", new[] { "BigSchool.Domain.Auth", "BigSchool.Domain.Finanzas" })]
    public void Modulos_de_dominio_no_dependen_entre_si(string moduleNs, string[] forbidden)
    {
        var result = Types.InAssembly(Domain)
            .That().ResideInNamespace(moduleNs)
            .ShouldNot().HaveDependencyOnAny(forbidden)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"{moduleNs} no debe depender de otros módulos de negocio. Fallos: {Describe(result)}");
    }

    [Theory]
    [InlineData("BigSchool.Application.Auth", new[] { "BigSchool.Application.Finanzas", "BigSchool.Application.Investments" })]
    [InlineData("BigSchool.Application.Finanzas", new[] { "BigSchool.Application.Auth", "BigSchool.Application.Investments" })]
    [InlineData("BigSchool.Application.Investments", new[] { "BigSchool.Application.Auth", "BigSchool.Application.Finanzas" })]
    public void Modulos_de_application_no_dependen_entre_si(string moduleNs, string[] forbidden)
    {
        var result = Types.InAssembly(Application)
            .That().ResideInNamespace(moduleNs)
            .ShouldNot().HaveDependencyOnAny(forbidden)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"{moduleNs} no debe depender de otros módulos de negocio. Fallos: {Describe(result)}");
    }

    private static string Describe(TestResult result)
        => result.FailingTypeNames is null ? "-" : string.Join(", ", result.FailingTypeNames);
}
```

- [ ] **Step 3: Test de dependencias de capa**

`LayerDependencyTests.cs`:
```csharp
using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace BigSchool.Architecture.Tests;

public class LayerDependencyTests
{
    private static readonly Assembly Domain = typeof(BigSchool.Domain.SharedKernel.Entities.BaseEntity).Assembly;

    private static string Describe(TestResult r)
        => r.FailingTypeNames is null ? "-" : string.Join(", ", r.FailingTypeNames);

    [Fact]
    public void Domain_no_depende_de_Infrastructure_ni_EF()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot().HaveDependencyOnAny("BigSchool.Infrastructure", "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue($"Domain debe ser POCO. Fallos: {Describe(result)}");
    }

    [Fact]
    public void Domain_no_depende_de_Application()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot().HaveDependencyOn("BigSchool.Application")
            .GetResult();

        result.IsSuccessful.Should().BeTrue($"Domain no debe conocer Application. Fallos: {Describe(result)}");
    }
}
```

- [ ] **Step 4: Ejecutar los guard tests**

Run: `dotnet test tests/BigSchool.Architecture.Tests` → *Expected:* PASS. Si alguno **falla**, hay una fuga de frontera real (p. ej. un tipo de Finanzas usa uno de Investments): **arréglala** (mueve el tipo o pásalo por SharedKernel), no relajes el test.

> El test "el publisher no referencia el módulo consumidor" (spec §12) y las reglas sobre `Notifications` se **añaden en Spec 007**, cuando ese módulo y el primer IntegrationEvent existan.

- [ ] **Step 5: Verificar suite completa (con ARCH)**

Run: BUILD + UNIT + ARCH + INTEGRATION. *Expected:* todo verde.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "test(backend): guard tests de arquitectura (NetArchTest) para fronteras de módulo

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Verificación final (Definition of Done — spec 005 §12)

- [ ] `dotnet build` limpio y **toda la suite existente en verde** (unit + integración E2E) → sin cambio de comportamiento.
- [ ] `BigSchool.Architecture.Tests` en verde (fronteras respetadas).
- [ ] Test del `InMemoryIntegrationEventBus` (publica→invoca handler; sin handler→no-op) — Tarea 6.
- [ ] Tests de outbox: fila persistida atómicamente; dispatcher drena, marca `ProcessedOn`, publica exactamente una vez; no reprocesa — Tarea 7.
- [ ] Smoke E2E: `/health` 200 y endpoints existentes responden igual (mismos contratos/códigos) — lo cubren los E2E existentes.
- [ ] Sin regresión de contrato: los tipos del frontend no cambian (esta spec no toca la API pública).
- [ ] `dotnet ef migrations has-pending-model-changes` sin pendientes salvo `AddOutboxMessage` (ya aplicada).

**Nota de secuencia (spec §7):** aquí `SubCategory` solo se **reubica** a `BigSchool.Domain.Finanzas.Entities`. Su **re-modelación como AR independiente** y su CRUD (#8/C) se ejecutan en la **Spec 009**. No re-modeles `SubCategory` en este plan.

---

## Self-Review (cobertura de la spec 005)

| Sección spec 005 | Tarea(s) |
|---|---|
| §4 Mapa de módulos + SharedKernel | 1, 2, 3, 4, 5 |
| §5 Layout objetivo (namespace = carpeta, subcarpetas por categoría) | 1–5 (regla universal) |
| §6.1 Contratos IntegrationEvents | 6 |
| §6.2 Bus in-memory | 6 |
| §6.3 Outbox transaccional + dispatcher post-commit | 7 |
| §6.4 Idempotencia (EventId único + no reproceso) | 7 (índice único + test de no-reproceso) |
| §7 Categorías→Finanzas (solo reubicación) | 2 (`SubCategory`→`Domain.Finanzas.Entities`) + nota |
| §8 DI Autofac por módulo | 8 |
| §9 Guard tests (NetArchTest) | 9 |
| §10 Migración por pasos, verde por commit | estructura de todas las tareas |
| §12 Verificación | "Verificación final" |
