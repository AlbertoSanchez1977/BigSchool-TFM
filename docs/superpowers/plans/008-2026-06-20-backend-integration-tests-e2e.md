# Backend — Tests de Integración E2E (Auth + Transactions) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Construir tests de integración E2E por endpoint con **foco prioritario en `AuthController`** (punto crítico: hashing Argon2 real, emisión/validación JWT real, persistencia en BD), y completar la cobertura de `TransactionsController`, fijando además las normas de testing E2E en `AGENTS.md`.

**Architecture:** Tests E2E sobre `WebApplicationFactory<Program>` (pipeline real: JwtBearer, MediatR, FluentValidation, EF Core commands, Dapper queries, middleware de excepciones) contra MySQL real (`bigschool_test`). Una clase base `IntegrationTestBase` centraliza el ciclo de vida del factory, el reset de BD y los tipos de deserialización del envelope `ApiResponse<T>`. Sobre ella, `AuthEndpointTestBase` y `TransactionEndpointTestBase` añaden helpers de cada feature. Cada endpoint tiene su fichero `{Verbo|Accion}{Recurso}Tests.cs`. El valor diferencial frente a los unitarios: se ejercita el **hashing y el JWT reales** y la **serialización HTTP**, que los unitarios mockean.

**Tech Stack:** xUnit, FluentAssertions, `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory<Program>`), MySqlConnector + Dapper (verificación física + manipulación directa de BD), Moq (para `IMediator` al sembrar vía `DbContext`).

---

## Contexto del código existente (leer antes de empezar)

### AuthController (`src/backend/src/BigSchool.WebApi/Controllers/AuthController.cs`) — PRIORITARIO
- `POST /api/v1/auth/register` body `RegisterCommand(Email, Password, FullName)` → 200 `ApiResponse<AuthResponseDto>` / 400 / **409**.
- `POST /api/v1/auth/login` body `LoginCommand(Email, Password)` → 200 `ApiResponse<AuthResponseDto>` / 400.
- `POST /api/v1/auth/refresh` **`[Authorize]`**, sin body (lee el header `Authorization`) → 200 `ApiResponse<AuthResponseDto>` / 401.
- `AuthResponseDto(string AccessToken, DateTime ExpiresAt, string Email, string FullName)`.
- **Validadores**: Register → email no vacío + formato email + ≤255; password no vacía + ≥8 + ≤100; fullName no vacío + ≤200. Login → email no vacío + formato; password no vacía.
- **Excepciones de negocio**:
  - Register email duplicado → `EmailAlreadyExistsDomainException : ConflictException` → **409** `Code="EMAIL_ALREADY_EXISTS"`.
  - Login credenciales inválidas (usuario inexistente O password incorrecta) → `InvalidCredentialsDomainException : DomainException` → **400** `Code="INVALID_CREDENTIALS"`.
  - Refresh token inválido / usuario inexistente → `UnauthorizedAccessException` → **401**.
- **Hashing**: `RegisterCommandHandler` usa el `IPasswordHasher` REAL (Argon2); `LoginCommandHandler` verifica con el mismo. Un unitario los mockea → solo un E2E prueba el round-trip real.

### TransactionsController (`Controllers/TransactionsController.cs`)
- 8 acciones, todas `[Authorize]`. `POST`/`PUT` body `CreateTransactionRequest(Type, IdMainCategory, IdSubCategory, Description, TransactionDate, Amount, Currency?)`.
  - `POST` → 200 `ApiResponse<TransactionDto>` / 400. `PUT {id}` → 200 / 404. `DELETE {id}` → 200 / 404.
  - `GET {id}` → 200 `ApiResponse<TransactionListItemDto>` / 404. `GET` lista `?type&category&from&to&page&pageSize` → 200 con `Meta{Page,PageSize,TotalCount}`.
  - `GET summary?from&to` → 200 `ApiResponse<TransactionSummaryDto>`. `GET monthly-chart?year` → 200 `ApiResponse<IReadOnlyList<MonthlyChartPointDto>>`.
- **Validador POST** (`CreateTransactionCommandValidator`): `Amount>0`, `IdMainCategory`/`Type` `IsInEnum`, `TransactionDate` no vacía, `Description`≤255.
- **DTOs**: `TransactionDto` → `Type`/`IdMainCategory` serializan como **string** (`JsonStringEnumConverter`), monedas string, fechas `DateOnly` (`"yyyy-MM-dd"`). `TransactionListItemDto` → `Type` **short**, `IdMainCategory` int. `TransactionSummaryDto(TotalIncome, TotalExpense, Balance, BaseCurrency)`. `MonthlyChartPointDto(Year, Month, Income, Expense)`.

### Infra común
- **Middleware** (`Middleware/ExceptionHandlingMiddleware.cs`): `ValidationException`→400 (`Code="VALIDATION_ERROR"`, `Field`), `NotFoundException`→404 (`ENTITY_NOT_FOUND`), `ConflictException`→409, `DomainException`→400, `UnauthorizedAccessException`→401. Serializa `ApiResponse.Fail(errors)` en camelCase.
- **Enums**: `TransactionType{Income=0,Expense=1}`; `MainCategory{EssentialExpenses=1,Investment=2,Savings=3,Donations=4,Luxuries=5,Education=6,Amortizations=7,Salary=10,Rentals=11,Dividends=12,Other=13}`; `Currency{EUR=978,USD=840,GBP=826,CHF=756,JPY=392}`; `EntityStatus{Pending=1,Active=2,Processing=3,Deleted=4}`.
- **SubCategories sembradas** en `bigschool_test`: IDs 1-28 (`1=Supermercado`).
- **Conversión** (`ExchangeRateApiClient`): busca `(From,To,RateDate=transactionDate)` en `ExchangeRates`; con caché es determinista (sin red). Para tests USD: sembrar la fila con `RateDate = transactionDate`.
- **Fixtures** (`Fixtures/`): `MySqlDatabaseFixture` (crea/migra `bigschool_test`, `ResetAsync()` borra Users/Transactions/SubCategories de usuario/ExchangeRates no-seed), `BigSchoolWebAppFactory` (sobrescribe connection string), `IntegrationCollection` (colección serial compartida).
- **Tests E2E existentes a sustituir**: `Transactions/TransactionsEndpointTests.cs` (2 tests). Se reparte/migra a los nuevos ficheros.

## File Structure

- **Crear** `tests/.../IntegrationTestBase.cs` — base compartida (ciclo de vida + envelope).
- **Crear** `tests/.../Auth/AuthEndpointTestBase.cs` — helpers de auth (RegisterAsync, manipulación de usuario).
- **Crear** `tests/.../Auth/RegisterTests.cs` — EXHAUSTIVO (round-trip Argon2 + persistencia) + 409 + 400.
- **Crear** `tests/.../Auth/LoginTests.cs` — éxito + token usable en endpoint protegido + 400 credenciales + 400 validación.
- **Crear** `tests/.../Auth/RefreshTests.cs` — éxito + 401 sin token + 401 usuario eliminado.
- **Crear** `tests/.../Transactions/TransactionEndpointTestBase.cs` — helpers de transactions.
- **Crear** `tests/.../Transactions/{PostTransaction,GetTransactionById,GetTransactions,PutTransaction,DeleteTransaction,GetTransactionSummary,GetMonthlyChart}Tests.cs`.
- **Borrar** `tests/.../Transactions/TransactionsEndpointTests.cs`.
- **Modificar** `src/backend/AGENTS.md` — sección Testing.

Ruta base de tests: `src/backend/tests/BigSchool.Integration.Tests/`.

---

### Task 1: Base compartida `IntegrationTestBase`

**Files:**
- Create: `src/backend/tests/BigSchool.Integration.Tests/IntegrationTestBase.cs`

- [ ] **Step 1: Escribir la base compartida**

```csharp
using BigSchool.Integration.Tests.Fixtures;
using Xunit;

namespace BigSchool.Integration.Tests;

/// <summary>
/// Ciclo de vida común a todos los tests E2E: reset de la BD de test antes de cada test
/// y un BigSchoolWebAppFactory propio (pipeline real) por test. Las clases derivadas deben
/// llevar [Collection(IntegrationCollection.Name)].
/// </summary>
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly MySqlDatabaseFixture Fixture;
    protected BigSchoolWebAppFactory Factory = null!;

    protected IntegrationTestBase(MySqlDatabaseFixture fixture) => Fixture = fixture;

    public async Task InitializeAsync()
    {
        await Fixture.ResetAsync();
        Factory = new BigSchoolWebAppFactory(Fixture.ConnectionString);
    }

    public Task DisposeAsync()
    {
        Factory.Dispose();
        return Task.CompletedTask;
    }

    // ---- Tipos de deserialización del envelope ApiResponse<T> (System.Text.Json, camelCase) ----
    protected record ApiEnvelope<T>(T? Data, List<ApiErrorPayload> Errors, MetaPayload? Meta);
    protected record ApiErrorPayload(string Code, string Message, string? Field);
    protected record MetaPayload(int? Page, int? PageSize, int? TotalCount, int? TotalPages);
}
```

- [ ] **Step 2: Compilar**

Run: `dotnet build src/backend/tests/BigSchool.Integration.Tests/BigSchool.Integration.Tests.csproj -c Debug`
Expected: BUILD SUCCEEDED, 0 errores.

- [ ] **Step 3: Commit**

```bash
git add src/backend/tests/BigSchool.Integration.Tests/IntegrationTestBase.cs
git commit -m "test: añadir IntegrationTestBase compartida (ciclo de vida + envelope)

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 2: `AuthEndpointTestBase` — helpers de auth

**Files:**
- Create: `src/backend/tests/BigSchool.Integration.Tests/Auth/AuthEndpointTestBase.cs`

- [ ] **Step 1: Escribir la base de auth**

```csharp
using System.Net.Http.Json;
using BigSchool.Integration.Tests.Fixtures;
using Dapper;
using MySqlConnector;

namespace BigSchool.Integration.Tests.Auth;

/// <summary>Helpers específicos de los endpoints de autenticación.</summary>
public abstract class AuthEndpointTestBase : IntegrationTestBase
{
    protected AuthEndpointTestBase(MySqlDatabaseFixture fixture) : base(fixture) { }

    protected const string DefaultPassword = "Secret123!";

    // Registra un usuario vía el endpoint REAL y devuelve la respuesta HTTP (sin assertions).
    protected Task<HttpResponseMessage> RegisterRawAsync(string email, string password, string fullName)
        => Factory.CreateClient().PostAsJsonAsync("/api/v1/auth/register",
            new { email, password, fullName });

    // Registra y devuelve el AuthResponse deserializado (asume éxito).
    protected async Task<AuthResponse> RegisterAsync(string email, string fullName = "E2E User")
    {
        var resp = await RegisterRawAsync(email, DefaultPassword, fullName);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ApiEnvelope<AuthResponse>>())!.Data!;
    }

    // Lee el PasswordHash crudo de un usuario (para verificar que NO se persiste en claro).
    protected async Task<string?> ReadPasswordHashAsync(string email)
    {
        await using var conn = new MySqlConnection(Fixture.ConnectionString);
        return await conn.QuerySingleOrDefaultAsync<string?>(
            "SELECT PasswordHash FROM Users WHERE Email = @email;", new { email });
    }

    // Elimina físicamente un usuario (para el caso refresh con usuario inexistente).
    protected async Task DeleteUserAsync(string email)
    {
        await using var conn = new MySqlConnection(Fixture.ConnectionString);
        await conn.ExecuteAsync("DELETE FROM Users WHERE Email = @email;", new { email });
    }

    protected record AuthResponse(string AccessToken, DateTime ExpiresAt, string Email, string FullName);
}
```

- [ ] **Step 2: Compilar**

Run: `dotnet build src/backend/tests/BigSchool.Integration.Tests/BigSchool.Integration.Tests.csproj -c Debug`
Expected: BUILD SUCCEEDED.

- [ ] **Step 3: Commit**

```bash
git add src/backend/tests/BigSchool.Integration.Tests/Auth/AuthEndpointTestBase.cs
git commit -m "test: añadir AuthEndpointTestBase con helpers de registro y BD

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 3: `RegisterTests` — EXHAUSTIVO (round-trip Argon2 + persistencia) + 409 + 400

**Files:**
- Create: `src/backend/tests/BigSchool.Integration.Tests/Auth/RegisterTests.cs`

- [ ] **Step 1: Escribir el fichero**

```csharp
using System.Net;
using System.Net.Http.Json;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Auth;

[Collection(IntegrationCollection.Name)]
public class RegisterTests : AuthEndpointTestBase
{
    public RegisterTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    // ── EXHAUSTIVO: response completa + persistencia + hash NO en claro + round-trip con login ──
    [Fact]
    public async Task Register_NewUser_ReturnsToken_PersistsHashedPassword_AndCanLogin()
    {
        var email = $"reg-{Guid.NewGuid():N}@test.com";

        var response = await RegisterRawAsync(email, DefaultPassword, "Ana Pérez");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<AuthResponse>>();
        env!.Errors.Should().BeEmpty();
        var dto = env.Data!;
        dto.AccessToken.Should().NotBeNullOrWhiteSpace();
        dto.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        dto.Email.Should().Be(email);
        dto.FullName.Should().Be("Ana Pérez");

        // El password se persiste HASHEADO (Argon2), nunca en claro.
        var storedHash = await ReadPasswordHashAsync(email);
        storedHash.Should().NotBeNullOrWhiteSpace();
        storedHash.Should().NotBe(DefaultPassword);

        // Round-trip REAL hash+verify: el mismo password permite hacer login (lo que un unitario mockeado no prueba).
        var login = await Factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = DefaultPassword });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409_EmailAlreadyExists()
    {
        var email = $"dup-{Guid.NewGuid():N}@test.com";
        await RegisterAsync(email);

        var response = await RegisterRawAsync(email, DefaultPassword, "Otro Nombre");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        env!.Errors.Should().Contain(e => e.Code == "EMAIL_ALREADY_EXISTS");
    }

    [Theory]
    [InlineData("not-an-email", "Secret123!", "Nombre")]   // email inválido
    [InlineData("ok@test.com", "short", "Nombre")]          // password < 8
    [InlineData("ok@test.com", "Secret123!", "")]           // fullName vacío
    public async Task Register_InvalidPayload_Returns400_ValidationError(string email, string password, string fullName)
    {
        var response = await RegisterRawAsync(email, password, fullName);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        env!.Errors.Should().Contain(e => e.Code == "VALIDATION_ERROR");
    }
}
```

- [ ] **Step 2: Ejecutar**

Run: `dotnet test src/backend/tests/BigSchool.Integration.Tests/BigSchool.Integration.Tests.csproj --filter "FullyQualifiedName~RegisterTests"`
Expected: PASS los 5 casos (1 exhaustivo + 1 conflicto + 3 del Theory).

- [ ] **Step 3: Commit**

```bash
git add src/backend/tests/BigSchool.Integration.Tests/Auth/RegisterTests.cs
git commit -m "test: cobertura E2E de POST /auth/register (round-trip Argon2, 409, 400)

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 4: `LoginTests` — éxito + token usable + 400 credenciales + 400 validación

**Files:**
- Create: `src/backend/tests/BigSchool.Integration.Tests/Auth/LoginTests.cs`

- [ ] **Step 1: Escribir el fichero**

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Auth;

[Collection(IntegrationCollection.Name)]
public class LoginTests : AuthEndpointTestBase
{
    public LoginTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    // El token emitido por login debe ser válido contra un endpoint protegido (JWT real, extremo a extremo).
    [Fact]
    public async Task Login_ValidCredentials_ReturnsUsableToken()
    {
        var email = $"login-{Guid.NewGuid():N}@test.com";
        await RegisterAsync(email);

        var login = await Factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = DefaultPassword });

        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = (await login.Content.ReadFromJsonAsync<ApiEnvelope<AuthResponse>>())!.Data!;
        dto.AccessToken.Should().NotBeNullOrWhiteSpace();
        dto.Email.Should().Be(email);

        // Usar el token en un endpoint [Authorize] → 200 (no 401): valida emisión + validación JwtBearer reales.
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", dto.AccessToken);
        var protected_ = await client.GetAsync("/api/v1/transactions/summary");
        protected_.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns400_InvalidCredentials()
    {
        var email = $"wrongpw-{Guid.NewGuid():N}@test.com";
        await RegisterAsync(email);

        var login = await Factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "WrongPassword9!" });

        login.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var env = await login.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        env!.Errors.Should().Contain(e => e.Code == "INVALID_CREDENTIALS");
    }

    // Usuario inexistente devuelve el MISMO error que password incorrecta (no filtra existencia → seguridad).
    [Fact]
    public async Task Login_NonExistentUser_Returns400_InvalidCredentials()
    {
        var login = await Factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { email = $"ghost-{Guid.NewGuid():N}@test.com", password = DefaultPassword });

        login.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var env = await login.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        env!.Errors.Should().Contain(e => e.Code == "INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Login_EmptyEmail_Returns400_ValidationError()
    {
        var login = await Factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { email = "", password = DefaultPassword });

        login.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var env = await login.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        env!.Errors.Should().Contain(e => e.Code == "VALIDATION_ERROR");
    }
}
```

- [ ] **Step 2: Ejecutar**

Run: `dotnet test src/backend/tests/BigSchool.Integration.Tests/BigSchool.Integration.Tests.csproj --filter "FullyQualifiedName~LoginTests"`
Expected: PASS los 4 tests.

- [ ] **Step 3: Commit**

```bash
git add src/backend/tests/BigSchool.Integration.Tests/Auth/LoginTests.cs
git commit -m "test: cobertura E2E de POST /auth/login (token usable, 400 credenciales, 400 validación)

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 5: `RefreshTests` — éxito + 401 sin token + 401 usuario eliminado

**Files:**
- Create: `src/backend/tests/BigSchool.Integration.Tests/Auth/RefreshTests.cs`

- [ ] **Step 1: Escribir el fichero**

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Auth;

[Collection(IntegrationCollection.Name)]
public class RefreshTests : AuthEndpointTestBase
{
    public RefreshTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    private HttpClient ClientWithBearer(string token)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Refresh_WithValidToken_ReturnsUsableToken()
    {
        var email = $"refresh-{Guid.NewGuid():N}@test.com";
        var registered = await RegisterAsync(email);

        var client = ClientWithBearer(registered.AccessToken);
        var response = await client.PostAsync("/api/v1/auth/refresh", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = (await response.Content.ReadFromJsonAsync<ApiEnvelope<AuthResponse>>())!.Data!;
        dto.AccessToken.Should().NotBeNullOrWhiteSpace();
        dto.Email.Should().Be(email);

        // El token refrescado también es válido contra un endpoint protegido.
        var client2 = ClientWithBearer(dto.AccessToken);
        var protected_ = await client2.GetAsync("/api/v1/transactions/summary");
        protected_.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_WithoutToken_Returns401()
    {
        var response = await Factory.CreateClient().PostAsync("/api/v1/auth/refresh", content: null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // Token de firma válida pero cuyo usuario ya no existe → el handler lanza Unauthorized (401).
    [Fact]
    public async Task Refresh_UserDeletedAfterTokenIssued_Returns401()
    {
        var email = $"deleted-{Guid.NewGuid():N}@test.com";
        var registered = await RegisterAsync(email);
        await DeleteUserAsync(email);

        var client = ClientWithBearer(registered.AccessToken);
        var response = await client.PostAsync("/api/v1/auth/refresh", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

- [ ] **Step 2: Ejecutar**

Run: `dotnet test src/backend/tests/BigSchool.Integration.Tests/BigSchool.Integration.Tests.csproj --filter "FullyQualifiedName~RefreshTests"`
Expected: PASS los 3 tests.

- [ ] **Step 3: Commit**

```bash
git add src/backend/tests/BigSchool.Integration.Tests/Auth/RefreshTests.cs
git commit -m "test: cobertura E2E de POST /auth/refresh (token válido, 401 sin token, 401 usuario eliminado)

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 6: `TransactionEndpointTestBase` — helpers de transactions

**Files:**
- Create: `src/backend/tests/BigSchool.Integration.Tests/Transactions/TransactionEndpointTestBase.cs`

- [ ] **Step 1: Escribir la base**

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

namespace BigSchool.Integration.Tests.Transactions;

/// <summary>Helpers específicos de los endpoints de transacciones.</summary>
public abstract class TransactionEndpointTestBase : IntegrationTestBase
{
    protected TransactionEndpointTestBase(MySqlDatabaseFixture fixture) : base(fixture) { }

    // Siembra un usuario real (FK Transactions.IdUser) directamente en BD.
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

    // Siembra una tasa para una fecha exacta → conversión determinista (sin red).
    protected async Task SeedExchangeRateAsync(Currency from, Currency to, decimal rate, DateOnly date)
    {
        await using var conn = new MySqlConnection(Fixture.ConnectionString);
        await conn.ExecuteAsync(
            @"INSERT INTO ExchangeRates (FromCurrency, ToCurrency, Rate, RateDate, Source, FetchedAt)
              VALUES (@From, @To, @Rate, @Date, 'test', @Now);",
            new { From = from.ToString(), To = to.ToString(), Rate, Date = date.ToDateTime(TimeOnly.MinValue), Now = DateTime.UtcNow });
    }

    // Cliente con JWT REAL del IJwtService del factory (pasa por la validación JwtBearer real).
    protected HttpClient AuthenticatedClient(int userId, string email)
    {
        var jwt = Factory.Services.GetRequiredService<IJwtService>().GenerateToken(userId, email);
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt.AccessToken);
        return client;
    }

    // Crea una transacción vía API real y devuelve su id (black-box para sembrar datos en GET/PUT/DELETE).
    protected async Task<int> CreateTransactionViaApiAsync(HttpClient client, object body)
    {
        var resp = await client.PostAsJsonAsync("/api/v1/transactions", body);
        resp.EnsureSuccessStatusCode();
        var env = await resp.Content.ReadFromJsonAsync<ApiEnvelope<TransactionResponse>>();
        return env!.Data!.IdTransaction;
    }

    protected async Task<int> CountActiveTransactionsAsync(int userId)
    {
        await using var conn = new MySqlConnection(Fixture.ConnectionString);
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Transactions WHERE IdUser = @userId AND IdStatus <> @deleted;",
            new { userId, deleted = (short)EntityStatus.Deleted });
    }

    protected async Task<short?> ReadStatusAsync(int idTransaction)
    {
        await using var conn = new MySqlConnection(Fixture.ConnectionString);
        return await conn.QuerySingleOrDefaultAsync<short?>(
            "SELECT IdStatus FROM Transactions WHERE IdTransaction = @id;", new { id = idTransaction });
    }

    // TransactionDto: Type/IdMainCategory STRING (JsonStringEnumConverter); fechas "yyyy-MM-dd".
    protected record TransactionResponse(
        int IdTransaction, string Type, string IdMainCategory, int? IdSubCategory,
        string? Description, string TransactionDate, decimal OriginalAmount, string OriginalCurrency,
        decimal ExchangeRate, decimal BaseAmount, string BaseCurrency, string RateDate);

    // TransactionListItemDto: Type short, IdMainCategory int, monedas string, fechas "yyyy-MM-dd".
    protected record TransactionListItemResponse(
        int IdTransaction, short Type, int IdMainCategory, int? IdSubCategory,
        string? Description, string TransactionDate, decimal OriginalAmount, string OriginalCurrency,
        decimal ExchangeRate, decimal BaseAmount, string BaseCurrency, string RateDate);

    protected record SummaryPayload(decimal TotalIncome, decimal TotalExpense, decimal Balance, string BaseCurrency);
    protected record MonthlyChartPayload(int Year, int Month, decimal Income, decimal Expense);
}
```

- [ ] **Step 2: Compilar**

Run: `dotnet build src/backend/tests/BigSchool.Integration.Tests/BigSchool.Integration.Tests.csproj -c Debug`
Expected: BUILD SUCCEEDED (el fichero viejo `TransactionsEndpointTests.cs` aún existe y compila).

- [ ] **Step 3: Commit**

```bash
git add src/backend/tests/BigSchool.Integration.Tests/Transactions/TransactionEndpointTestBase.cs
git commit -m "test: añadir TransactionEndpointTestBase con helpers de siembra y verificación

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 7: `PostTransactionTests` — exhaustivo + validaciones

**Files:**
- Create: `src/backend/tests/BigSchool.Integration.Tests/Transactions/PostTransactionTests.cs`

- [ ] **Step 1: Escribir el fichero**

```csharp
using System.Net;
using System.Net.Http.Json;
using BigSchool.Domain.Enums;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Transactions;

[Collection(IntegrationCollection.Name)]
public class PostTransactionTests : TransactionEndpointTestBase
{
    public PostTransactionTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Post_ExpenseEur_ReturnsFullDto_AndPersistsAllFields()
    {
        var (userId, email) = await SeedUserAsync(Currency.EUR);
        var client = AuthenticatedClient(userId, email);

        var response = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            type = (int)TransactionType.Expense,
            idMainCategory = (int)MainCategory.EssentialExpenses,
            idSubCategory = 1,
            description = "Compra semanal",
            transactionDate = "2026-03-15",
            amount = 185.50m
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<TransactionResponse>>();
        env!.Errors.Should().BeEmpty();
        var dto = env.Data!;
        dto.IdTransaction.Should().BeGreaterThan(0);
        dto.Type.Should().Be("Expense");
        dto.IdMainCategory.Should().Be("EssentialExpenses");
        dto.IdSubCategory.Should().Be(1);
        dto.Description.Should().Be("Compra semanal");
        dto.TransactionDate.Should().Be("2026-03-15");
        dto.OriginalAmount.Should().Be(185.50m);
        dto.OriginalCurrency.Should().Be("EUR");
        dto.ExchangeRate.Should().Be(1m);
        dto.BaseAmount.Should().Be(185.50m);
        dto.BaseCurrency.Should().Be("EUR");
        dto.RateDate.Should().Be("2026-03-15");

        await using var conn = new MySqlConnector.MySqlConnection(Fixture.ConnectionString);
        var row = await Dapper.SqlMapper.QuerySingleAsync(conn,
            @"SELECT Type, IdMainCategory, OriginalCurrency, BaseAmount, IdStatus
              FROM Transactions WHERE IdTransaction = @id;", new { id = dto.IdTransaction });
        ((short)row.Type).Should().Be((short)TransactionType.Expense);
        ((int)row.IdMainCategory).Should().Be((int)MainCategory.EssentialExpenses);
        ((string)row.OriginalCurrency).Should().Be("EUR");
        ((decimal)row.BaseAmount).Should().Be(185.50m);
        ((short)row.IdStatus).Should().Be((short)EntityStatus.Active);
    }

    [Fact]
    public async Task Post_IncomeUsd_UsesCachedRate_AndConsolidatesInBase()
    {
        var (userId, email) = await SeedUserAsync(Currency.EUR);
        var client = AuthenticatedClient(userId, email);
        await SeedExchangeRateAsync(Currency.USD, Currency.EUR, 0.90m, new DateOnly(2026, 4, 10));

        var response = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            type = (int)TransactionType.Income,
            idMainCategory = (int)MainCategory.Other,
            description = "Pago USD",
            transactionDate = "2026-04-10",
            amount = 100.00m,
            currency = "USD"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = (await response.Content.ReadFromJsonAsync<ApiEnvelope<TransactionResponse>>())!.Data!;
        dto.OriginalAmount.Should().Be(100.00m);
        dto.OriginalCurrency.Should().Be("USD");
        dto.ExchangeRate.Should().Be(0.90m);
        dto.BaseAmount.Should().Be(90.00m);
        dto.BaseCurrency.Should().Be("EUR");
    }

    [Fact]
    public async Task Post_WithoutToken_Returns401()
    {
        var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            type = 1, idMainCategory = 1, transactionDate = "2026-03-15", amount = 10m
        });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_AmountZero_Returns400_WithValidationError()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);

        var response = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            type = (int)TransactionType.Expense,
            idMainCategory = (int)MainCategory.EssentialExpenses,
            transactionDate = "2026-03-15",
            amount = 0m
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        env!.Errors.Should().Contain(e => e.Code == "VALIDATION_ERROR" && e.Field == "Amount");
    }

    [Fact]
    public async Task Post_InvalidMainCategory_Returns400()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);

        var response = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            type = (int)TransactionType.Expense,
            idMainCategory = 999,
            transactionDate = "2026-03-15",
            amount = 50m
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        env!.Errors.Should().Contain(e => e.Code == "VALIDATION_ERROR");
    }
}
```

- [ ] **Step 2: Ejecutar**

Run: `dotnet test src/backend/tests/BigSchool.Integration.Tests/BigSchool.Integration.Tests.csproj --filter "FullyQualifiedName~PostTransactionTests"`
Expected: PASS los 5 tests.

- [ ] **Step 3: Commit**

```bash
git add src/backend/tests/BigSchool.Integration.Tests/Transactions/PostTransactionTests.cs
git commit -m "test: cobertura E2E exhaustiva del POST /transactions (campos, conversión, 401, 400)

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 8: `GetTransactionByIdTests`

**Files:**
- Create: `src/backend/tests/BigSchool.Integration.Tests/Transactions/GetTransactionByIdTests.cs`

- [ ] **Step 1: Escribir el fichero**

```csharp
using System.Net;
using System.Net.Http.Json;
using BigSchool.Domain.Enums;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Transactions;

[Collection(IntegrationCollection.Name)]
public class GetTransactionByIdTests : TransactionEndpointTestBase
{
    public GetTransactionByIdTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetById_ExistingOwnTransaction_ReturnsListItemDto()
    {
        var (userId, email) = await SeedUserAsync(Currency.EUR);
        var client = AuthenticatedClient(userId, email);
        var id = await CreateTransactionViaApiAsync(client, new
        {
            type = (int)TransactionType.Expense,
            idMainCategory = (int)MainCategory.EssentialExpenses,
            idSubCategory = 1,
            description = "Compra",
            transactionDate = "2026-03-20",
            amount = 42.30m
        });

        var response = await client.GetAsync($"/api/v1/transactions/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = (await response.Content.ReadFromJsonAsync<ApiEnvelope<TransactionListItemResponse>>())!.Data!;
        dto.IdTransaction.Should().Be(id);
        dto.Type.Should().Be((short)TransactionType.Expense);
        dto.IdMainCategory.Should().Be((int)MainCategory.EssentialExpenses);
        dto.OriginalAmount.Should().Be(42.30m);
        dto.OriginalCurrency.Should().Be("EUR");
        dto.TransactionDate.Should().Be("2026-03-20");
    }

    [Fact]
    public async Task GetById_NonExisting_Returns404()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);

        var response = await client.GetAsync("/api/v1/transactions/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        env!.Errors.Should().Contain(e => e.Code == "ENTITY_NOT_FOUND");
    }

    [Fact]
    public async Task GetById_WithoutToken_Returns401()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/v1/transactions/1");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

- [ ] **Step 2: Ejecutar**

Run: `dotnet test src/backend/tests/BigSchool.Integration.Tests/BigSchool.Integration.Tests.csproj --filter "FullyQualifiedName~GetTransactionByIdTests"`
Expected: PASS los 3 tests.

- [ ] **Step 3: Commit**

```bash
git add src/backend/tests/BigSchool.Integration.Tests/Transactions/GetTransactionByIdTests.cs
git commit -m "test: cobertura E2E de GET /transactions/{id} (happy, 404, 401)

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 9: `GetTransactionsTests` — lista, paginación y filtros

**Files:**
- Create: `src/backend/tests/BigSchool.Integration.Tests/Transactions/GetTransactionsTests.cs`

- [ ] **Step 1: Escribir el fichero**

```csharp
using System.Net;
using System.Net.Http.Json;
using BigSchool.Domain.Enums;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Transactions;

[Collection(IntegrationCollection.Name)]
public class GetTransactionsTests : TransactionEndpointTestBase
{
    public GetTransactionsTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    private async Task SeedSampleSetAsync(HttpClient client)
    {
        await CreateTransactionViaApiAsync(client, new { type = (int)TransactionType.Expense, idMainCategory = (int)MainCategory.EssentialExpenses, transactionDate = "2026-03-01", amount = 10m });
        await CreateTransactionViaApiAsync(client, new { type = (int)TransactionType.Expense, idMainCategory = (int)MainCategory.EssentialExpenses, transactionDate = "2026-03-15", amount = 20m });
        await CreateTransactionViaApiAsync(client, new { type = (int)TransactionType.Expense, idMainCategory = (int)MainCategory.Luxuries, transactionDate = "2026-03-20", amount = 30m });
        await CreateTransactionViaApiAsync(client, new { type = (int)TransactionType.Income, idMainCategory = (int)MainCategory.Salary, transactionDate = "2026-04-01", amount = 1500m });
    }

    [Fact]
    public async Task Get_NoFilters_ReturnsAll_WithPaginationMeta()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        await SeedSampleSetAsync(client);

        var response = await client.GetAsync("/api/v1/transactions?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<TransactionListItemResponse>>>();
        env!.Data!.Should().HaveCount(4);
        env.Meta!.Page.Should().Be(1);
        env.Meta.PageSize.Should().Be(20);
        env.Meta.TotalCount.Should().Be(4);
        env.Data![0].TransactionDate.Should().Be("2026-04-01");  // orden fecha desc
    }

    [Fact]
    public async Task Get_FilterByTypeExpense_ReturnsOnlyExpenses()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        await SeedSampleSetAsync(client);

        var response = await client.GetAsync($"/api/v1/transactions?type={(int)TransactionType.Expense}");

        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<TransactionListItemResponse>>>();
        env!.Data!.Should().HaveCount(3);
        env.Data!.Should().OnlyContain(t => t.Type == (short)TransactionType.Expense);
    }

    [Fact]
    public async Task Get_FilterByDateRange_ReturnsOnlyInRange()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        await SeedSampleSetAsync(client);

        var response = await client.GetAsync("/api/v1/transactions?from=2026-03-01&to=2026-03-31");

        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<TransactionListItemResponse>>>();
        env!.Data!.Should().HaveCount(3);
        env.Data!.Should().OnlyContain(t => t.TransactionDate.StartsWith("2026-03"));
    }

    [Fact]
    public async Task Get_SecondPage_RespectsPageSize()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        await SeedSampleSetAsync(client);

        var response = await client.GetAsync("/api/v1/transactions?page=2&pageSize=2");

        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<TransactionListItemResponse>>>();
        env!.Data!.Should().HaveCount(2);
        env.Meta!.TotalCount.Should().Be(4);
        env.Meta.TotalPages.Should().Be(2);
    }

    [Fact]
    public async Task Get_WithoutToken_Returns401()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/v1/transactions");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

- [ ] **Step 2: Ejecutar**

Run: `dotnet test src/backend/tests/BigSchool.Integration.Tests/BigSchool.Integration.Tests.csproj --filter "FullyQualifiedName~GetTransactionsTests"`
Expected: PASS los 5 tests.

- [ ] **Step 3: Commit**

```bash
git add src/backend/tests/BigSchool.Integration.Tests/Transactions/GetTransactionsTests.cs
git commit -m "test: cobertura E2E de GET /transactions (lista, paginación, filtros, 401)

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 10: `PutTransactionTests`

**Files:**
- Create: `src/backend/tests/BigSchool.Integration.Tests/Transactions/PutTransactionTests.cs`

- [ ] **Step 1: Escribir el fichero**

```csharp
using System.Net;
using System.Net.Http.Json;
using BigSchool.Domain.Enums;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Transactions;

[Collection(IntegrationCollection.Name)]
public class PutTransactionTests : TransactionEndpointTestBase
{
    public PutTransactionTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Put_ExistingTransaction_UpdatesFields_AndPersists()
    {
        var (userId, email) = await SeedUserAsync(Currency.EUR);
        var client = AuthenticatedClient(userId, email);
        var id = await CreateTransactionViaApiAsync(client, new
        {
            type = (int)TransactionType.Expense,
            idMainCategory = (int)MainCategory.EssentialExpenses,
            description = "Original",
            transactionDate = "2026-03-10",
            amount = 50m
        });

        var response = await client.PutAsJsonAsync($"/api/v1/transactions/{id}", new
        {
            type = (int)TransactionType.Expense,
            idMainCategory = (int)MainCategory.Luxuries,
            description = "Modificado",
            transactionDate = "2026-03-12",
            amount = 75.25m
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = (await response.Content.ReadFromJsonAsync<ApiEnvelope<TransactionResponse>>())!.Data!;
        dto.IdTransaction.Should().Be(id);
        dto.IdMainCategory.Should().Be("Luxuries");
        dto.Description.Should().Be("Modificado");
        dto.OriginalAmount.Should().Be(75.25m);
        dto.TransactionDate.Should().Be("2026-03-12");
        (await CountActiveTransactionsAsync(userId)).Should().Be(1);  // update, no duplica
    }

    [Fact]
    public async Task Put_NonExisting_Returns404()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);

        var response = await client.PutAsJsonAsync("/api/v1/transactions/999999", new
        {
            type = (int)TransactionType.Expense,
            idMainCategory = (int)MainCategory.EssentialExpenses,
            transactionDate = "2026-03-12",
            amount = 10m
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        env!.Errors.Should().Contain(e => e.Code == "ENTITY_NOT_FOUND");
    }

    [Fact]
    public async Task Put_WithoutToken_Returns401()
    {
        var client = Factory.CreateClient();
        var response = await client.PutAsJsonAsync("/api/v1/transactions/1", new
        {
            type = 1, idMainCategory = 1, transactionDate = "2026-03-12", amount = 10m
        });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

- [ ] **Step 2: Ejecutar**

Run: `dotnet test src/backend/tests/BigSchool.Integration.Tests/BigSchool.Integration.Tests.csproj --filter "FullyQualifiedName~PutTransactionTests"`
Expected: PASS los 3 tests.

- [ ] **Step 3: Commit**

```bash
git add src/backend/tests/BigSchool.Integration.Tests/Transactions/PutTransactionTests.cs
git commit -m "test: cobertura E2E de PUT /transactions/{id} (update, 404, 401)

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 11: `DeleteTransactionTests` — soft-delete

**Files:**
- Create: `src/backend/tests/BigSchool.Integration.Tests/Transactions/DeleteTransactionTests.cs`

- [ ] **Step 1: Escribir el fichero**

```csharp
using System.Net;
using BigSchool.Domain.Enums;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Transactions;

[Collection(IntegrationCollection.Name)]
public class DeleteTransactionTests : TransactionEndpointTestBase
{
    public DeleteTransactionTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Delete_ExistingTransaction_SoftDeletes_AndHidesFromReads()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        var id = await CreateTransactionViaApiAsync(client, new
        {
            type = (int)TransactionType.Expense,
            idMainCategory = (int)MainCategory.EssentialExpenses,
            transactionDate = "2026-03-10",
            amount = 50m
        });

        var response = await client.DeleteAsync($"/api/v1/transactions/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadStatusAsync(id)).Should().Be((short)EntityStatus.Deleted);  // soft-delete: la fila persiste
        (await CountActiveTransactionsAsync(userId)).Should().Be(0);
        var getAfter = await client.GetAsync($"/api/v1/transactions/{id}");
        getAfter.StatusCode.Should().Be(HttpStatusCode.NotFound);               // query filter la oculta
    }

    [Fact]
    public async Task Delete_NonExisting_Returns404()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);

        var response = await client.DeleteAsync("/api/v1/transactions/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_WithoutToken_Returns401()
    {
        var client = Factory.CreateClient();
        var response = await client.DeleteAsync("/api/v1/transactions/1");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

- [ ] **Step 2: Ejecutar**

Run: `dotnet test src/backend/tests/BigSchool.Integration.Tests/BigSchool.Integration.Tests.csproj --filter "FullyQualifiedName~DeleteTransactionTests"`
Expected: PASS los 3 tests.

- [ ] **Step 3: Commit**

```bash
git add src/backend/tests/BigSchool.Integration.Tests/Transactions/DeleteTransactionTests.cs
git commit -m "test: cobertura E2E de DELETE /transactions/{id} (soft-delete, 404, 401)

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 12: `GetTransactionSummaryTests` — migrar y borrar fichero antiguo

**Files:**
- Create: `src/backend/tests/BigSchool.Integration.Tests/Transactions/GetTransactionSummaryTests.cs`
- Delete: `src/backend/tests/BigSchool.Integration.Tests/Transactions/TransactionsEndpointTests.cs`

- [ ] **Step 1: Escribir el nuevo fichero del summary**

```csharp
using System.Net;
using System.Net.Http.Json;
using BigSchool.Domain.Enums;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Transactions;

[Collection(IntegrationCollection.Name)]
public class GetTransactionSummaryTests : TransactionEndpointTestBase
{
    public GetTransactionSummaryTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Summary_AfterIncomeAndExpense_ConsolidatesBalanceInBase()
    {
        var (userId, email) = await SeedUserAsync(Currency.EUR);
        var client = AuthenticatedClient(userId, email);
        await CreateTransactionViaApiAsync(client, new { type = (int)TransactionType.Income, idMainCategory = (int)MainCategory.Salary, transactionDate = "2026-03-01", amount = 1500m });
        await CreateTransactionViaApiAsync(client, new { type = (int)TransactionType.Expense, idMainCategory = (int)MainCategory.EssentialExpenses, transactionDate = "2026-03-05", amount = 200m });

        var response = await client.GetAsync("/api/v1/transactions/summary");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = (await response.Content.ReadFromJsonAsync<ApiEnvelope<SummaryPayload>>())!.Data!;
        dto.TotalIncome.Should().Be(1500m);
        dto.TotalExpense.Should().Be(200m);
        dto.Balance.Should().Be(1300m);
        dto.BaseCurrency.Should().Be("EUR");
    }

    [Fact]
    public async Task Summary_WithoutToken_Returns401()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/v1/transactions/summary");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

- [ ] **Step 2: Borrar el fichero monolítico antiguo**

```bash
git rm src/backend/tests/BigSchool.Integration.Tests/Transactions/TransactionsEndpointTests.cs
```

- [ ] **Step 3: Ejecutar**

Run: `dotnet test src/backend/tests/BigSchool.Integration.Tests/BigSchool.Integration.Tests.csproj --filter "FullyQualifiedName~GetTransactionSummaryTests"`
Expected: PASS los 2 tests; `TransactionsEndpointTests` ya no existe.

- [ ] **Step 4: Commit**

```bash
git add src/backend/tests/BigSchool.Integration.Tests/Transactions/GetTransactionSummaryTests.cs
git commit -m "test: migrar summary a fichero propio y eliminar TransactionsEndpointTests monolítico

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 13: `GetMonthlyChartTests`

**Files:**
- Create: `src/backend/tests/BigSchool.Integration.Tests/Transactions/GetMonthlyChartTests.cs`

- [ ] **Step 1: Escribir el fichero**

```csharp
using System.Net;
using System.Net.Http.Json;
using BigSchool.Domain.Enums;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Transactions;

[Collection(IntegrationCollection.Name)]
public class GetMonthlyChartTests : TransactionEndpointTestBase
{
    public GetMonthlyChartTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task MonthlyChart_GroupsByMonth_WithIncomeAndExpense()
    {
        var (userId, email) = await SeedUserAsync(Currency.EUR);
        var client = AuthenticatedClient(userId, email);
        await CreateTransactionViaApiAsync(client, new { type = (int)TransactionType.Income, idMainCategory = (int)MainCategory.Salary, transactionDate = "2026-03-01", amount = 1500m });
        await CreateTransactionViaApiAsync(client, new { type = (int)TransactionType.Expense, idMainCategory = (int)MainCategory.EssentialExpenses, transactionDate = "2026-03-10", amount = 300m });
        await CreateTransactionViaApiAsync(client, new { type = (int)TransactionType.Expense, idMainCategory = (int)MainCategory.Luxuries, transactionDate = "2026-04-05", amount = 120m });

        var response = await client.GetAsync("/api/v1/transactions/monthly-chart?year=2026");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var points = (await response.Content.ReadFromJsonAsync<ApiEnvelope<List<MonthlyChartPayload>>>())!.Data!;
        var march = points.Single(p => p.Month == 3);
        march.Year.Should().Be(2026);
        march.Income.Should().Be(1500m);
        march.Expense.Should().Be(300m);
        var april = points.Single(p => p.Month == 4);
        april.Income.Should().Be(0m);
        april.Expense.Should().Be(120m);
    }

    [Fact]
    public async Task MonthlyChart_WithoutToken_Returns401()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/v1/transactions/monthly-chart?year=2026");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

> **Nota:** ambos meses sembrados (marzo, abril) tienen al menos un movimiento, así que el test no asume relleno de meses vacíos por parte del handler.

- [ ] **Step 2: Ejecutar**

Run: `dotnet test src/backend/tests/BigSchool.Integration.Tests/BigSchool.Integration.Tests.csproj --filter "FullyQualifiedName~GetMonthlyChartTests"`
Expected: PASS los 2 tests.

- [ ] **Step 3: Commit**

```bash
git add src/backend/tests/BigSchool.Integration.Tests/Transactions/GetMonthlyChartTests.cs
git commit -m "test: cobertura E2E de GET /transactions/monthly-chart (agrupación, 401)

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 14: Fijar las normas en `AGENTS.md`

**Files:**
- Modify: `src/backend/AGENTS.md` (sección `### Testing`)

- [ ] **Step 1: Reemplazar la sección Testing por la versión ampliada**

Sustituir el bloque actual `### Testing` (líneas ~124-129) por:

```markdown
### Testing
- **Unitarios**: Domain y Application (mocking de Infrastructure)
- **Integración (E2E)**: BD MySQL real de docker-compose (BD dedicada `bigschool_test` migrada por EF, fixture `ICollectionFixture`) + `WebApplicationFactory<Program>` ejercitando el **pipeline completo** (JwtBearer, MediatR, FluentValidation, EF commands, Dapper queries, middleware de excepciones); WireMock.Net para fakear servidores HTTP externos.
- Framework: xUnit + FluentAssertions + Moq + WireMock.Net + MySqlConnector/Dapper + Microsoft.AspNetCore.Mvc.Testing
- Cobertura mínima: 80% en Domain y Application
- **Un fichero de test por clase bajo prueba**, nombrado `{ClaseBajoPrueba}Tests.cs`. No agrupar varias clases/handlers en un mismo fichero.

#### Reglas obligatorias de tests de integración E2E (Definition of Done por endpoint)
Todo endpoint nuevo o modificado DEBE acompañarse de tests E2E que cumplan:
1. **Un fichero por endpoint**, nombrado `{Verbo|Acción}{Recurso}Tests.cs` (p. ej. `RegisterTests`, `PostTransactionTests`, `GetTransactionsTests`), bajo carpeta por feature (`Auth/`, `Transactions/`). El plumbing común (ciclo de vida del factory, reset de BD, tipos de deserialización del envelope) vive en `IntegrationTestBase`; los helpers de cada feature en una base específica (`AuthEndpointTestBase`, `TransactionEndpointTestBase`).
2. **Al menos un test EXHAUSTIVO por feature** sobre la operación principal (POST/register), que valide:
   - **Request real serializado** (no se invoca el handler directamente): caza fallos de binding/serialización (enums string vía `JsonStringEnumConverter`, `DateOnly`, conversores Dapper como `DateOnlyTypeHandler`).
   - **Cada campo de la response** (contrato del frontend), incluida la forma serializada de enums y fechas.
   - **Persistencia física en BD** (consulta Dapper directa) verificando las columnas escritas.
3. **Foco en seguridad/auth (crítico)**: para flujos de credenciales, ejercitar el **hashing real** (round-trip register→login con la misma password) y el **JWT real** (token emitido usable contra un endpoint `[Authorize]`); nunca mockear `IPasswordHasher`/`IJwtService` en E2E. Verificar que el password se persiste hasheado (nunca en claro) y que usuario inexistente y password incorrecta devuelven el **mismo** error (no filtrar existencia).
4. **Tests de validación HTTP** de todos los códigos que el endpoint puede devolver: `401` (sin token en `[Authorize]`), `400` (`VALIDATION_ERROR` con `Field`; `INVALID_CREDENTIALS`), `404` (`ENTITY_NOT_FOUND`), `409` (`EMAIL_ALREADY_EXISTS` / `ConflictException`) cuando aplique.
5. **Operaciones de borrado**: verificar el **soft-delete** (la fila persiste con `IdStatus=Deleted`) y que las lecturas posteriores la ocultan (404).
6. **Deterministas**: para conversión multimoneda, sembrar la tasa en `ExchangeRates` con `RateDate = transactionDate` (nunca depender de la red); para servicios HTTP externos, WireMock.Net.
```

- [ ] **Step 2: Verificar el diff**

Run: `git diff src/backend/AGENTS.md`
Expected: solo cambia la sección Testing.

- [ ] **Step 3: Commit**

```bash
git add src/backend/AGENTS.md
git commit -m "docs: fijar reglas obligatorias de tests de integración E2E en AGENTS.md

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 15: Verificación global y PR

- [ ] **Step 1: Ejecutar TODA la suite de integración**

Run: `dotnet test src/backend/tests/BigSchool.Integration.Tests/BigSchool.Integration.Tests.csproj`
Expected: PASS todos — Auth (Register 5 + Login 4 + Refresh 3) + Transactions (Post 5 + GetById 3 + GetList 5 + Put 3 + Delete 3 + Summary 2 + MonthlyChart 2) + los de Persistence/Services existentes.

- [ ] **Step 2: Ejecutar la solución completa**

Run: `dotnet test src/backend/Backend.sln`
Expected: PASS toda la suite (Domain + Application + Integration).

- [ ] **Step 3: Push + PR**

```bash
git push -u origin feature/backend-integration-tests-e2e
gh pr create --base develop --title "test: tests de integración E2E (Auth con foco + Transactions) + normas en AGENTS.md" --body "Tests E2E por endpoint sobre el pipeline real. Foco en AuthController (round-trip Argon2, JWT real usable, 409/400/401). Completa la cobertura de TransactionsController y fija las reglas E2E en src/backend/AGENTS.md."
```

---

## Self-Review

**1. Cobertura de los 4 puntos del usuario:**
- Punto 1 (más E2E): Tasks 3-5 (Auth) + 7-13 (Transactions) cubren los 11 endpoints. ✅
- Punto 2 (fichero-por-endpoint + 1 exhaustivo + validaciones): bases compartidas (1,2,6) + exhaustivos en Register (Task 3) y POST (Task 7) + validaciones 401/400/404/409 distribuidas. ✅
- **Foco en Auth (corrección del usuario)**: Auth va primero (Tasks 3-5), con round-trip de hashing real, JWT usable, 409 y 400 de credenciales. ✅
- Punto 3 (solo Plan): este documento es solo un Plan. ✅
- Punto 4 (cumplimiento futuro): Task 14 actualiza AGENTS.md, con sub-punto específico de seguridad/auth. ✅

**2. Placeholders:** ninguno; cada paso lleva código completo y comando con resultado esperado.

**3. Consistencia de tipos:** envelope (`ApiEnvelope<T>`, `ApiErrorPayload`, `MetaPayload`) en `IntegrationTestBase` (Task 1). `AuthResponse` en `AuthEndpointTestBase` (Task 2). Records de transacción en `TransactionEndpointTestBase` (Task 6). Códigos de error verificados contra el código real: `EMAIL_ALREADY_EXISTS` (409), `INVALID_CREDENTIALS` (400), `VALIDATION_ERROR` (400), `ENTITY_NOT_FOUND` (404). Valores de enum coinciden con los del dominio.
