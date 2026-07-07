# BigSchool-TFM — Diario del Proceso

Registro cronológico del desarrollo del proyecto siguiendo un ciclo ligero:
**Diseñar → Analizar → Implementar → Revisar**

---

## 2026-06-06 — Inicio del Proyecto

### Fase: Diseño

**Actividades realizadas:**
- Definición de la visión y alcance del proyecto
- Selección del stack tecnológico completo
- Decisiones de arquitectura documentadas (ADR)
- Creación de estructura de monorepo
- Redacción de AGENTS.md (raíz + por módulo)
- Creación del README.md

**Decisiones clave:**
- Monorepo para facilitar entrega del TFM
- RAG en Python/FastAPI separado del backend .NET
- Qdrant como vector store
- Azure OpenAI como LLM provider
- Next.js + Vitest + Playwright para frontend web
- React Native Expo para mobile (solo lectura)
- TypeScript para MCP Server
- Seeds con datos fake de 2-3 meses

**Siguiente paso:**
- [x] Inicializar Git
- [x] Comenzar diseño detallado del backend (.NET 8)

---

## 2026-06-06 — Diseño detallado del Backend

### Fase: Diseño

**Módulo**: backend

**Actividades realizadas:**
- Diseño completo del modelo de datos (Users, Transactions, Categories, SubCategories, Companies, Portfolios, Holdings, Valuations, RagDocuments)
- Convenciones de datos: nomenclatura de IDs (`Id{Tabla}`), borrado lógico con `IdStatus` (Enum), auditoría (`CreatedAt`/`UpdatedAt`), seguridad (Argon2)
- Definición de Bounded Contexts: Finanzas Personales, Inversiones, IA/RAG
- Estructura CQRS detallada con ejemplos de Commands/Queries
- Convenciones de API REST: versionado, envelope pattern, paginación

**Resultado / Estado:**
- Documento `docs/02-backend-design.md` aprobado
- Commit: `5378412`

---

## 2026-06-06 — Infraestructura base Docker Compose

### Fase: Diseño

**Módulo**: infra

**Actividades realizadas:**
- Docker Compose base con servicios MySQL 8 y Qdrant
- Servicios de aplicación (backend, rag, frontend, mcp) definidos pero comentados (pendientes de Dockerfile)
- Docker Compose override para desarrollo local
- `.env.example` con todas las variables de entorno documentadas
- AGENTS.md de infraestructura con convenciones, puertos y estructura k8s

**Resultado / Estado:**
- Infraestructura mínima lista para levantar BD y vector store
- Commit: `c9644df` (HEAD en develop)

---

## 2026-06-07 — Revisión de estado y mejora de referencias

### Fase: Revisión

**Módulo**: docs

**Actividades realizadas:**
- Revisión del estado global del proyecto
- Identificación de problema: los AGENTS.md de cada módulo no referencian los documentos de diseño consolidados en `docs/`
- Solución: añadir sección "Documentación de referencia" al AGENTS.md raíz que enlace a los diseños aprobados

**Estado actual del proyecto:**
- ✅ Visión y alcance definidos (`docs/00-vision.md`)
- ✅ ADRs de arquitectura (`docs/01-arquitectura.md`)
- ✅ Diseño detallado del backend (`docs/02-backend-design.md`)
- ✅ Estructura de monorepo creada con AGENTS.md por módulo
- ✅ Docker Compose base (MySQL + Qdrant)
- ✅ Scaffolding del backend (Fase 1 completada)
- ⬜ Implementación del backend — BC Finanzas Personales (siguiente)
- ⬜ Diseño del frontend web
- ⬜ Diseño del RAG service
- ⬜ Diseño del MCP server
- ⬜ Diseño del mobile

**Siguiente paso:**
- [x] Añadir sección de documentación de referencia al AGENTS.md raíz
- [x] Comenzar implementación del backend: solución .NET, capas Domain y Application

---

## 2026-06-07 — Scaffolding completo del backend .NET 8

### Fase: Implementación

**Módulo**: backend

**Actividades realizadas:**
- Plan detallado de scaffolding con 8 tareas incrementales (`docs/superpowers/plans/2026-06-07-backend-scaffolding.md`)
- Flujo de trabajo: `develop` → `feature/backend-scaffolding-taskN` → PR → revisión humana → merge
- **Task 1** (PR #1): Solución .NET 8 con 4 proyectos fuente + 3 de tests (formato `.slnx` de SDK 10)
- **Task 2** (PR #2): `Directory.Build.props` con versiones centralizadas (AutoMapper 15.1.3 por vulnerabilidad en 13/14)
- **Task 3** (PR #3): Capa Domain — `BaseEntity`, `IAggregateRoot`, `IUnitOfWork`, `IDomainEvent`, Enums, 3 tests unitarios
- **Task 4** (PR #4): Capa Application — `IRepository<T,Y>`, repos específicos, `AppSettings`, `DomainEventNotification`, `IDbConnectionFactory`, `IRagServiceClient`
- **Task 5** (PR #5): Capa Infrastructure — `EFRepository<T,Y>` abstracto, `BigSchoolDbContext` con dispatch condicional de eventos, `DbConnectionMySqlFactory`
- **Task 6** (PR #6): Capa WebApi — `Program.cs` con Autofac simplificado, `CustomMediatR` (SyncContinueOnException), Serilog (file + console), Swagger, Health Check, CORS
- **Task 7** (PR #7): `.editorconfig` con convenciones C#
- **Task 8** (PR #8): Verificación quickstart — MySQL Docker, `/health` OK, Swagger OK, Serilog file sink OK

**Decisiones clave tomadas durante la implementación:**
- `IUnitOfWork` en Domain, `IRepository<T,Y>` en Application (Domain permanece persistence-agnostic)
- `SaveChangesAsync(bool dispatchEvents = true)` sin `CancellationToken` → evita recursión infinita con overrides de EF Core
- `IAggregateRoot` como marcador — solo Aggregate Roots tienen repositorio
- `CustomMediatR` en Application/Infrastructure (no WebApi) — handlers pueden elegir estrategia de publicación
- Autofac simplificado: un `RegisterAssemblyTypes` por capa en `Program.cs`
- Sin `appsettings.Development.json` — usar `dotnet user-secrets` para overrides locales

**Resultado / Estado:**
- Backend scaffolding 100% completado (8/8 tareas, PRs #1-#8)
- Build: 0 errores, 3 tests unitarios pasando
- API arrancable con `dotnet run`, verificada con MySQL Docker

**Siguiente paso:**
- [ ] Fase 2: Implementación BC Finanzas Personales (entidades completas, CQRS, endpoints)

---

## 2026-06-08 al 2026-06-10 — Autenticación Backend Completa (Tasks 1-10)

### Fase: Implementación

**Módulo**: backend

**Actividades realizadas:**
- Plan detallado de autenticación crosscutting en 10 tareas (`docs/superpowers/plans/2026-06-08-backend-crosscutting-auth.md`)
- Flujo de trabajo: `develop` → `feature/backend-CR-auth-plan1-taskN` → PR → revisión humana → merge
- **Task 1** (PR #10): Entidad `User` completa con factory `Create`, `UpdateLastLogin`, colección `SubCategories`, 4 tests unitarios
- **Task 2** (PR #11): Entidad `SubCategory` (hija de User) con constructor `private`, factory `internal`, `AddSubCategory()` en User, invariante de unicidad, 5 tests
- **Task 3** (PR #13): Excepciones de dominio tipadas — `DomainException` base (abstract), `ConflictException`, `NotFoundException`, `UnauthorizedException`, `InvalidCredentialsDomainException`, `ApiError` RFC 7807
- **Task 4** (PR #14): Envelope `ApiResponse<T>`, `ExceptionHandlingMiddleware` con mapeo de excepciones a HTTP codes, `NotificationExceptionBehavior` para proteger handlers de DomainEvents
- **Task 5** (PR #17): EF Core Configurations (UserConfiguration, SubCategoryConfiguration), Seed Data (28 subcategorías predefinidas), Migración `InitialCreate`, Global Query Filter para IdStatus != Deleted
- **Task 6** (PR #18): Auth Services — `Argon2PasswordHasher` (Parallelism=2, MemoryCost=65536), `JwtService` (userId encriptado con DPAPI), `DataProtectionUserIdEncryptor`, 8 tests pasando
- **Task 7** (PR #19): Commands Auth — `RegisterCommand`, `LoginCommand`, `EmailAlreadyExistsDomainException`, `AuthResponseDto`, extensión de `IUserRepository`, 5 tests pasando
- **Task 8** (PR #20): `AuthController` (POST /api/v1/auth/register y /login), `UserRepository` con normalización de emails, JWT Authentication + DataProtection en `Program.cs`
- **Task 9** (PR #21): `ValidationBehavior` para pipeline MediatR, validación paralela con FluentValidation, 3 tests pasando (TDD: Red → Green)
- **Task 10** (PR #22): Tests de validators (6 para Register, 4 para Login), verificación build completo (0 errores), documentación de verificación end-to-end

**Decisiones clave tomadas durante la implementación:**
- **Constantes en UPPERCASE snake_case** (ej: `MEMORY_COST`, `PURPOSE`) — convención consolidada durante implementación
- **Entidad User como Aggregate Root** — SubCategory es entidad hija accesible solo vía `user.AddSubCategory()`
- **Constructor protected + factory Create** — Patrón para todas las entidades (EF Core usa protected parameterless, lógica usa factory)
- **Argon2id** con parámetros: Parallelism=2, MemoryCost=65536 KB, Iterations=3, HashLength=16, SaltLength=32
- **DPAPI para userId** — `DataProtection` API de ASP.NET Core encripta userId antes de incluirlo en JWT 'sub' claim (frontend nunca ve INTs internos)
- **ValidationBehavior con Task.WhenAll** — ejecuta múltiples validators en paralelo para mejor rendimiento
- **Normalización de email** — `.Trim().ToLowerInvariant()` en UserRepository para búsquedas case-insensitive
- **DomainException específicas** — Cada error tiene su excepción tipada (ej: `EmailAlreadyExistsDomainException` hereda de `ConflictException` 409)
- **ApiError RFC 7807** — Errores como objetos `{ code, message, field }` para que frontend haga switch/case sin parsear strings
- **NotificationExceptionBehavior** — Envuelve handlers de DomainEvents con try-catch para que una excepción no reviente toda la petición
- **Global Query Filter** — `IdStatus != Deleted` en todas las Configurations → soft delete transparente
- **FluentValidation TestHelper** — `TestValidate()` y `ShouldHaveValidationErrorFor()` para tests declarativos

**Problemas encontrados y soluciones:**
- **Argon2 API** — No usar método estático `Argon2.Hash()`, usar `new Argon2(config)` con propiedades `Threads` (no `parallelism`), `MemoryCost`, `TimeCost` (not `iterations`)
- **Autofac + EF Core** — Excluir Domain entities del contenedor DI (tienen constructores protegidos para EF Core), filtrar con `.Where(t => !t.Namespace!.Contains("Entities"))`
- **Dependency conflicts** — Actualizar JWT packages de 8.0.2 a 8.14.0 para resolver conflictos con IdentityModel.Tokens
- **Missing using Xunit** — Los tests de ValidationBehavior necesitaban `using Xunit;` explícito
- **ApiResponse envelope** — Decidir si es `<T>` o no genérico para errors → solución: `ApiResponse` (sin T) para errores, `ApiResponse<T>` para datos

**Resultado / Estado:**
- Autenticación backend 100% completada (10/10 tareas, PRs #10, #11, #13, #14, #17-#22)
- Build: 0 errores, warnings solo de versiones EF Core (Pomelo.EntityFrameworkCore.MySql 8.0.2 vs 8.0.11)
- Tests: 19+ tests pasando (6 validator + 4 validator + 2 handler + 3 handler + 3 behavior + 4 services + 9 entities)
- Migración EF Core lista: `InitialCreate` con Users y SubCategories + 28 subcategorías seed
- API lista para pruebas end-to-end con MySQL
- ExceptionMiddleware capturando y mapeando errores a Problem Details RFC 7807
- ValidationBehavior integrado en pipeline MediatR para validación automática

**Cobertura de Tests:**
- 4 tests: `User` entity (Create, UpdateLastLogin, validaciones)
- 5 tests: `SubCategory` + `User.AddSubCategory()` (unicidad, validaciones)
- 6 tests: `RegisterCommandValidator` (email, password, fullName)
- 4 tests: `LoginCommandValidator` (email, password)
- 2 tests: `RegisterCommandHandler` (success, duplicate email)
- 3 tests: `LoginCommandHandler` (valid, wrong password, non-existent user)
- 3 tests: `ValidationBehavior` (valid, invalid, no validators)
- 4 tests: `Argon2PasswordHasher` + `JwtService` (hash, verify, generate, extract)

**Siguiente paso:**
- [ ] Fase 3: Implementación BC Finanzas Personales — entidades Transaction, Category con lógica de negocio completa
- [ ] Prueba end-to-end manual: MySQL Docker + migrations + curl a /register y /login para verificar flujo completo

---

## 2026-06-15 — Endpoint de refresh de token JWT

### Fase: Implementación

**Módulo**: backend

**Actividades realizadas:**
- Implementación del endpoint pendiente `POST /api/v1/auth/refresh` documentado en `docs/02-backend-design.md`
- Plan: `docs/superpowers/plans/2026-06-15-backend-auth-refresh-endpoint.md` (4 tareas, PRs #23–#25)
- **Task 1** (PR #23): `RefreshTokenCommand` + `RefreshTokenCommandValidator` (FluentValidation)
- **Task 2** (PR #24): `RefreshTokenCommandHandler` con TDD (Red → Green); 3 tests nuevos
- **Task 3** (PR #25): `AuthController.Refresh()` con `[Authorize]` + esquema Bearer en Swagger UI

**Decisiones clave:**
- **Estrategia elegida: re-emisión desde access token válido** — El middleware `JwtBearer` (ya configurado con `ValidateLifetime = true`) valida la firma y expiración antes de entrar al handler; sin refresh token separado, sin tabla, sin migración
- **Sin `UpdateLastLogin` ni `SaveChanges`** — un refresh no es un login; el handler solo lee y re-emite
- **Global Query Filter como guarda** — `GetByIdAsync` devuelve `null` para usuarios con `IdStatus = Deleted`; handler lanza `UnauthorizedAccessException` → middleware mapea a `401`
- **Bearer en Swagger** — `AddSecurityDefinition` + `AddSecurityRequirement` globales; todos los endpoints muestran el candado y el botón "Authorize"
- **Limitación conocida y aceptada** — sin rotación ni revocación; si el access token expira el usuario debe volver a hacer login. Para revocación real se necesitaría un `RefreshToken` persistido (entidad + tabla + migración)

**Resultado / Estado:**
- Endpoint `POST /api/v1/auth/refresh` operativo
- Build: 0 errores
- Tests: 3 tests nuevos pasando (flujo feliz, token sin sub, usuario borrado/inexistente)
- Verificación E2E manual completada: flujo register → login → refresh → nuevo token correcto; sin token devuelve `401`
- Swagger UI con botón "Authorize" y soporte Bearer para todos los endpoints

**Cobertura de tests añadida:**
- `Handle_ValidToken_ReturnsNewToken`
- `Handle_TokenWithoutUserId_ThrowsUnauthorized`
- `Handle_UserNotFoundOrDeleted_ThrowsUnauthorized`

**Siguiente paso:**
- [ ] Implementación BC Finanzas Personales — entidades Transaction, Category con lógica de negocio completa

---

## 2026-06-18 al 2026-06-20 — Cimientos Multimoneda (Plan 2A, Tasks 1-9)

### Fase: Implementación

**Módulo**: backend

**Actividades realizadas:**
- Plan detallado de cimientos multimoneda en 9 tareas (`docs/superpowers/plans/2026-06-18-backend-multicurrency-foundations.md`), task-by-task con PR + revisión humana por tarea.
- **Task 1** (PR #29): enum `Currency : short` (ISO 4217 — EUR/USD/GBP/CHF/JPY; nombre = alpha-3, valor = numérico) + 9 tests.
- **Task 2** (PR #30): VO `Money` (record sellado, factory `Create` con redondeo bancario a 2 decimales) + 6 tests.
- **Task 3** (PR #31): VO `MoneyConversion` (snapshot Original + Rate + Base + RateDate, factory que calcula `Base` e impone invariante misma-moneda→rate=1) + 4 tests.
- **Task 4** (PR #32): `CurrencyConverter.CharIso` compartido (Currency↔CHAR(3)), `Users.BaseCurrency` (default EUR, factory retrocompatible), migración `AlterUsersAddBaseCurrency`.
- **Task 5** (PR #33): entidad plana `ExchangeRate` (reference data, sin AR/BaseEntity), `ExchangeRateConfiguration`, migración `CreateExchangeRates` con índice único `(From, To, RateDate)` y seed de 4 pares fijos.
- **Task 6** (PR #34): `IExchangeRateProvider`, `ExchangeRateSettings`, registro `AddHttpClient` + sección `appsettings.json` (Frankfurter).
- **Task 7** (PR #35): `ExchangeRateApiClient` (anti-corruption: atajo from==to, cache Dapper hit/miss, UPSERT idempotente, fallback al último tipo, proveedor Frankfurter) + unit test del atajo.
- **Task 8** (PR #36): tests de integración sobre el MySQL real de docker-compose — fixture `MySqlDatabaseFixture` + `IntegrationCollection` (un solo crear/migrar/destruir de `bigschool_test` por ejecución, serial, `ResetAsync` entre tests) verificando seed y ciclo cache miss→WireMock→hit.
- **Task 9** (este PR): cierre documental — ADR-006, diseño backend, diario; **nueva convención SQL/Dapper** en AGENTS.md y su anexo/absorción en planes.

**Decisiones clave tomadas durante la implementación:**
- **Conversión por snapshot, dominio puro**: `MoneyConversion` guarda el tipo aplicado; el `rate` lo resuelve Application (`IExchangeRateProvider`), el dominio nunca llama a servicios externos.
- **`ExchangeRates` como reference data**: EF posee solo el esquema; el cliente lee/escribe con Dapper (UPSERT) desacoplado del `UnitOfWork` de negocio.
- **Estrategia de integración reusando docker-compose** en vez de Testcontainers: BD dedicada `bigschool_test` migrada por EF (no `bigschool`, cuyas tablas las crea `init.sql` y chocarían), con `ICollectionFixture` de ciclo único y ejecución serial. WireMock.Net fakea el servidor HTTP de Frankfurter (ejercita toda la capa anti-corrupción HTTP, lo que Moq sobre la interfaz no cubre).
- **Convención SQL/Dapper consolidada**: SQL como `const` UPPERCASE `_QUERY` a nivel de clase + `DynamicParameters`. Documentada en AGENTS.md; se aplica en Plan 2B desde su origen y deja como deuda registrada el refactor de `ExchangeRateApiClient` (Anexo Plan 2A → tarea en Plan 2B).
- **`HasDefaultValueSql("'EUR'")`** en `Users.BaseCurrency` para evitar el warning de sentinel value de EF con enums persistidos como string.

**Problemas encontrados y soluciones:**
- **Credenciales del fixture**: el default usaba `root/root`; el `.env` de compose define `MYSQL_ROOT_PASSWORD=bigschool_root`. Corregido el fallback (configurable por `BIGSCHOOL_TEST_MYSQL`).
- **`Microsoft.Extensions.Http` no transitivo** en class libraries: `IHttpClientFactory` no resolvía en Infrastructure → añadido el paquete explícito + versión centralizada en `Directory.Build.props`.
- **Interpolación de JSON en raw string** (`$$"""..."""`) daba CS9007 por las `}}` finales del JSON → el body del fake WireMock se construye con `JsonSerializer.Serialize(new { ... })`, más limpio y sin ambigüedad de compilador.

**Resultado / Estado:**
- Plan 2A completado (9/9 tareas, PRs #29-#36). Build 0 errores; unit tests Domain/Application en verde; 2 tests de integración PASS contra MySQL de docker-compose.
- Cimientos multimoneda listos y reutilizables tal cual por el BC Transaction (Plan 2B) y por Inversiones (Plan 3).

**Siguiente paso:**
- [ ] Plan 2B — BC Finanzas Personales: agregado `Transaction` con `MoneyConversion`, CQRS completo y endpoints. Incluye, como tarea final, el refactor de `ExchangeRateApiClient` a la convención SQL/Dapper.

---

## 2026-06-20 — BC Finanzas Personales completo (Plan 2B, Tasks 1-10)

### Fase: Implementación

**Módulo**: backend

**Actividades realizadas:**
- Plan detallado en 10 (+1 refactor) tareas (`docs/superpowers/plans/2026-06-18-backend-finanzas-transactions.md`), task-by-task con PR + revisión humana por tarea.
- **Task 1** (PR #38): Agregado `Transaction` (AR) con `MoneyConversion` owned type — factory `Create` con invariantes (userId > 0, amount > 0, rate > 0), métodos `Update` y `Delete` (soft), `TransactionCreatedEvent(Transaction)`. Convenciones de dominio añadidas a `AGENTS.md`.
- **Task 2** (PR #39): `TransactionConfiguration` (EF) — `OwnsOne(MoneyConversion)`, FK `IdUser` CASCADE, FK `IdSubCategory` SET NULL, índice `(IdUser, TransactionDate)`, Global Query Filter `IdStatus != Deleted`. Migración `CreateTransactions`.
- **Task 3** (PR #40): `ITransactionRepository` + `TransactionRepository` (EFRepository). Commands Create/Update/Delete con handlers, validadores FluentValidation y `TransactionDto`. Lógica de ownership (not-found en vez de forbidden). 12 tests unitarios.
- **Task 4** (PR #41): `GetTransactionByIdQuery` + `GetTransactionsQuery` (paginación, `QueryMultipleAsync` COUNT+SELECT en un roundtrip, `PagedResult<T>`). `TransactionListItemDto` con `string` para monedas (Dapper no convierte CHAR(3) a enum). 4 tests.
- **Task 5** (PR #42): `GetTransactionSummaryQuery` (COALESCE SUM CASE por tipo) + `GetMonthlyChartQuery` (GROUP BY YEAR/MONTH). DTOs `TransactionSummaryDto` y `MonthlyChartPointDto`. 4 tests.
- **Task 6** (PR #43): `GetCategoriesQuery` — SQL a `SubCategories` con `IdUser IS NULL OR IdUser = @IdUser`; agrupación en memoria via `Enum.GetValues<MainCategory>()`. DTOs `CategoryDto` / `SubCategoryDto`. 2 tests.
- **Task 7** (PR #44): DI — registro de `TransactionRepository` en módulo Autofac (ya auto-scan). `ApiResponse.Fail()` sin parámetros añadido.
- **Task 8** (PR #45): `TransactionsController` (7 endpoints) + `CategoriesController` (GET) + helper `CurrentUser.GetId` (extrae `IdUser` del claim `sub` cifrado DPAPI vía `IUserIdEncryptor.Decrypt`; `UnauthorizedAccessException` → 401 por middleware).
- **Task 9** (PR #46): Tests E2E con `BigSchoolWebAppFactory` (`WebApplicationFactory<Program>`) sobre `bigschool_test`. JWT real (mismo secreto que `JwtBearer`), sin mocks de auth. Happy path: POST crear ingreso → GET summary (TotalIncome=1500, BaseCurrency="EUR") + verificación física en MySQL con Dapper. 401 auténtico sin token. `ResetAsync` ampliado.
- **Task 10** (este PR): Documentación — tabla `Transactions` corregida y completada, enums actualizados a valores reales, endpoints alineados con la implementación, nota CQRS multimoneda añadida, diario actualizado.

**Decisiones clave tomadas durante la implementación:**
- **`TransactionListItemDto` usa `string` para monedas**: Dapper no convierte CHAR(3) a enum automáticamente. `TransactionDto` (built from domain objects) sí usa `Currency` tipado.
- **Ownership check en Update/Delete**: `transaction.IdUser != request.IdUser` → `NotFoundException` (not 403), evita revelar existencia del recurso a usuarios no autorizados.
- **`QueryMultipleAsync`** en `GetTransactions`: COUNT + SELECT paginado en un único roundtrip; la query WHERE compartida como `const string TRANSACTIONS_WHERE` se concatena en compile-time.
- **`currency` opcional en Create/Update**: si es null, se usa `user.BaseCurrency` con `rate = 1` (sin red). Esto permite tests E2E sin acoplarse a un servidor de tipos de cambio.
- **`BigSchoolWebAppFactory` sin mocks de auth**: usa el `IJwtService` real del contenedor; el 401 es real, no simulado.
- **`public partial class Program { }`** ya estaba en `Program.cs` (estándar ASP.NET Core para `WebApplicationFactory`).

**Problemas encontrados y soluciones:**
- **`MoneyConversion` constructor binding en EF**: `MoneyConversion` es un `sealed record` con `Money Original` y `Money Base` como parámetros del constructor primario. EF no puede inyectar owned navigations por constructor → añadido `private MoneyConversion() : this(null!, 0m, null!, default) { }` (EF usa el ctor sin parámetros y luego asigna propiedades por init setters).
- **`MainCategory` enum en inglés** (implementación) vs nombres en español (diseño original): corrección en `AGENTS.md` y `02-backend-design.md`.
- **`DomainEvent` con entidad completa**: convención establecida — el record recibe la entidad de dominio completa (puntero), no campos individuales. Documentado en `AGENTS.md` y en el plan.
- **Constructor private + factory**: revertido de object initializer a patrón `protected ctor() + private ctor(all fields) + static Create`. Documentado en `AGENTS.md`.

**Resultado / Estado:**
- Plan 2B completado (Tasks 1-10, PRs #38-#46 + este PR). Build 0 errores.
- Tests: 22+ unitarios (Domain + Application) + 4 de integración PASS (2 de Plan 2A + 2 nuevos E2E).
- Endpoints implementados: `/api/v1/transactions` (7 endpoints: POST, PUT/{id}, DELETE/{id}, GET/{id}, GET, GET/summary, GET/monthly-chart) y `/api/v1/categories` (GET).
- BC Finanzas Personales operativo end-to-end: registro/login JWT → CRUD transacciones multimoneda → queries consolidadas en moneda base.
- **Task 11 pendiente** (registrada en el plan): refactor de `ExchangeRateApiClient` a convención SQL/Dapper (`const _QUERY` + `DynamicParameters`).

**Cobertura de tests añadida (Plan 2B):**
- 4 tests: `Transaction` entity (Create con invariantes, Update, Delete, re-conversión)
- 4 tests: Commands Create/Update/Delete handlers (happy path, NotFoundException)
- 4 tests: `GetTransactions` + `GetTransactionById` (filtros, paginación, not-found)
- 4 tests: `GetTransactionSummary` + `GetMonthlyChart` (agregación, agrupación)
- 2 tests: `GetCategories` (subcategorías globales + de usuario)
- 2 tests E2E: POST crear → GET summary → verificación física MySQL + 401 sin token

**Task 11** (PR independiente, cierre de deuda Plan 2A Anexo A): `ExchangeRateApiClient` refactorizado a la convención SQL/Dapper — SQL extraído a `private const string READCACHE_QUERY / READLASTKNOWN_QUERY / UPSERTCACHE_QUERY` a nivel de clase (UPPERCASE), parámetros migrados de objeto anónimo a `DynamicParameters`. Sin cambio de comportamiento; test de integración cache miss→hit sigue en verde.

**Siguiente paso:**
- [ ] Plan 3: BC Inversiones (Portfolio, Holding, Company, Valuation)

---

## 2026-06-21 — Tests de integración E2E (Auth + Transactions) y normas en AGENTS.md (Plan 008)

### Fase: Implementación

**Módulo**: backend

**Actividades realizadas:**
- Plan detallado en 15 tareas (`docs/superpowers/plans/008-2026-06-20-backend-integration-tests-e2e.md`), ejecutado con **Subagent-Driven Development** (un subagente fresco por tarea) en una única rama `feature/backend-integration-tests-e2e-task1-15` → **PR #50**.
- Motivación: solo existía 1 test E2E del summary; faltaban los de **AuthController** (punto crítico: hashing Argon2 y JWT reales) y la cobertura por endpoint de Transactions. Se detectaron en sesiones previas dos bugs de integración (serialización) que un E2E sencillo habría cazado.
- **Bases compartidas** (Tasks 1, 2, 6): `IntegrationTestBase` (ciclo de vida del factory + reset de BD + tipos de deserialización del envelope `ApiResponse<T>`), `AuthEndpointTestBase` (helpers de registro y BD) y `TransactionEndpointTestBase` (siembra de usuario/tasas, `AuthenticatedClient` con JWT real, verificación física con Dapper).
- **Auth E2E** (Tasks 3-5, prioritario): `RegisterTests` (round-trip Argon2 real + persistencia de hash + 409 + 400), `LoginTests` (token usable contra endpoint `[Authorize]` + 400 credenciales/validación), `RefreshTests` (token válido + 401 sin token + 401 usuario eliminado).
- **Transactions E2E** (Tasks 7-13): `PostTransactionTests` (exhaustivo: cada campo + conversión USD→EUR + persistencia + 401/400), `GetTransactionByIdTests`, `GetTransactionsTests` (lista/paginación/filtros), `PutTransactionTests`, `DeleteTransactionTests` (soft-delete + oculto en lecturas), `GetTransactionSummaryTests` (migra y elimina el monolítico `TransactionsEndpointTests.cs`), `GetMonthlyChartTests`.
- **Normas fijadas** (Task 14): sección `### Testing` de `src/backend/AGENTS.md` ampliada con la **Definition of Done de tests E2E por endpoint** (un fichero por endpoint, ≥1 test exhaustivo con request real + cada campo de response + persistencia física, foco en hashing/JWT reales, validación de 401/400/404/409, soft-delete, determinismo multimoneda).

**Decisiones clave / Problemas encontrados:**
- **Un único branch/PR para las 15 tareas** (en vez de el flujo habitual task-por-PR): la naturaleza homogénea y acumulativa de los tests lo justifica; el nombre de rama `*-task1-15` lo refleja.
- **Subagent-Driven con dos puertas de revisión** por tarea (spec + calidad) sobre el plan, sin pausa entre tareas.
- **Fix de serialización previos consolidados** (de PR #49, ya en develop): `JsonStringEnumConverter` (acepta `"USD"`) y `DateOnlyTypeHandler` (Dapper mapea `DATE`→`DateOnly`); los E2E los blindan.
- **Bloqueo de DLLs por Visual Studio** durante algunos builds (`devenv.exe` con la app corriendo): los subagentes compilaron/ejecutaron en `-c Release` para evitar el lock del directorio Debug; problema de entorno, no de código.

**Resultado / Estado:**
- Plan 008 completado (15/15 tareas). **37/37 tests de integración PASS**: Auth (Register 5 + Login 4 + Refresh 3 = 12) + Transactions (Post 5 + GetById 3 + GetList 5 + Put 3 + Delete 3 + Summary 2 + MonthlyChart 2 = 23) + 2 existentes de Persistence/Services.
- `AuthController` cubierto E2E con hashing Argon2 y JWT reales (sin mocks); valor que los unitarios mockeados no aportan.
- Reglas de testing E2E vinculantes en `AGENTS.md` para futuras implementaciones.

**Siguiente paso:**
- [ ] Plan 3: BC Inversiones — diseño aprobado (`docs/superpowers/specs/002-2026-06-21-backend-inversiones-design.md`); pendiente de planes 3A (Company + Valuation) y 3B (Portfolio + Holding + Disposal + FIFO + performance).

---

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
- Plan 3A completado. Suite de integración en verde (37 previos + 20 nuevos = 57 total). Catálogo de inversiones operativo end-to-end.

**Siguiente paso:**
- [ ] Plan 3B — Portfolio + Holding + Disposal + ventas FIFO + performance (consume el catálogo de 3A).

---

## 2026-06-21 — BC Inversiones Plan 3B: Carteras (Portfolio/Holding/Disposal, FIFO)

### Fase: Implementación

**Módulo**: backend

**Actividades realizadas:**
- Plan `docs/superpowers/plans/011-2026-06-21-backend-inversiones-plan-3b-portfolios.md` (16 tareas), segundo plan del BC Inversiones, consumes el catálogo de 3A.
- **Dominio** (Tasks 1-5): agregado `Portfolio` (AR) → `Holding` (lote de compra) → `Disposal` (venta). `AddHolding` (snapshot `AvgBuyPrice` = MoneyConversion congelado a `BuyDate`), `SellShares` **FIFO** a nivel (Portfolio, Company) generando N `Disposal`, `RealizedPnL` consolidado y persistido en `Portfolio`, `DeleteHolding` con soft-delete en cascada sobre disposals y reversa del realizado acumulado. `InsufficientSharesDomainException` (→ 400). `Holding`/`Disposal` con constructores `internal`; sin `InternalsVisibleTo` — toda la lógica se testea desde `PortfolioTests` (caja negra de AR).
- **Persistencia** (Tasks 6-7): `PortfolioConfiguration` / `HoldingConfiguration` / `DisposalConfiguration` (owned `MoneyConversion`/`Money`, shadow FKs, Global Query Filter `IdStatus != Deleted`, `UsePropertyAccessMode.Field` para colecciones backing), migración `CreatePortfolios`, `PortfolioRepository.GetByIdWithHoldingsAsync` (carga Holdings + Disposals).
- **CQRS** (Tasks 8-12): commands `CreatePortfolio` / `AddHolding` / `UpdateHolding` / `DeleteHolding` / `SellShares` (con `IExchangeRateProvider` resuelto en Application); queries Dapper `GetPortfolios` / `GetPortfolioById` / `GetPortfolioPerformance` (consolidación en moneda base con fallback de tipo — fragment `PortfolioSqlFragments.HOLDING_VALUATION`). `PortfoliosController` (8 endpoints `[Authorize]`).
- **Tests** (Tasks 13-15): `ResetAsync` ampliado (Disposals → Holdings → Portfolios preservando catálogo seed); `PortfolioEndpointTestBase` con helpers CRUD y deserializadores; 48 tests E2E — `PostPortfolioTests` (3), `PostHoldingTests` (5), `GetPortfoliosTests` (2), `GetPortfolioByIdTests` (3), `PostSaleTests` (5 — FIFO multimoneda, efecto FX, 400/401/404), `GetPerformanceTests` (4 — no realizado + ciclo completo), `PutHoldingTests` (3), `DeleteHoldingTests` (3 — soft-delete + reversa realizado).
- **Documentación** (Task 16): `02-backend-design.md` (tablas Portfolios/Holdings/Disposals, endpoints), `01-arquitectura.md` (ADR-007), `diario.md`.

**Decisiones / Problemas encontrados:**
- **Bug de Pomelo MySQL con nested owned entities compartidas en batch INSERT**: `Portfolio.SellShares` creaba un único objeto `Money sellPrice` reutilizado como `Original` de todos los `MoneyConversion` del loop FIFO. EF Core rastrea owned entities por referencia; al compartir la instancia entre disposals distintos, el batch INSERT omitía `SellOriginalAmount` en la segunda fila (error: `Field 'SellOriginalAmount' doesn't have a default value`). Fix: `Money.Create(sellPrice.Amount, sellPrice.Currency)` fresco por iteración. El test que lo detectó fue `Sell_FifoAcrossLots_GeneratesNDisposals_AndConsolidatesRealized` (escenario: 2 lotes distintos, venta cruzando ambos).
- **`MoneyConversion` constructor binding en EF**: owned record con `Money Original` y `Money Base` en el constructor primario — EF no puede inyectar owned navigations anidadas por ctor → añadido `private MoneyConversion() : this(null!, 0m, null!, default) { }` (patrón idéntico al aplicado en Plan 2B para `Transaction`).
- **Catálogo preservado en `ResetAsync`**: `Companies` 1-4 y `Valuations` 1-8 son seed determinista consumido por los E2E de inversiones → se borran solo las filas con `IdCompany > 4` / `IdValuation > 8`. Orden de borrado: `Disposals → Holdings → Portfolios` (respeta FK constraints).
- **`IExchangeRateProvider` resuelto en Application, no en el dominio**: el handler `SellSharesCommandHandler` y `AddHoldingCommandHandler` resuelven el tipo a la fecha exacta y lo pasan al AR; el dominio recibe `rate` ya calculado. Los tests E2E siembran la tasa a la fecha exacta de la operación.

**Resultado / Estado:**
- Plan 3B completado (16/16 tareas, PRs #71-#81). Build 0 errores.
- **48/48 tests E2E de Investments PASS** contra MySQL real (docker-compose).
- BC Inversiones operativo end-to-end: registro/login → crear cartera → añadir lotes multimoneda → venta FIFO cruzando lotes → RealizedPnL consolidado → performance realizada/no realizada en moneda base.
- ADR-007 documentado; modelo de datos actualizado en `02-backend-design.md`.

**Siguiente paso:**
- [ ] Integración frontend del BC Inversiones (Plan 4).

---

## 2026-06-23 — Cambio de enfoque: reorientación a MVP

### Fase: Diseño

**Módulo**: docs / arquitectura general

**Actividades realizadas:**
- Revisión de alcance del TFM y actualización de documentación de visión y arquitectura (`README.md`, `docs/00-vision.md`, `docs/01-arquitectura.md` con ADR-008, `docs/02-backend-design.md`).

**Decisiones / Problemas encontrados:**
- **Blocker de Azure**: la suscripción no permite crear recursos de IA → el RAG con embeddings/LLM en Azure no es ejecutable ahora.
- **Replanteamiento del MCP**: de widget para ChatGPT (TypeScript) a MCP integrado en Python, como herramienta de flujos de análisis (screener / criterios / revisión de cartera) a futuro.
- **Prioridad MVP**: con el Backend completo (Finanzas + Inversiones) se entrega ya el Frontend-Web con una demo de IA mínima (chat + subida de documentos) vía Backend hacia un LLM de pago externo.

**Resultado / Estado:**
- Entrega del TFM reorientada a MVP (Backend + Frontend-Web + IA mínima externa). RAG completo, MCP (Python) y Mobile reclasificados como trabajo futuro, documentados como roadmap.

**Siguiente paso:**
- [ ] Conversación de diseño en detalle de la pieza **Frontend-Web** (solo diseño, abierta a preguntas e ideas).

---

## 2026-06-23 al 2026-06-24 — Inicio del Frontend-Web: diseño, v0 y arranque

### Fase: Diseño → Implementación

**Módulo**: frontend-web

**Actividades realizadas:**
- Sesión de brainstorming sobre tecnología de generación de UI (v0, shadcn blocks, código); estrategia en 3 capas (v0 para diseño+Landing, shadcn para "muebles", código para lógica).
- Generación con **v0 Max** del sistema de diseño (tokens HSL light+dark, paleta fintech) y la Landing pública completa con gráficas fake en Recharts. Integración en `src/frontend-web`; compilación y validación manual.
- Spec `003-2026-06-24-frontend-web-mvp-design.md`: cadencia pasos pequeños + gate, JWT en localStorage+Bearer, TDD unit tests por tarea + E2E en hitos, backend diferido para Emails y Profile.
- Plan `012-2026-06-24-frontend-web-mvp.md`: 16 tareas task-by-task, autosuficiente para el modelo ejecutor (Sonnet).
- **Task 1 (PR #88)**: fundamentos del proyecto — deps runtime + test, Vitest + Playwright configurados, componentes shadcn (14), providers QueryClient+Toaster, `.env.local`, `apiClient` con TDD, tipos TS de DTOs backend **verificados contra el código fuente** (rutas `/api/v1`, enums como nombre string, categorías anidadas, auth sin refresh token), y política de refresco en cliente `refreshPolicy` con TDD (máx. 5 refrescos o 24 h). 15 tests verdes, build limpio.

**Decisiones clave:**
- v0 solo para diseño+Landing (una tirada, modelo Max); shadcn + código para el resto.
- JWT en `localStorage` + `Bearer`; TDD con Vitest+RTL en cada tarea.
- Corrección `components.json`: aliases `@src/...` → `@/...` para que la CLI de shadcn genere imports resolvibles por el tsconfig.
- Reparto de modelos: Opus para specs/planes, Sonnet para implementar, Haiku para repetitivo.

**Resultado / Estado:**
- Landing arrancando y validada (PR #85 merged).
- Spec 003 + Plan 012 (PR #86 merged).
- Task 1 Fundamentos (PR #87 pendiente de revisión).

**Siguiente paso:**
- [ ] Merge PR #87 y arrancar **Task 2** (auth: tokenStore + AuthProvider + useAuth).

---

## 2026-06-24 al 2026-06-28 — Frontend-Web MVP completo (Tasks 1-16)

### Fase: Implementación

**Módulo**: frontend-web

**Actividades realizadas:**
- Plan `docs/superpowers/plans/012-2026-06-24-frontend-web-mvp.md` (16 tareas), spec `003-2026-06-24-frontend-web-mvp-design.md`. Rama-por-tarea, PRs #87–#103.
- **Task 1 (PR #87)**: Fundamentos — Next.js 15, Vitest + RTL, Playwright, 14 componentes shadcn/Base UI, providers QueryClient + Toaster, `apiClient` con interceptor de refresh, tipos TS verificados contra el backend. 15 tests.
- **Task 2 (PR #88)**: Auth — `tokenStore`, `AuthProvider`, `useAuth`, formularios Login/Registro (React Hook Form + Zod), integración JWT real. 17 tests.
- **Task 3 (PR #89)**: Fundamentos privados — `PrivateLayout` con `AppNavbar`, `ProfileDropdown`, redirección protegida. E2E hito: flujos de auth (8 specs).
- **Task 4 (PR #90)**: Dashboard — `useDashboard` (TanStack Query), resumen mensual, `MonthlyChart` (Recharts), skeleton de carga. 12 tests.
- **Task 5 (PR #91)**: Gastos/Ingresos — listado con filtros (tipo, categoría, búsqueda), paginación, estados vacío/cargando/error. 18 tests.
- **Task 6 (PR #92)**: CRUD Transacciones — Sheet lateral, formularios alta/edición/borrado, validación Zod. E2E hito: crear gasto.
- **Task 7 (PR #93)**: Categorías — `useCategoriesQuery`, `CategorySelect` agrupado por MainCategory. 9 tests.
- **Task 8 (PR #94)**: `formatAmount` multimoneda (Intl.NumberFormat), `formatDate`. 11 tests.
- **Task 9 (PR #95)**: Inversiones listado — `usePortfolios`, cards de carteras, modal de creación. 14 tests.
- **Task 10 (PR #96)**: Detalle de cartera — `usePortfolioDetail`, `HoldingCard`, modales de holding y venta FIFO. E2E hito: vender holding.
- **Task 11 (PR #97)**: Performance de cartera — `PortfolioPerformance`, métricas realizadas/no realizadas. 8 tests.
- **Task 12 (PR #98)**: AI Scanner — feature-flag `NEXT_PUBLIC_AI_SCANNER_ENABLED`, chat demo, `RagPanel`, settings de API keys (localStorage). 21 tests.
- **Task 13 (PR #99)**: Contacto — formulario integrado en landing (sección CTA), listado privado `/contacts`. 13 tests.
- **Task 14 (PR #100)**: Emails — `useEmails`, listado `/emails` con Badge bienvenida/contacto. Contrato backend documentado. 7 tests.
- **Task 15 (PR #102)**: Perfil — `profileSchema` (contraseña opcional con `superRefine`), página `/profile` con info read-only + formulario. 8 tests.
- **Task 16 (este PR)**: Cierre — página pública `/scope` (alcance MVP + roadmap), enlace en navbar y footer, entrada en diario.

**Decisiones clave:**
- **JWT en localStorage + Bearer** — sencillo para demo TFM; el `apiClient` gestiona refresh automático.
- **TDD task-by-task** — ciclo RED → GREEN obligatorio; 225+ tests unitarios en verde.
- **Regla de huecos de backend** — `TODO (deuda técnica backend)` + contrato inline; datos demo en localStorage hasta que exista el endpoint.
- **`asChild` no soportado** en `Button` de `@base-ui/react` — `<Link>` dentro de `<Button>` como solución.
- **`superRefine` en Zod v4** — usar `ZodIssueCode.custom` para validaciones cruzadas (`too_small` shape cambió, requiere `origin`).
- **ProfileDropdown como hub** de páginas privadas secundarias (Contacto, Emails, Editar perfil).

**Problemas encontrados y soluciones:**
- **Strict mode en E2E** — landing tiene CTAs duplicados; acotar con `page.getByRole('navigation')`.
- **`getByText` con múltiples matches** en AI Scanner — usar `getByRole('heading', ...)`.
- **`.env.local` gitignoreado** — solo se commitea `.env.example` como plantilla.
- **Formulario de contacto** — evolucionó: página separada → integrado en CTA de landing, grid 2 columnas.

**E2E smoke tests (requieren backend en http://localhost:5285):**
- `tests/e2e/login.spec.ts` — 8 specs de auth.
- `tests/e2e/create-expense.spec.ts` — crear gasto y verificar en lista.
- `tests/e2e/sell-holding.spec.ts` — añadir holding + venta FIFO.

**Resultado / Estado:**
- Frontend-Web MVP 100% completado (16/16 tareas).
- **225 tests unitarios PASS · 0 errores TypeScript · build de producción limpio.**
- Deuda técnica localizable con `TODO (deuda técnica backend)` en ficheros afectados.
- Roadmap público en `/scope`.

**Siguiente paso:**
- [ ] Memoria académica del TFM y presentación.
- [ ] (Opcional) Endpoints backend pendientes: GET/PUT /users/me, POST /contact, GET /emails.
- [ ] (Opcional) RAG real cuando se disponga de suscripción Azure OpenAI.

---

## 2026-06-28 — Bloque 0: Dockerfiles base backend + frontend (Plan 013, Tasks 1-3)

### Fase: Implementación

**Módulo**: infra

**Actividades realizadas:**
- Spec `docs/superpowers/specs/004-2026-06-28-infra-e2e-seed-azure-design.md` y plan `docs/superpowers/plans/013-2026-06-28-bloque0-dockerfiles.md` ya aprobados; sesión dedicada a la ejecución.
- **Task 1 (PR #105)**: `infra/docker/backend.Dockerfile` multi-stage `sdk:8.0 → aspnet:8.0`. Restore cacheado copiando primero los `.csproj` + `Directory.Build.props`. Stage runtime instala `curl` (no incluido en `aspnet:8.0`) para el `HEALTHCHECK`. Escucha en `8081` (`ASPNETCORE_URLS`). Smoke test verificado: `/health` → `Healthy`. También: `src/backend/.dockerignore`.
- **Task 2 (PR #106)**: `output: 'standalone'` en `src/frontend-web/next.config.mjs`. Build local verificado: `pnpm build` OK y `.next/standalone/server.js` presente. Incidencia: requiere **Developer Mode de Windows** (crea symlinks de pnpm sin permisos de admin).
- **Task 3 (PR #107)**: `infra/docker/frontend-web.Dockerfile` (4 stages: base → deps → build → runner) + `src/frontend-web/.dockerignore`. pnpm@11.5.2 fijado via corepack. Smoke test verificado: landing → HTTP 200.

**Decisiones / Problemas encontrados:**
- **pnpm 11 + build scripts**: pnpm 11 bloquea por defecto todos los `postinstall` scripts; lanza `ERR_PNPM_IGNORED_BUILDS`. La solución es copiar `pnpm-workspace.yaml` (que ya tenía `allowBuilds: sharp: true`) y `.npmrc` al stage `deps` del Dockerfile. Ni `--ignore-scripts` (rompe el binario de Turbopack) ni `neverBuiltDependencies` (el error persiste) funcionan solos.
- **Turbopack en Docker/Linux**: Next.js 16 usa Turbopack por defecto en `next build`. El lockfile generado en Windows no incluye el binario de Turbopack para Linux; en Docker la resolución de `@vercel/turbopack-next/internal/font/google/font` falla. Fix: `pnpm exec next build --webpack` en el Dockerfile.
- **Google Fonts + proxy SSL corporativo**: con webpack, `next/font/google` descarga Geist de Google Fonts durante el build; el proxy corporativo presenta un certificado auto-firmado → `SELF_SIGNED_CERT_IN_CHAIN`. Fix: `ENV NODE_TLS_REJECT_UNAUTHORIZED=0` solo en el stage `build` (no llega al `runner`). En CI (Azure/GitHub sin proxy) esto no es necesario pero es inofensivo.
- **Directorio `public/` ausente**: el proyecto no tiene carpeta `public/`; el stage `runner` intentaba `COPY --from=build /app/public` y fallaba. Fix: `mkdir -p /app/public` antes del build.
- **smoke test del backend**: `--retry-connrefused` de curl no reintenta en `Empty reply` (la app cierra la conexión los ~100 ms de inicialización). Con `sleep 3` previo es consistente.

**Resultado / Estado:**
- Plan 013 completado (3/3 tareas, PRs #105-#107 pendientes de revisión).
- Imágenes verificadas localmente: `bigschool-backend:dev` (→ `Healthy` en `:8081/health`) y `bigschool-frontend:dev` (→ `200` en `:3001`).
- Base reutilizable para Bloque 1 (compose E2E), Bloque 3 (compose completo) y Bloques 4/5 (CI/deploy Azure).

**Siguiente paso:**
- [ ] Merge PRs #105, #106, #107.
- [ ] Bloque 1: `docker-compose.e2e.yml` (backend + frontend + MySQL, smoke test E2E).

---

## 2026-06-29 — Bloque 1: E2E dockerizado y aislado (Plan 014, Tasks 1-4)

### Fase: Implementación

**Módulo**: infra / frontend-web (tests)

**Actividades realizadas:**
- Spec `docs/superpowers/specs/004-2026-06-28-infra-e2e-seed-azure-design.md` (Bloque 1) y plan `docs/superpowers/plans/014-2026-06-29-bloque1-e2e-dockerizado.md`; sesión dedicada a la ejecución tarea a tarea.
- **Task 1 (PR #109)**: `infra/docker-compose.e2e.yml` — compose efímero que levanta solo `backend` (`:8081`) y `frontend-web` (`:3001`); el MySQL dev (`bigschool-mysql`) se alcanza desde el backend por `host.docker.internal:3306` + `extra_hosts: host-gateway`. La BD de target es `bigschool_e2e` (no toca `bigschool` ni `bigschool_test`).
- **Task 2 (PR #110)**: `global-setup.ts` y `global-teardown.ts` de Playwright. El setup: verifica que `bigschool-mysql` corre, DROP+CREATE `bigschool_e2e` con `init.sql` reescrito (`USE \`bigschool\`` → `USE \`bigschool_e2e\``), levanta el compose, polling hasta salud. El teardown: `compose down`, `DROP DATABASE bigschool_e2e`.
- **Task 3 (PR #111)**: dos configs de Playwright — `playwright.config.ts` reescrito como config dockerizada por defecto (sin `webServer`, `globalSetup`/`globalTeardown`, `baseURL: http://localhost:3001`); `playwright.local.config.ts` creado como config de debug local (`webServer: pnpm dev`, `baseURL: http://localhost:3000`). Script `test:e2e:local` añadido a `package.json`.
- **Task 4 (PR #112)**: verificación integral (`pnpm test:e2e`) + 2 bugs encontrados y corregidos en `global-setup.ts` / `global-teardown.ts` (ver abajo). **11/11 tests PASS**.

**Decisiones / Problemas encontrados:**
- **Bug: `--remove-orphans` eliminaba `bigschool-mysql`**. Causa raíz: ambos compose files (`docker-compose.yml` y `docker-compose.e2e.yml`) están en `infra/`; Docker Compose deriva el proyecto del directorio → ambos usan proyecto `infra`. El teardown con `down --remove-orphans` ve a `bigschool-mysql` como huérfano del mismo proyecto y lo elimina, borrando también la red `infra_default`. Fix: `-p bigschool-e2e` en todos los comandos compose e2e → proyecto aislado, red `bigschool-e2e_default`, MySQL del proyecto `infra` completamente invisible para el teardown.
- **Bug: `create-expense` (test 1/11) timeout por cold-start del pool de BD**. El backend reporta `/health` como OK sin comprobar MySQL (conexión lazy de EF Core). El primer `POST /api/v1/auth/register` abría el pool de conexiones en frío; con 10 s de `waitForURL` en el test fallaba. Los tests 9 y 10 de login sí pasaban porque por entonces el backend llevaba ~8 minutos en marcha. Fix: `waitForApiReady` en el setup — hace `POST /api/v1/auth/login` con credenciales dummy y espera una respuesta 4xx (la BD respondió, el pool está caliente) antes de ceder el turno a los tests.
- **Instrucción MySQL corregida en el plan**: el comando original (`docker compose -f infra/docker-compose.yml up -d mysql`, sin el override) recrea el contenedor sin port binding `3306:3306`. Corregido en las 4 ocurrencias del plan a `cd infra && docker compose --env-file .env up -d mysql && cd -`.
- **`pnpm typecheck` en vez de `npx tsc --noEmit <ficheros>`**: pasar ficheros explícitos a `tsc` ignora el `tsconfig.json` del proyecto (sin `esModuleInterop`) y produce falsos errores TS1259 en imports `node:path`. Corrección documentada en el plan (Step 3 de Task 2).

**Resultado / Estado:**
- Plan 014 completado (4/4 tareas, PRs #109-#112).
- `pnpm test:e2e`: 11/11 PASS incluyendo `create-expense` como test 1/11 (cold-start resuelto).
- Teardown limpio: solo baja `bigschool-e2e-backend` y `bigschool-e2e-frontend`; `bigschool-mysql` permanece vivo; `bigschool_e2e` eliminada; `bigschool` y `bigschool_test` intactas.

**Siguiente paso:**
- [ ] Bloque 2 (seed), Bloque 3 (compose completo) y Bloques 4/5 (CI/deploy Azure) según spec 004.

---

## 2026-07-02 — Backend: monolito modular (Spec 0, Plan 018, Tasks 1-9)

### Fase: Implementación

**Módulo**: backend

**Actividades realizadas:**
- Spec `docs/superpowers/specs/005-2026-07-01-backend-modular-monolith-design.md` (Spec 0, fundacional — precede a las specs de features 006-010) y plan `docs/superpowers/plans/018-2026-07-01-backend-modular-monolith.md`; ejecución tarea a tarea con gate humano (1 tarea = 1 rama = 1 PR).
- Backend reorganizado de layer-first a **monolito modular**: 4 proyectos de capa intactos (`Domain/Application/Infrastructure/WebApi`), pero dentro de cada uno carpeta+namespace por módulo funcional (`Auth`, `Finanzas`, `Investments`, `Rag` esqueleto) + `SharedKernel` transversal. Convención estricta namespace = ruta de carpeta.
- **Task 1 (PR #123)**: building blocks compartidos del dominio (`BaseEntity`, `IAggregateRoot`, `IDomainEvent`, `IUnitOfWork`, `Money`, `MoneyConversion`, `Currency`, `EntityStatus`, excepciones base, `ExchangeRate`) → `Domain/SharedKernel`.
- **Task 2 (PR #124)**: entidades/enums/eventos/excepciones específicas de negocio reubicadas por módulo (`User`→Auth, `Transaction`/`SubCategory`→Finanzas, `Company`/`Portfolio`/`Holding`/`Disposal`→Investments, `RagDocument`→Rag).
- **Task 3 (PR #125)**: Commands/Queries/DTOs/Interfaces de Application por módulo + `Application/SharedKernel`. Corrección de diseño aplicada **antes** de mover ficheros (feedback humano): el movimiento de `Interfaces/Repositories` y `Interfaces/Services` es 1:1 por subcarpeta, nunca se funden en un único `Interfaces/` plano — se retocó el plan (Tasks 3, 4 y el código de ejemplo de Task 8) para que quedara coherente de principio a fin.
- **Task 4 (PR #126)**: configuraciones EF, repositorios y servicios de Infrastructure por módulo, con el mismo criterio 1:1 (`Persistence/Configurations/`, `Persistence/Repositories/`). Namespace de `Migrations/*.cs` preservado intacto por excepción explícita (evita romper `__EFMigrationsHistory`).
- **Task 5 (PR #127)**: Controllers de WebApi por módulo. Rutas por atributo, sin regresión de contrato.
- **Task 6 (PR #128)**: primera tarea con código nuevo (TDD) tras el bloque mecánico — `IIntegrationEvent`/`IIntegrationEventHandler`/`IIntegrationEventBus` (Application/SharedKernel) + `InMemoryIntegrationEventBus` (Infrastructure/SharedKernel), resuelve handlers por reflexión vía `IServiceProvider.GetServices`. Sin consumidores todavía (se estrenan en Spec 007).
- **Task 7 (PR #129)**: patrón Outbox transaccional — `OutboxMessage` + `OutboxMessageConfiguration` + migración `AddOutboxMessage`, `IntegrationEventOutbox` (encola en la misma UoW que el agregado), `OutboxDispatcher` (drena, publica, marca `ProcessedOn`, idempotente), `OutboxDispatchBehavior` (pipeline MediatR post-commit). TDD: test de integración escrito antes de la implementación, verificado en rojo.
- **Task 8 (PR #130)**: DI Autofac por módulo (`SharedKernelModule`, `AuthModule`, `FinanzasModule`, `InvestmentsModule`) sustituyendo el registro por-capa de `Program.cs`. Prueba de fuego: la suite E2E completa valida la resolución real del grafo de contenedores.
- **Task 9 (PR #131)**: guard tests de arquitectura (`BigSchool.Architecture.Tests`, NetArchTest) — fronteras entre módulos + Domain no depende de Infrastructure/Application. Al ejecutarlos por primera vez aparecieron 3 fugas reales de frontera (ver abajo).
- Documentación adicional (post-mergeo del plan): comentarios de evolución a microservicios en `IUserBaseCurrencyProvider`/`UserBaseCurrencyProvider` (rama `docs/018-modular-monolith-diario-and-notes`).

**Decisiones / Problemas encontrados:**
- **Fuga real de frontera (Task 9)**: 6 handlers de Finanzas/Investments (`CreateTransactionCommandHandler`, `UpdateTransactionCommandHandler`, `CreatePortfolioCommandHandler`, `GetPortfoliosQueryHandler`, `GetPortfolioPerformanceQueryHandler`, `GetPortfolioByIdQueryHandler`) dependían de `IUserRepository` (Auth) solo para leer `BaseCurrency`. Corregido introduciendo `IUserBaseCurrencyProvider` en `Application/SharedKernel`, implementado en `Infrastructure/Auth` — mismo patrón que `IExchangeRateProvider`: el módulo publica una capacidad estrecha vía SharedKernel en vez de exponer su repositorio completo.
- **Excepción documentada, no corregida (Task 9)**: `Auth.Entities.User` → `Finanzas` (`SubCategory`/`MainCategory`/`DuplicateSubCategoryDomainException`) es la excepción cerrada en spec 005 §7 — `SubCategory` sigue siendo hija de `User` hasta que la **Spec 009** la re-modele como AR independiente de Finanzas; el plan 018 prohíbe explícitamente re-modelarla aquí. Documentado inline en `ModuleBoundaryTests`, no relajado sin más.
- **Bug propio en scripts de refactor mecánico (Tasks 3/4)**: el script de PowerShell que reescribía `using`s confundía `using var conn = ...;` (declaración C#) con una directiva de importación y la insertaba en mitad de un método, rompiendo la compilación. Detectado por el propio build, corregido con lógica que restringe la búsqueda de "última línea `using`" a las líneas anteriores a la declaración `namespace`.
- **`dotnet new xunit` fijaba `net10.0`** en el nuevo proyecto `BigSchool.Architecture.Tests`, sobrescribiendo el `net8.0` centralizado en `Directory.Build.props`. Corregido a mano; de paso se centralizaron `TestSdkVersion`/`XunitRunnerVisualStudioVersion`/`NetArchTestVersion` (los 3 proyectos de test existentes hardcodeaban las versiones) siguiendo la convención del repo de nunca hardcodear `PackageReference` nuevas.
- **Reflexión sobre evolución a microservicios**: `IUserBaseCurrencyProvider` es válido tal cual mientras Auth viva en el mismo proceso/BD (monolito modular). Si Finanzas/Investments se extraen a microservicios reales, la interfaz sobrevive pero la implementación no se puede seguir inyectando por DI desde el código de otro servicio — dos caminos: (A) RPC síncrono a Auth (simple, acopla disponibilidad/latencia) o (B, recomendado) proyección local eventualmente consistente alimentada por el mismo `IIntegrationEventBus` + Outbox ya construidos en las Tasks 6-7, sin consumidores reales todavía pero con la infraestructura lista para este caso de uso exacto.

**Resultado / Estado:**
- Plan 018 completado (9/9 tareas, PRs #123-#131, todos mergeados).
- Suite final: `dotnet build` 0 errores · **233/233 tests en verde** (73 Domain + 64 Application + 8 Architecture + 88 Integration) · `dotnet ef migrations has-pending-model-changes` sin pendientes.
- Refactor sin cambio de comportamiento observable (mismas rutas, mismos contratos, misma API pública) salvo la corrección de frontera de Task 9 (invisible desde fuera: mismo resultado, distinta forma de resolver `BaseCurrency` internamente).

**Siguiente paso:**
- [ ] Spec 007 (Notifications): primer consumidor real del `IIntegrationEventBus` + Outbox — `UserRegisteredIntegrationEvent` → `EmailLog` de bienvenida.
- [ ] Spec 009 (Finanzas): re-modelar `SubCategory` como AR independiente de Finanzas; al hacerlo, retirar la excepción documentada en `ModuleBoundaryTests` (Auth ya no debería depender de Finanzas).
- [ ] Backlog de deuda técnica de backend (`docs/04-backend-tech-debt.md`) — specs 006-010 sobre la base modular ya cerrada.

---

## 2026-07-04 — Backend: paginación de listados (Spec 00, Plan 019, Tasks 1-4)

### Fase: Implementación

**Módulo**: backend

**Actividades realizadas:**
- Spec `docs/superpowers/specs/006-2026-07-01-backend-paginacion-listados-design.md` (Spec 00, segunda fundacional tras la Spec 0) y plan `docs/superpowers/plans/019-2026-07-01-backend-paginacion-listados.md`; ejecución tarea a tarea con gate humano (PRs #133-#138), ya sobre la estructura modular de la Spec 0.
- Generalizado el contrato de paginación que **ya cumplía** `GET /transactions` (`?page&pageSize` + `meta.totalCount`) al resto de listados tabulares — **Companies, Portfolios, Valuations** — con contrato idéntico (`PagedResult<T>` + `MetaData`).
- **Task 1 (#133)**: helper único `Pagination` en `Application/SharedKernel/Common` (`DEFAULT_PAGE_SIZE=20`, `MAX_PAGE_SIZE=100`) + dedup — se retiraron los `NormalizePage/NormalizePageSize` que vivían dentro de `GetTransactionsQuery`.
- **Task 2 (#134/#135)**: paginar `GET /companies` — `COUNT(*)` con los filtros pero **sin** el `LEFT JOIN` de última valoración; página con `ORDER BY c.Name, c.IdCompany` (desempate único).
- **Task 3 (#136)**: paginar `GET /portfolios` — el `COUNT(*)` cuenta **carteras**, no filas del `GROUP BY`; página conserva `GROUP BY` + `HOLDING_VALUATION` + `LIMIT/OFFSET`.
- **Task 4 (#137/#138)**: paginar `GET /companies/{id}/valuations` (`ORDER BY Date DESC, IdValuation DESC`).
- Patrón por listado: COUNT + página en un solo `QueryMultipleAsync` (espejo de `GetTransactionsQueryHandler`); E2E por endpoint (primera/segunda página sin solape, `totalCount`, cap de `pageSize` a 100, listado vacío). Adaptado `bigschool-api.http`.

**Decisiones / Problemas encontrados:**
- **Holdings: excepción consciente** (spec 006 §5) — colección hija acotada del AR `Portfolio`; se mantiene anidada en `GET /portfolios/{id}` y **no** se pagina. `PortfolioDetailDto` intacto → cero churn en frontend.
- **Series y summaries no se paginan** (`monthly-chart`, `summary`, `performance`): se quiere el conjunto completo por diseño.
- **`totalCount` de Portfolios cuenta carteras** (no holdings): test dedicado con cartera con 2 holdings + 2 vacías verificando `totalCount = 3`.

**Resultado / Estado:**
- Plan 019 completado (4/4 tareas, PRs #133-#138, mergeados). Build 0 errores + suite en verde (unit + E2E por listado).
- Con esto la **fase fundacional (Specs 0 + 00) queda cerrada**: el backend es modular, con IntegrationEvents + Outbox listos y todos los listados paginados. Base preparada para especificar las features 1-4 (Specs 007-010) contra código real.

**Siguiente paso:**
- [ ] Especificar Specs 007-010 (features 1-4) en una única rama, empezando por la 007 (Notifications): primer consumidor real del `IIntegrationEventBus` + Outbox.
- [ ] Al cerrar 1-4, nueva entrada de diario del bloque completo.

---

## 2026-07-04 — Backend: especificación de features 1-4 (Specs 007-010)

### Fase: Diseño

**Módulo**: backend

**Actividades realizadas:**
- Sesión de **especificación pura** (Opus) de las cuatro features de deuda técnica del backlog
  (`docs/04-backend-tech-debt.md`), redactadas en una única rama `feature/specs-007-010-backend-features`
  y **verificadas contra el código real post-modular** (specs 0/00 ya mergeadas), no de memoria.
- **Spec 007 — Notifications** (`007-…-notifications-emails-contactos`): módulo nuevo con AR `Contact`
  + `EmailLog` (+ `EmailType`). Estrena los dos patrones de comunicación de la Spec 0: alta de contacto
  → `EmailLog` por **DomainEvent intra-módulo (atómico)**; registro → `EmailLog` de bienvenida por
  **IntegrationEvent + Outbox (post-commit)**. Endpoints `POST /contacts` (público) + `GET /contacts|
  /emails|/emails/{id}` (privado, paginado; `IdUser = @user OR NULL`).
- **Spec 008 — User/Registro** (`008-…-user-registro-moneda`): `BaseCurrency` obligatoria en el registro
  (cambio de contrato); `GET/PUT /users/me` en `UsersController` nuevo; `User.UpdateProfile`/`ChangePassword`.
  `email` y `baseCurrency` inmutables.
- **Spec 009 — Finanzas** (`009-…-finanzas-agregaciones`): re-modela `SubCategory` de hija de `User` a
  **AR independiente** de Finanzas (cierra la deuda de spec 005 §7 y **reactiva** el guard
  `ModuleBoundaryTests` Auth ⊥ Finanzas); #8/C CRUD subcategorías; #6 `by-category`; #7 nuevo
  `/transactions/monthly` con filtros; validación de rango (from≤to, span máx 4 años).
- **Spec 010 — Inversiones** (`010-…-inversiones-summaries`): #10 campos `*Original` por holding en
  performance; #9 serie de precio por periodo anclada a la última valoración + summary; #11/D
  rename/delete de cartera; #11/E `GET /portfolios/summary` global.

**Decisiones / Problemas encontrados:**
- **Mejora de la UoW (A)** — decisión de diseño clave discutida a fondo: `BigSchoolDbContext.SaveChangesAsync`
  pasa a envolver en **transacción** y persistir en la **misma** transacción los efectos intra-BD que los
  domain-event handlers añaden (p. ej. la fila de `OutboxMessage`), con guard `CurrentTransaction is null`
  para ser **componible** ante llamadas anidadas. Sin (A), el patrón hecho-de-dominio → publish-handler →
  outbox no persistía la fila.
- **Regla intra-BD vs desacoplado**: el criterio no es "toca BD o no", sino **¿atómico con el agregado
  o efecto desacoplado?** Intra-módulo/mismo `DbContext` → DomainEvent dentro de (A) (atómico);
  otro módulo/externo (email, Elastic) → **IntegrationEvent + Outbox** post-commit (I/O externo dentro
  de la transacción = anti-patrón). Los **DomainEvents son hechos** (llevan la entidad) y los **handlers
  acciones** (1 hecho : N handlers).
- **`SubCategory.IdUser` como referencia suave** (sin FK dura), coherente con `EmailLog`; la migración
  del re-modelado solo suelta la FK a `Users`.
- **Regla fiscal en `Portfolio.Delete`** (feedback humano): no se borra una cartera con posiciones
  abiertas; si está cerrada, tampoco dentro de los **5 años fiscales de gracia** (prescripción ES) desde
  la última venta, con **excepción de dominio específica** (409 con `Code` propio) para que el frontend avise.
- **Frontend fuera de alcance** en las cuatro: cada spec deja el frontend como apunte; son backend puro.

**Resultado / Estado:**
- 4 specs de features redactadas y commiteadas (007-010) + entradas de backlog/diario. Ningún código de
  producción tocado todavía (sesión de diseño).

**Siguiente paso:**
- [ ] Planificar cada spec (planes 020+) y ejecutarla spec→plan→implementación con gate humano
  (1 tarea = 1 rama = 1 PR), empezando por la 007 (Notifications), que incluye la mejora de la UoW (A).
- [ ] Al implementar 007-010, nueva entrada de diario por bloque.

---

*Añadir nuevas entradas al final del documento con fecha y fase.*

### Plantilla para nuevas entradas:

```markdown
## YYYY-MM-DD — [Título descriptivo]

### Fase: [Diseño | Análisis | Implementación | Revisión]

**Módulo**: [backend | frontend-web | mobile | mcp-server | rag-service | infra]

**Actividades realizadas:**
- ...

**Decisiones / Problemas encontrados:**
- ...

**Resultado / Estado:**
- ...

**Siguiente paso:**
- ...
```

## 2026-07-04 — Planificación de features 1–4 (Planes 020–023)

### Fase: Diseño

**Módulo**: backend (Notifications, Auth, Finanzas, Investments)

**Actividades realizadas:**
- Redacción de los planes de implementación derivados de las Specs 007–010, sobre la estructura modular ya integrada (planes 018/019 en `develop`):
  - **020** — Notifications (EmailLog + Contactos + Welcome): UoW transaccional componible, `Contact`/`EmailLog` (AR), flujo Contacto (DomainEvent atómico) y Welcome (IntegrationEvent + Outbox), lecturas paginadas. 6 tareas.
  - **021** — User/Registro: moneda obligatoria en registro (contrato rompedor), `GET/PUT /users/me` (re-hash Argon2). 3 tareas.
  - **022** — Finanzas: `SubCategory` re-modelada como AR independiente (cierre de la frontera Auth⊥Finanzas), CRUD de subcategorías, agregaciones `by-category`/`monthly` con validación de rango. 3 tareas.
  - **023** — Investments: holdings `*Original`, serie de cotización por periodo, rename/delete de cartera con guards fiscales, summary global. 4 tareas.

**Decisiones clave (revisión de planes):**
- **Norma de EventHandlers**: son disparadores finos (`_mediator.Send(Command)`); el Command hace `entity.Add` + `SaveChangesAsync()`. Excepción documentada en código: el handler de Auth que encola el IntegrationEvent (no guarda; lo persiste el save-3 de la UoW componible, atómico con el `User`).
- **Convención namespace = carpeta** (anidado, con subcarpetas por categoría); se corrigió la Spec 005 en consecuencia.
- **Invariantes en el dominio**: `SubCategory.Delete(requestingUserId)` rechaza predefinidas y no-propietario; `Portfolio.Delete(today)` aplica guards de posiciones abiertas y gracia fiscal de 5 años (excepciones de dominio → 409).
- **Sin magic values**: el periodo de la serie de cotización se modela como enum `ValuationPeriod` (valor subyacente = nº de meses), compartible con el frontend.
- **DI**: se excluyen los `INotificationHandler` del escaneo Autofac (los posee MediatR) para evitar doble disparo; los `IIntegrationEventHandler` se resuelven por `GetServices`.

**Resultado / Estado:**
- Planes `docs/superpowers/plans/020–023` listos para ejecución task-by-task (subagent-driven).
- Rama `feature/007-010-backend-features`.

**Siguiente paso:**
- [ ] Ejecutar los planes por orden (020 → 023), una tarea = una rama = un PR con gate humano.

---

## 2026-07-04 al 2026-07-05 — Backend: módulo Notifications completo (Spec 007, Plan 020, Tasks 1-6)

### Fase: Implementación

**Módulo**: backend (Notifications)

**Actividades realizadas:**
- Spec `docs/superpowers/specs/007-2026-07-04-backend-notifications-emails-contactos-design.md` y plan `docs/superpowers/plans/020-2026-07-04-backend-notifications-emails-contactos.md`; ejecución tarea a tarea con gate humano. Primer módulo funcional nuevo desde el monolito modular (Spec 0) y primer consumidor real de `IIntegrationEventBus`/Outbox (Spec 0, Tasks 6-7 del plan 018).
- **Task 1 (PR #141)**: dominio del módulo — `Contact`, `EmailLog` (ARs), `EmailType` (Welcome=1, Contact=2), `ContactSubmittedDomainEvent`, `UserRegisteredDomainEvent` (nuevo en `Auth`, disparado por `User.Create`).
- **Task 2 (PR #142)**: **UoW transaccional componible** — `BigSchoolDbContext.SaveChangesAsync` reescrito para envolver en transacción y persistir en la **misma** transacción los efectos que los domain-event handlers añaden (p. ej. una fila de `EmailLog` disparada por el evento de otro agregado), con guard `CurrentTransaction is null` para admitir llamadas anidadas sin intentar un `BEGIN` doble en MySQL.
- **Task 3 (PR #143)**: persistencia — `IContactRepository`/`IEmailLogRepository`, configuraciones EF (`EmailLogs.IdUser` nullable **sin FK dura**, referencia blanda), migración `AddNotificationsModule`.
- **Task 4 (PR #144)**: flujo Contacto — `POST /contacts` (público) dispara `ContactSubmittedDomainEvent` → `SendContactAckEmailOnContactSubmittedHandler` (disparador fino) → `CreateContactAckEmailCommand` persiste el `EmailLog` de acuse en la **misma transacción** (Task 2). `GET /contacts` paginado. `NotificationsModule` (Autofac) excluye los `INotificationHandler<T>` del escaneo para que MediatR sea el único dueño de su resolución (si no, `Publish` los invocaría 2 veces vía `GetServices`).
- **Task 5 (PR #145)**: flujo Welcome — `Register` (Auth) → `UserRegisteredDomainEvent` → `PublishIntegrationEventHandler` encola `UserRegisteredIntegrationEvent` (excepción documentada: no llama a `SaveChanges`, lo persiste el save-3 de la UoW componible) → `OutboxDispatchBehavior` drena post-commit → `CreateWelcomeEmailOnUserRegisteredHandler` dispara el command del `EmailLog` de bienvenida. `AuthModule` recibe el mismo guard anti-doble-registro que `NotificationsModule`.
- **Task 6 (PR #146)**: lecturas — `GET /emails` (paginado, visibilidad `IdUser = @user OR IdUser IS NULL`) y `GET /emails/{id}` (404 si no visible, p. ej. welcome ajeno).
- `docs/swagger/bigschool-api.http` e `infra/docker/mysql/init.sql` actualizados en cada tarea relevante (endpoints nuevos; tablas `Contacts`/`EmailLogs` espejo de la migración, validado con `dotnet ef database update` contra el `init.sql` modificado).

**Decisiones / Problemas encontrados:**
- **Convención de carpetas de test establecida en esta sesión** (feedback humano, dos rondas): `Domain.Tests` reestructurado a `Entities/{Módulo}/` (con cambio de namespace, `git mv`); `Application.Tests` a `Commands/{Módulo}/`, `Validators/{Módulo}/` y una categoría nueva `EventHandlers/{Módulo}/`. Se propagó retroactivamente a los planes 021-023 (que aún usaban rutas planas) antes de ejecutarlos.
- **Nomenclatura de tests**: alineados a inglés + PascalCase (`Método_Escenario_Resultado`) tras detectar snake_case en español en los primeros ficheros generados; referencia canónica `PostTransactionTests.cs`.
- **Bug crítico real en `OutboxDispatcher` (hallado durante Task 5, no introducido por ella)**: `OutboxDispatchBehavior` corre en **todo** request MediatR; como `CreateWelcomeEmailOnUserRegisteredHandler` hace `_mediator.Send(CreateWelcomeEmailCommand)`, esa llamada reentra en el mismo pipeline. El `OutboxDispatcher` original solo marcaba `ProcessedOn` y guardaba **al final** del `foreach`, así que la consulta de pendientes de la llamada anidada seguía viendo la fila como `NULL` en BD → **recursión infinita**. Detectado empíricamente: un único registro generó 5000+ `EmailLogs` antes de que el proceso quedara colgado (vstest lo abortó). Corregido marcando y persistiendo `ProcessedOn` **antes** de invocar el handler de cada mensaje; test de regresión dedicado que simula la reentrada.
- **`DuplicateSubCategoryDomainException`-style guard**: mismo patrón de doble-registro Autofac/MediatR que ya se había resuelto en el plan 018 (Task 8), reaplicado aquí para `NotificationsModule` y `AuthModule`.
- **Test E2E del flujo Welcome reubicado**: vive en `Integration.Tests/Auth/RegisterTests.cs` (el endpoint bajo prueba es `/api/v1/auth/register`, de Auth) y no en `Notifications/`, aunque sus asserts lean `EmailLogs`/`OutboxMessages` — documentado inline como cruce de frontera deliberado, válido solo en monolito modular con BD compartida.

**Resultado / Estado:**
- Plan 020 completado (6/6 tareas, PRs #141-#146, todos mergeados).
- Suite final: Domain 83/83 · Application 74/74 · Architecture 8/8 · Integration 102/102.
- Primer módulo de negocio nuevo construido sobre la infraestructura de eventos de la Spec 0 (DomainEvent intra-módulo + IntegrationEvent/Outbox inter-módulo), con un bug de infraestructura real corregido y cubierto por regresión.

**Siguiente paso:**
- [ ] Plan 021 (User/Registro): moneda obligatoria + perfil.

---

## 2026-07-05 — Backend: User/Registro completo (Spec 008, Plan 021, Tasks 1-3)

### Fase: Implementación

**Módulo**: backend (Auth)

**Actividades realizadas:**
- Spec `docs/superpowers/specs/008-2026-07-04-backend-user-registro-moneda-design.md` y plan `docs/superpowers/plans/021-2026-07-04-backend-user-registro-moneda.md`; revisión previa de nomenclatura de tests del propio documento (todavía en español/estilo antiguo) antes de empezar, alineada a la convención cerrada en el plan 020.
- **Task 1 (PR #147)**: `BaseCurrency` **obligatoria** en el registro — `RegisterCommand` añade el 4º parámetro `Currency BaseCurrency`, validado con `IsInEnum` (omitirla → `default=0` → 400). Cambio de contrato rompedor: ajustados todos los call-sites existentes (`AuthEndpointTestBase.RegisterRawAsync`, tests de `GetEmailsTests` del plan 020) para pasar la moneda explícitamente.
- **Task 2 (PR #148)**: `GET /users/me` — `UserProfileDto` + `GetMeQuery/Handler` (Dapper, lectura directa por `IdUser` del token) + `UsersController` nuevo bajo `/api/v1/users`.
- **Task 3 (PR #149)**: `PUT /users/me` — métodos de dominio `User.UpdateProfile(fullName)` (valida no-vacío) y `User.ChangePassword(hash, salt)`; `UpdateUserCommand/Validator/Handler` con re-hash Argon2 **solo si** llega `password`; verificado con round-trip real de login (contraseña nueva funciona, la vieja falla) sin mocks.
- `docs/swagger/bigschool-api.http` actualizado (registro con `baseCurrency` + `GET/PUT /users/me`), junto con una limpieza de redacción en los `CLAUDE.md` de cada subdirectorio (referencia a `AGENTS.md` relativa al propio directorio).

**Decisiones / Problemas encontrados:**
- **Nomenclatura de tests del plan corregida antes de implementar**: el documento traía nombres en español (`Moneda_valida_pasa`, etc.) y estilo `.Validate(...).IsValid` en vez del `TestValidate`/`ShouldHaveValidationErrorFor` ya establecido en `RegisterCommandValidatorTests.cs` real — reescrito el plan completo (las 3 tareas) antes de tocar código.
- **`NotFoundException` requiere `(entityName, key)`**, no un mensaje libre: el snippet del plan usaba un solo argumento; ajustado al ctor real del repo.
- Sin cambios de frontera de módulo (todo dentro de Auth); `ModuleBoundaryTests` no se toca.

**Resultado / Estado:**
- Plan 021 completado (3/3 tareas, PRs #147-#149, todos mergeados).
- Suite final: Domain 83/83 · Application 87/87 · Architecture 10/10 · Integration 109/109.

**Siguiente paso:**
- [ ] Plan 022 (Finanzas/Finance): re-modelado de `SubCategory` + agregaciones.

---

## 2026-07-05 — Backend: Finanzas re-modelada + agregaciones (Spec 009, Plan 022, Tasks 1-3) + rename `Finanzas → Finance`

### Fase: Implementación

**Módulo**: backend (Finance, ex-Finanzas)

**Actividades realizadas:**
- Spec `docs/superpowers/specs/009-2026-07-04-backend-finanzas-agregaciones-design.md` y plan `docs/superpowers/plans/022-2026-07-04-backend-finanzas-agregaciones.md`; ejecución tarea a tarea con gate humano.
- **Task 1 (PR #150)**: `SubCategory` re-modelada de entidad hija de `User` a **Aggregate Root independiente**. `IdUser` pasa de propiedad sombra a explícita (nullable = global; referencia blanda **sin FK dura**, mismo patrón que `EmailLogs.IdUser`). Invariante de borrado en el dominio: `Delete(requestingUserId)` rechaza predefinidas (`IsDefault`) y no-propietarios. `User` pierde `_subCategories`/`SubCategories`/`AddSubCategory` y sus `using` de Finanzas → deja de referenciar el módulo. `ModuleBoundaryTests` **reactivado**: `Domain.Auth ⊥ Domain.Finanzas` pasa a estar en verde (cierra la excepción documentada desde el plan 018). Migración `RemodelSubCategoryAggregate` (solo suelta la FK; validada contra `init.sql` con `dotnet ef database update` → *already up to date*).
- **Task 2 (PR #151)**: CRUD de subcategorías — `POST/DELETE /categories/sub`. `ISubCategoryRepository.ExistsActiveAsync` (unicidad por nombre+categoría entre las propias y las globales); `DeleteSubCategoryCommandHandler` con 404 no-leak (inexistente, predefinida, global o ajena) reafirmado por la invariante del dominio como defensa en profundidad.
- **Refactor intermedio (PR #152)**: renombrado el módulo **`Finanzas` → `Finance`** en las 4 capas + tests (namespaces, carpetas, `FinanzasModule`→`FinanceModule`), consolidando además la carpeta de tests E2E `Integration.Tests/Transactions` → `Integration.Tests/Finance` (era el único punto donde módulo y agregado colisionaban de nombre). Ver detalle en "Decisiones" — se trata como entrada propia por alcance (afecta a las 4 capas + AGENTS.md), aunque nace de una observación durante este mismo plan.
- **Task 3 (PR #153)**: agregaciones — `GET /transactions/by-category` (totales por `MainCategory` sobre `BaseAmount`) y `GET /transactions/monthly` (agrupado año-mes, split Income/Expense, reutiliza `MonthlyChartPointDto`). `DateRange.IsValid` (`SharedKernel.Common`) como predicado de rango compartido (`from ≤ to`, span ≤ 4 años) entre ambos validators. `GET /transactions/monthly-chart?year=` (dashboard) confirmado sin tocar.
- `docs/swagger/bigschool-api.http` actualizado con los 4 endpoints nuevos (`POST/DELETE /categories/sub`, `GET /transactions/by-category`, `GET /transactions/monthly`).

**Decisiones / Problemas encontrados:**
- **`DuplicateSubCategoryDomainException` mal jerarquizada**: heredaba de `DomainException` (→400) en vez de `ConflictException` (→409), a diferencia de sus hermanas (`EmailAlreadyExistsDomainException`, `DuplicateTickerDomainException`, `DuplicateValuationDomainException`). El propio plan señalaba el riesgo; confirmado y corregido cambiando la base, sin tocar el middleware.
- **Colisión de nombres `SubCategoryDto`**: `Application.Finance.DTOs.SubCategoryDto` (nuevo, forma CRUD) colisionaba con el `SubCategoryDto` ya existente en `Queries.Categories.GetCategories` (item anidado, forma distinta). Resuelto cualificando por nombre completo en `CategoriesController` en vez de añadir el `using` que colisiona.
- **Rename `Finanzas → Finance` (PR #152)**: el usuario detectó que "Finanzas" era el único módulo en español (resto: Auth/Investments/Notifications/SharedKernel en inglés) y que colisionaba conceptualmente con "Transactions" — que en realidad es una *feature* dentro del módulo (`Queries/Transactions`), no el módulo. Decisiones tomadas explícitamente: (1) nombre elegido `Finance` (bounded context completo, no solo el agregado `Transaction`); (2) la carpeta de tests E2E se renombra también, a diferencia de dejarla como `Transactions/`; (3) el módulo `Rag` (2 ficheros, marcado "futuro" en spec 005) se mantiene y se documenta como 6º módulo futuro en `AGENTS.md`. Alcance del rename **acotado a código + AGENTS.md + backlog vivo** (`04-backend-tech-debt.md`) — specs/planes históricos (005/009/010, diario, 01-arquitectura, 02-backend-design) se dejan como registro de época; los planes en vuelo (022 Task 3, 023) se adaptan al ejecutarlos. Verificado behavior-preserving: `has-pending-model-changes` sin cambios (los strings de tipo del `ModelSnapshot` se reescribieron en el mismo commit) y suite completa en verde antes/después.
- **`SubCategory.IdUser` como referencia suave**: se reafirma el patrón (sin FK dura) ya usado en `EmailLog`, coherente con la filosofía de frontera de módulo por convención + `NetArchTest`, no por integridad referencial de BD.

**Resultado / Estado:**
- Plan 022 completado (3/3 tareas, PRs #150-#153, todos mergeados) + refactor de nomenclatura (PR #152).
- Suite final: Domain 81/81 · Application 101/101 · Architecture 10/10 (`Auth ⊥ Finance` en verde) · Integration 122/122.
- Con esto, el módulo Finance queda con nomenclatura consistente (inglés, sin ambigüedad módulo↔agregado) y su deuda de re-modelado (Spec 009) cerrada.

**Siguiente paso:**
- [ ] Plan 023 (Investments): holdings `*Original`, serie de cotización por periodo, rename/delete de cartera con guards fiscales, summary global — namespaces ya en inglés (`Investments`), sin impacto del rename.

---

## 2026-07-05 al 2026-07-06 — Backend: Investments completo (Spec 010, Plan 023, Tasks 1-4)

### Fase: Implementación

**Módulo**: backend (Investments)

**Actividades realizadas:**
- Spec `docs/superpowers/specs/010-2026-07-04-backend-inversiones-summaries-design.md` y plan `docs/superpowers/plans/023-2026-07-04-backend-inversiones-summaries.md`; ejecución tarea a tarea con gate humano y TDD estricto (RED verificado antes de cada implementación).
- **Task 1 (PR #155)**: campos `*Original` (moneda de la empresa, sin conversión FX) en `HoldingPerformanceDto` — `CostBasisOriginal`, `MarketValueOriginal`, `UnrealizedPnLOriginal`, reutilizando `PortfolioSqlFragments.HOLDING_VALUATION` (aditivo, no toca `GetPortfolios`).
- **Task 2 (PR #156)**: `GET /companies/{id}/valuations/series?period=` — serie de precio + summary (`first/last/min/max/changePct`), anclada a la **última** valoración de la empresa (no a "hoy"). Periodo modelado como enum `ValuationPeriod` cuyo valor subyacente **es el nº de meses** (3/6/12/36/60) — sin magic values, mismo patrón que `Currency`.
- **Task 3 (PR #157)**: `PUT`/`DELETE /portfolios/{id}` — rename siempre permitido; delete con dos guards de dominio (`PortfolioHasOpenPositionsDomainException`, `PortfolioWithinFiscalGracePeriodDomainException`, ambas → 409). `Portfolio.Delete(today)` como dominio puro (el reloj lo resuelve Application).
- **Task 4 (PR #158)**: `GET /portfolios/summary` — agregado en moneda base de todas las carteras activas del usuario (market value/cost basis/unrealized de holdings abiertos + realized/portfolio count de todas las carteras). Cierra el plan 023 al completo.
- `docs/swagger/bigschool-api.http`: nueva sección "INVESTMENTS — Plan 023" (endpoints 43-46: serie de valoraciones, periodo inválido→400, rename, delete, summary) + nota de los campos `*Original` añadida a la entrada 30 (performance) ya existente.

**Decisiones / Problemas encontrados:**
- **Bug real — columna SQL duplicada (Task 1)**: el primer intento re-seleccionaba `x.BuyOriginalCurrency AS BuyOriginalCurrency` como columna "nueva" en `HOLDING_VALUATION`, pero ya se seleccionaba sin alias más arriba en el mismo SELECT — dos columnas con el mismo nombre en la derived table. Al referenciarla por nombre desde un consumidor (`OPEN_HOLDINGS_QUERY`), MySQL la resolvía como ambigua en runtime → 500 que tumbó 6 tests E2E de Investments, incluidos algunos de `GetPortfolios` que ni tocan las columnas nuevas (comparten el mismo fragmento). Corregido eliminando la re-selección redundante.
- **Excepciones de dominio con base class incorrecta (Task 3)**: mismo patrón de bug que `DuplicateSubCategoryDomainException` en el plan 022 — `PortfolioHasOpenPositionsDomainException`/`PortfolioWithinFiscalGracePeriodDomainException` debían heredar de `ConflictException` (409), no de `DomainException` (400). Al despachar `ExceptionHandlingMiddleware` por *pattern matching* de tipo (cubre subtipos), el Step del plan que proponía modificar el middleware sobraba y se eliminó.
- **`NotFoundException` con mensaje libre no compila (Task 3)**: el plan pasaba un string suelto; el ctor real es `(entityName, key)`. Mismo bug ya visto y corregido en el plan 021.
- **Convenciones de organización de tests no respetadas al implementar Task 3, corregidas tras revisión humana**: (1) los tests de dominio de `Rename`/`Delete` se crearon en un fichero nuevo (`PortfolioRenameDeleteTests.cs`) en vez de añadirse al `PortfolioTests.cs` ya existente de la misma clase — violaba "un fichero de test por clase bajo prueba"; fusionados en el fichero existente. (2) el E2E se escribió como un único fichero combinando `PUT` y `DELETE` — violaba "un fichero de test por endpoint"; separado en `PutPortfolioTests.cs`/`DeletePortfolioTests.cs`. (3) **tests de handler ausentes**: se habían omitido `RenamePortfolioCommandHandlerTests.cs`/`DeletePortfolioCommandHandlerTests.cs` (mock de `IPortfolioRepository`), pese a ser el patrón ya establecido para el resto de handlers de Investments — el E2E no sustituye la cobertura aislada del handler; añadidos a posteriori.
- **Bug real — `COUNT(*)` como `long` en Dapper (Task 4)**: `RealizedRow(decimal RealizedPnL, int PortfolioCount)` fallaba en runtime (`InvalidOperationException` al materializar). `COUNT(*)` en MySQL/MySqlConnector se lee como `long`; cuando Dapper construye un record de **varias columnas vía su constructor** (path IL-generado) exige tipo exacto, sin conversión numérica — a diferencia de los `COUNT(*)` ya existentes en el proyecto (paginación de `GetCompanies`, `GetPortfolios`, etc.), que lo leen como **valor escalar único** (`ReadSingleAsync<int>()`), donde Dapper sí convierte. Corregido con `long PortfolioCount` + cast a `int` al construir el DTO; no aplica a los conteos escalares existentes.
- **Nota aparte — symlinks `CLAUDE.md` → `AGENTS.md`**: se detectó que `CLAUDE.md` (raíz y los 6 subdirectorios) solo apuntaban a `AGENTS.md` con un texto ("Usar: AGENTS.md"), que no se resuelve automáticamente al cargar contexto en un agente — depende de que este decida seguir el puntero. Convertidos los 7 en symlinks reales (target relativo, mismo directorio) para que el auto-load de `CLAUDE.md` lea directamente el contenido de `AGENTS.md`, sin duplicar texto. Encontrado un problema de entorno: con `core.symlinks=false` (config del repo en esta máquina), git guarda el symlink correctamente en el índice (modo `120000`) pero cada `checkout`/cambio de rama lo materializaba en el working tree como fichero de texto plano con la ruta, no como enlace real — pasó desapercibido varias veces durante los cambios de rama de este mismo plan hasta que se verificó explícitamente. Solucionado activando `core.symlinks=true` (ejecutado por el usuario, no por el agente) y re-materializando los symlinks.

**Resultado / Estado:**
- Plan 023 completado (4/4 tareas, PRs #155-#158, todos mergeados).
- Suite final: Domain 87/87 · Application 113/113 · Architecture 10/10 · Integration 139/139.
- Con esto, Investments cubre performance con desglose en moneda de empresa, serie histórica de cotización por periodo, gestión completa de carteras (crear/renombrar/borrar con guards fiscales) y un resumen global multi-cartera — cierra la Spec 010. Con los planes 020-023 completados, la deuda técnica de backend identificada tras el MVP de frontend queda resuelta.

**Siguiente paso:**
- [ ] Frontend-web: consumir los endpoints de backend ya cerrados (Notifications, User/Registro, Finance, Investments) — la deuda técnica de backend que los bloqueaba está resuelta.

---

## 2026-07-06 — Frontend-Web: diseño de la Iteración 2 — integración con el Backend (Spec 011, Plan 024)

### Fase: Diseño

**Módulo**: frontend-web

**Actividades realizadas:**
- Spec `docs/superpowers/specs/011-2026-07-06-frontend-web-backend-integracion-design.md` y plan `docs/superpowers/plans/024-2026-07-06-frontend-web-backend-integracion.md`: cierran, desde el lado del Frontend-Web, la deuda técnica de backend registrada en `docs/04-backend-tech-debt.md` (puntos 1-4, ya resueltos por los planes 020-023).
- Plan estructurado en 9 tareas ordenadas por prioridad, cada una **1 rama `feature/024-frontend-integracion-taskN` = 1 PR = revisión humana**, cubriendo Notifications, Finance (2 gráficas nativas), Auth (moneda + perfil), paginación reutilizable, Investments (renombrar/borrar cartera, holdings `*Original`, summary global) y las pantallas nuevas de "Mercado" (Companies + Valuations).

**Decisiones clave (spec 011 §4):**
- **Nav "Mercado"**: Companies/Valuations (catálogo global, sin `IdUser`) reciben una entrada propia de navegación (`/market`); Contactos/Emails (vistas de administración) se acceden desde el `ProfileDropdown`, no desde el nav principal.
- **Perfil** sale de "fuera de alcance MVP": dos PRs adyacentes (T3a moneda en registro, T3b perfil leer/editar).
- **Gráficas de Finance = 2 gráficas nativas** sobre ventana de 4 años: la Gráfica A actual (barras por categoría × año) **no cambia de aspecto ni componente**; solo se reescribe el cuerpo de `useCategoryChart` para tirar de `by-category` (4 llamadas, una por año), lo que además **arregla un bug real** (el límite de 100 filas del paginado impedía agregar los 4 años completos en cliente). Gráfica B (nueva): barras apiladas por mes, `monthly` filtrado solo por Tipo.
- **Renombrar/borrar cartera** en la cabecera de la vista de detalle; el borrado respeta los guards fiscales del backend (409 si hay holdings abiertos).
- **Holdings `*Original`**: se materializan los campos que el `PerformanceTab` ya pintaba con fallback `—`; `GET /portfolios/summary` alimenta el mini-resumen del Dashboard.
- **Selector de empresa (añadir holding)** pasa a combobox con typeahead sobre `GET /companies?pageSize=100` filtrado en cliente (deuda documentada: sin `?search=` server-side).
- **Deuda que queda** (documentada, sin botones muertos): Companies sin `PUT/DELETE`; Valuations sin `GET{id}` ni `DELETE`; búsqueda server-side de empresas pendiente.

**Resultado / Estado:**
- Spec 011 y plan 024 aprobados y mergeados (PR #160).
- Arranca la ejecución tarea a tarea con revisión humana entre tareas.

**Siguiente paso:**
- [ ] Task 1 — Contactos + Emails a endpoints reales (módulo Notifications).

---

## 2026-07-07 — Frontend-Web: Task 1 — Contactos + Emails a endpoints reales (Plan 024)

### Fase: Implementación

**Módulo**: frontend-web (Notifications)

**Actividades realizadas:**
- `useContacts`/`useEmails` migrados de `localStorage`/mock en memoria a TanStack Query real contra `GET/POST /contacts` y `GET /emails`. Nuevos `types/notifications.ts` y `services/notificationService.ts`. `contact-form.tsx` pasa a `POST /contacts` real (toast de éxito/error); `contacts/page.tsx` y `emails/page.tsx` añaden estados de carga (Skeleton) y error, además del vacío ya existente. Retirados los restos de mock (`loadContacts`/`saveContact`/`ContactSubmission`/`LS_KEY`).
- TDD estricto: tests reescritos primero (`tests/unit/useContacts.test.tsx`, `useEmails.test.tsx`), verificados en rojo (7/7 fallando por el motivo correcto — los hooks viejos no llamaban al service) antes de implementar.

**Decisiones / Problemas encontrados:**
- **Los DTO reales del backend difieren de lo asumido en el plan** (verificado contra `Controllers/Notifications/*` y `Application/Notifications/DTOs/*` antes de escribir los tipos, como manda la norma del plan): `EmailLogListItemDto` no tiene `body` ni `status`, usa `recipient`/`sentAt` (no `toAddress`/`createdAt`) y `type` viaja como **número crudo** (Dapper, sin `JsonStringEnumConverter`: 1=Welcome, 2=Contact). `GET /contacts` y `GET /emails` están **paginados** (el plan no lo contemplaba); de momento se piden con `pageSize=100` sin controles de paginación en la UI — el componente reutilizable llega en la Task 5.
- **Gap preexistente detectado (no introducido en esta tarea)**: `eslint` no está instalado como dependencia en `frontend-web` pese a existir el script `lint` en `package.json` — `npm run lint` no es ejecutable. Confirmado con `git diff` que `package.json`/`pnpm-lock.yaml` no se han tocado. Decisión consensuada con el humano: se deja como deuda aparte, fuera de alcance de este plan.

**Resultado / Estado:**
- Task 1 completada y mergeada (PR #161). `typecheck` limpio, 221/221 tests unitarios en verde.

**Siguiente paso:**
- [x] Task 2 — Finance: migrar Gráfica A al backend (fix del límite de 100) + añadir Gráfica B (módulo Finance).

---

## 2026-07-07 — Frontend-Web: Task 2 — Finance: Gráfica A al backend + Gráfica B nueva (Plan 024)

### Fase: Implementación

**Módulo**: frontend-web (Finance)

**Actividades realizadas:**
- Verificados contra `TransactionsController.cs` (`GET /transactions/by-category`, `GET /transactions/monthly`) y sus DTOs (`CategoryTotalDto`, `MonthlyChartPointDto`) antes de tocar tipos — a diferencia de la Task 1, aquí el plan **acertó exactamente** la forma real (`CategoryTotal { idMainCategory, mainCategory: string, total }`, rango del enum `MainCategory` 1-7 gasto / 10-13 ingreso).
- **Gráfica A**: se conservan intactos `category-bars.tsx`, la pestaña y `aggregateByCategory.ts` (función pura, queda como fallback/tests); solo se reescribe el cuerpo de `useCategoryChart` (misma firma, mismo output `CategoryAggregation`) para hacer **4 llamadas a `by-category`** (una por año) vía `useQueries`, en vez de agregar en cliente sobre `useTransactions` con `pageSize=5000` — esto **arregla un bug real**: el backend topa la paginación en 100 filas, así que la agregación en cliente nunca veía los 4 años completos.
- **Gráfica B** (nueva): `useMonthlySeries` — una llamada a `monthly` por cada `MainCategory` del tipo elegido (7 gasto / 4 ingreso, vía `CATEGORIES_BY_TYPE` derivado del enum).
- `expenses/page.tsx`: añadido selector de año de referencia (máx = año actual) que alimenta ambas gráficas; Gráfica A intacta debajo del selector, Gráfica B nueva debajo de esa.
- TDD estricto: `tests/unit/useCategoryChart.test.tsx` (reescrito, no existía como fichero dedicado hasta ahora) y `tests/unit/useMonthlySeries.test.tsx` (nuevo), verificados en rojo antes de implementar.

**Decisiones / Problemas encontrados:**
- **Fix a mis propios tests, no al código de producción**: dos aserciones esperaban en una condición (`years`) que no dependía de los datos async, dejando pasar el resto de expects antes de que la promesa resolviera. Corregido esperando en la condición real (`rows`/`points`).
- **`expensesPage.test.tsx` roto por el cambio de hook**: `useCategoryChart`/`useMonthlySeries` llaman a `useQueries` de TanStack Query directamente (antes `useCategoryChart` delegaba en `useTransactions`, ya mockeado en ese test) → hacía falta un `QueryClientProvider` real. Añadidos mocks de ambos hooks en `expensesPage.test.tsx` (mismo patrón que el `useCategories` ya mockeado), ya que esa suite no ejercita la pestaña de Gráficas.
- **Rediseño de la Gráfica B tras revisión humana del PR #162**: la primera implementación (`monthly-stacked-bars.tsx`) mostraba **una sola gráfica** con los 12 meses × 4 años en el eje X y las categorías **apiladas** como segmentos — no era el diseño esperado. Corregido a **una gráfica por categoría** (desagregada, `monthly-category-bars.tsx`), cada una con los 12 meses en el eje X y **una barra por año agrupada** (no apilada) dentro de cada mes — mismo patrón visual que `category-bars.tsx` pero con los ejes girados (categoría↔mes). `expenses/page.tsx` renderiza ahora una rejilla de N gráficas (una por `MainCategory` del tipo) en vez de una única gráfica combinada.

**Resultado / Estado:**
- Task 2 completada (PR pendiente de apertura). `typecheck` limpio, 229/229 tests unitarios en verde.

**Siguiente paso:**
- [ ] Task 3 — Moneda base obligatoria en Registro (módulo Auth).
