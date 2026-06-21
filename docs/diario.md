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
