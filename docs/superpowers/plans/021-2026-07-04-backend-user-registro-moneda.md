# Backend — User / Registro: moneda obligatoria + perfil Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cerrar dos huecos del módulo **Auth**: (#1) `BaseCurrency` **obligatoria** en el registro (hoy todo usuario cae en EUR); (#2/A) `GET /users/me` (Dapper) y `PUT /users/me` (EF, re-hash Argon2 si cambia password) en un `UsersController` bajo `/api/v1/users`. `email` y `baseCurrency` son **inmutables** tras el registro.

**Architecture:** Todo dentro del módulo Auth (`User` es su AR) → sin cruces de frontera (`ModuleBoundaryTests` no cambia). Registro: se añade `Currency BaseCurrency` al `RegisterCommand` (los enums viajan como nombre string vía `JsonStringEnumConverter`), validado con `IsInEnum` (omitir → `default(Currency)=0` → inválido → 400). Perfil: métodos de dominio `User.UpdateProfile(fullName)` / `User.ChangePassword(hash, salt)`; lectura Dapper, escritura EF con re-hash solo si llega `password`.

**Tech Stack:** .NET 8, C#, CQRS (MediatR), EF Core + MySQL 8, Dapper, FluentValidation, Argon2 (`IPasswordHasher`), xUnit + FluentAssertions + Moq + `WebApplicationFactory`.

---

## Convenciones y contexto (LEER ANTES DE EMPEZAR)

> **BASE:** develop con estructura modular (018) + paginación (019). Rama de trabajo `feature/007-010-backend-features`; cada tarea = rama `feature/021-user-taskN` + PR.

Piezas verificadas en develop:
- `RegisterCommand(string Email, string Password, string FullName)` + `RegisterCommandValidator` + `RegisterCommandHandler` — `BigSchool.Application.Auth.Commands.Register`. El handler ya hace `User.Create(email, hash, salt, fullName)` (falta pasar la moneda).
- `User.Create(email, hash, salt, fullName, Currency baseCurrency = Currency.EUR)` ya **acepta** la moneda; tiene `UpdateLastLogin()`, propiedades `FullName`, `BaseCurrency`, `LastLoginDate`, `UpdatedAt` (todas `private set`) — `BigSchool.Domain.Auth.Entities`.
- `Currency` (EUR/USD/GBP/CHF/JPY) — `BigSchool.Domain.SharedKernel.Enums`. `NotFoundException : DomainException` — `BigSchool.Domain.SharedKernel.Exceptions`.
- `IUserRepository : IRepository<User,int>` (`GetByIdAsync`, `GetByEmailAsync`, `ExistsWithEmailAsync`, `UnitOfWork`) — `BigSchool.Application.Auth.Interfaces.Repositories`. `IPasswordHasher.HashPassword(pwd) → (hash, salt)` — `BigSchool.Application.Auth.Interfaces.Services`.
- `IDbConnectionFactory` — `BigSchool.Application.SharedKernel.Interfaces`; `EntityStatus.Deleted` — `BigSchool.Domain.SharedKernel.Enums`. `BaseCurrency` se persiste CHAR(3) (Dapper lo lee como **string**).
- Controller `[Authorize]` con userId: `CurrentUser.GetId(User, _encryptor)` + `IUserIdEncryptor` (`BigSchool.Application.Auth.Interfaces.Services`), patrón `TransactionsController`.
- E2E: `AuthEndpointTestBase.RegisterRawAsync(email, password, fullName)` postea `{email, password, fullName}` (⚠ hay que añadir `baseCurrency`); `AuthResponse(AccessToken, ExpiresAt, Email, FullName)`; `ApiEnvelope<T>`/`MetaPayload` en `IntegrationTestBase`.

Recetas: **BUILD** (`dotnet build`, cwd `src/backend`); **UNIT** (`dotnet test tests/BigSchool.Domain.Tests`, `.Application.Tests`); **INTEGRATION** (MySQL de `infra/docker-compose.yml`; `BIGSCHOOL_TEST_MYSQL` si difieren credenciales; `dotnet test tests/BigSchool.Integration.Tests`); **FULL** = todo.

---

## Task 1: #1 — Moneda obligatoria en el registro (cambio de contrato rompedor)

**Files:**
- Modify: `src/BigSchool.Application/Auth/Commands/Register/RegisterCommand.cs`, `RegisterCommandValidator.cs`, `RegisterCommandHandler.cs`
- Modify: `tests/BigSchool.Integration.Tests/Auth/AuthEndpointTestBase.cs` (helper con `baseCurrency`)
- Modify: `tests/BigSchool.Integration.Tests/Auth/RegisterTests.cs` (nuevos casos + ajuste)
- Test: `tests/BigSchool.Application.Tests/Validators/Auth/RegisterCommandValidatorTests.cs` (crear o extender)

- [x] **Step 1: Test del validator (falla)**

`RegisterCommandValidatorTests.cs` (añade estos casos al fichero existente; sigue su estilo real: `TestValidate`/`ShouldHaveValidationErrorFor`, no `.Validate(...).IsValid`):
```csharp
    [Fact]
    public void Validate_ValidBaseCurrency_NoError()
    {
        var result = _validator.TestValidate(new RegisterCommand("test@email.com", "P@ssw0rd!", "John Doe", Currency.USD));
        result.ShouldNotHaveValidationErrorFor(x => x.BaseCurrency);
    }

    [Fact]
    public void Validate_OmittedBaseCurrency_HasError()
    {
        var result = _validator.TestValidate(new RegisterCommand("test@email.com", "P@ssw0rd!", "John Doe", default));
        result.ShouldHaveValidationErrorFor(x => x.BaseCurrency);
    }

    [Fact]
    public void Validate_BaseCurrencyOutOfEnum_HasError()
    {
        var result = _validator.TestValidate(new RegisterCommand("test@email.com", "P@ssw0rd!", "John Doe", (Currency)999));
        result.ShouldHaveValidationErrorFor(x => x.BaseCurrency);
    }
```
> Añade `using BigSchool.Domain.SharedKernel.Enums;` a las `using` del fichero. Los `[Fact]` YA existentes (`Validate_ValidCommand_NoErrors`, etc.) dejan de compilar en cuanto `RegisterCommand` pase a tener 4 parámetros (Step 2) — hay que pasarles también `Currency.EUR` en ese mismo Step, no antes.

Run: `dotnet test tests/BigSchool.Application.Tests --filter RegisterCommandValidatorTests` → FAIL (el command aún no tiene `BaseCurrency`).

- [x] **Step 2: `RegisterCommand` + Validator + Handler**

`RegisterCommand.cs`:
```csharp
using BigSchool.Application.Auth.DTOs;
using BigSchool.Domain.SharedKernel.Enums;
using MediatR;

namespace BigSchool.Application.Auth.Commands.Register;

public record RegisterCommand(string Email, string Password, string FullName, Currency BaseCurrency) : IRequest<AuthResponseDto>;
```
En `RegisterCommandValidator.cs`, añade la regla (con `using BigSchool.Domain.SharedKernel.Enums;` si hiciera falta para `IsInEnum` no es necesario, es de FluentValidation):
```csharp
        RuleFor(x => x.BaseCurrency)
            .IsInEnum().WithMessage("La moneda base es obligatoria y debe ser válida.");
```
En `RegisterCommandHandler.cs`, pasa la moneda al factory:
```csharp
        var user = User.Create(request.Email, hash, salt, request.FullName, request.BaseCurrency);
```

- [x] **Step 3: Ajustar el helper de E2E al nuevo contrato**

En `AuthEndpointTestBase.cs`, añade `baseCurrency` (default EUR para no romper los tests existentes):
```csharp
    protected Task<HttpResponseMessage> RegisterRawAsync(string email, string password, string fullName, string baseCurrency = "EUR")
        => Factory.CreateClient().PostAsJsonAsync("/api/v1/auth/register",
            new { email, password, fullName, baseCurrency });
```
`RegisterAsync` no cambia (usa el default EUR). Los otros tests de Auth (`LoginTests`, `RefreshTests`) siguen verdes.
> Busca además seeds/utilidades que registren usuarios vía API (`grep -rn "auth/register" infra scripts tests`) y añádeles `baseCurrency`. Los seeds que usan `User.Create(...)` sin moneda siguen válidos (default EUR).

- [x] **Step 4: E2E de registro (moneda persiste / obligatoria)**

En `RegisterTests.cs` añade:
```csharp
    [Fact]
    public async Task Register_WithBaseCurrencyUsd_PersistsUsd()
    {
        var email = $"cur-{Guid.NewGuid():N}@test.com";
        var resp = await RegisterRawAsync(email, DefaultPassword, "Ada", "USD");
        resp.EnsureSuccessStatusCode();

        await using var conn = new MySqlConnector.MySqlConnection(Fixture.ConnectionString);
        var currency = await Dapper.SqlMapper.QuerySingleAsync<string>(conn,
            "SELECT BaseCurrency FROM Users WHERE Email=@email;", new { email });
        currency.Should().Be("USD");
    }

    [Fact]
    public async Task Register_MissingBaseCurrency_Returns400_ValidationError()
    {
        var email = $"nocur-{Guid.NewGuid():N}@test.com";
        var resp = await Factory.CreateClient().PostAsJsonAsync("/api/v1/auth/register",
            new { email, password = DefaultPassword, fullName = "Ada" }); // sin baseCurrency
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }
```
> Revisa que el test exhaustivo existente de `RegisterTests` (si asertaba `BaseCurrency`) siga coherente: ahora `RegisterRawAsync` envía EUR por defecto.

- [x] **Step 5: Verde + Commit**

Run: FULL. Luego:
```bash
git add -A && git commit -m "feat(auth): baseCurrency obligatoria en el registro (IsInEnum)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 2: #2/A lectura — `GET /users/me` (Dapper)

**Files:**
- Create: `src/BigSchool.Application/Auth/DTOs/UserProfileDto.cs`
- Create: `src/BigSchool.Application/Auth/Queries/GetMe/{GetMeQuery,GetMeQueryHandler}.cs`
- Create: `src/BigSchool.WebApi/Controllers/Auth/UsersController.cs`
- Test: `tests/BigSchool.Integration.Tests/Auth/GetMeTests.cs`

- [ ] **Step 1: DTO + Query + Handler (Dapper)**

`UserProfileDto.cs`:
```csharp
namespace BigSchool.Application.Auth.DTOs;

public record UserProfileDto(int IdUser, string Email, string FullName, string BaseCurrency, DateTime? LastLoginDate);
```
`GetMeQuery.cs`:
```csharp
using BigSchool.Application.Auth.DTOs;
using MediatR;

namespace BigSchool.Application.Auth.Queries.GetMe;

public record GetMeQuery(int IdUser) : IRequest<UserProfileDto?>;
```
`GetMeQueryHandler.cs`:
```csharp
using BigSchool.Application.Auth.DTOs;
using BigSchool.Application.SharedKernel.Interfaces;
using BigSchool.Domain.SharedKernel.Enums;
using Dapper;
using MediatR;

namespace BigSchool.Application.Auth.Queries.GetMe;

public class GetMeQueryHandler : IRequestHandler<GetMeQuery, UserProfileDto?>
{
    private readonly IDbConnectionFactory _dbFactory;

    public GetMeQueryHandler(IDbConnectionFactory dbFactory) => _dbFactory = dbFactory;

    private const string GETME_QUERY = @"SELECT IdUser, Email, FullName, BaseCurrency, LastLoginDate
                                         FROM Users
                                         WHERE IdUser = @IdUser AND IdStatus <> @StatusDeleted
                                         LIMIT 1;";

    public async Task<UserProfileDto?> Handle(GetMeQuery request, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@IdUser", request.IdUser);
        parameters.Add("@StatusDeleted", EntityStatus.Deleted);

        using var conn = _dbFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<UserProfileDto?>(GETME_QUERY, parameters);
    }
}
```

- [ ] **Step 2: `UsersController` (GET /users/me)**

`UsersController.cs`:
```csharp
using BigSchool.Application.Auth.DTOs;
using BigSchool.Application.Auth.Interfaces.Services;
using BigSchool.Application.Auth.Queries.GetMe;
using BigSchool.Application.SharedKernel.Common;
using BigSchool.WebApi.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BigSchool.WebApi.Controllers.Auth;

[ApiController]
[Authorize]
[Route("api/v1/users")]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserIdEncryptor _encryptor;

    public UsersController(IMediator mediator, IUserIdEncryptor encryptor)
    {
        _mediator = mediator;
        _encryptor = encryptor;
    }

    private int UserId => CurrentUser.GetId(User, _encryptor);

    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Me()
    {
        var result = await _mediator.Send(new GetMeQuery(UserId));
        return result is null
            ? NotFound(ApiResponse.Fail(new ApiError { Code = "ENTITY_NOT_FOUND", Message = "Usuario no encontrado." }))
            : Ok(ApiResponse<UserProfileDto>.Success(result));
    }
}
```

- [ ] **Step 3: E2E**

`GetMeTests.cs` (usa `AuthEndpointTestBase`; obtén un client autenticado del `AuthResponse.AccessToken`):
```csharp
    [Fact]
    public async Task GetMe_WithoutToken_Returns401()
        => (await Factory.CreateClient().GetAsync("/api/v1/users/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact]
    public async Task GetMe_Authenticated_ReturnsProfileFromToken()
    {
        var email = $"me-{Guid.NewGuid():N}@test.com";
        var auth = await RegisterAsync(email, "Ada Lovelace"); // EUR por defecto
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var env = await (await client.GetAsync("/api/v1/users/me"))
            .Content.ReadFromJsonAsync<ApiEnvelope<UserProfileResponse>>();
        env!.Data!.Email.Should().Be(email);
        env.Data!.FullName.Should().Be("Ada Lovelace");
        env.Data!.BaseCurrency.Should().Be("EUR");
    }

    private record UserProfileResponse(int IdUser, string Email, string FullName, string BaseCurrency, string? LastLoginDate);
```
> `using System.Net.Http.Headers;` para `AuthenticationHeaderValue`. (Si el base ya expone un `AuthenticatedClient`, reutilízalo.)

Run: `dotnet test tests/BigSchool.Integration.Tests --filter GetMeTests` → PASS.

- [ ] **Step 4: Verde + Commit**

Run: FULL. Luego:
```bash
git add -A && git commit -m "feat(auth): GET /users/me (perfil, Dapper) + UsersController

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 3: #2/A escritura — `PUT /users/me` (dominio + EF, re-hash Argon2)

**Files:**
- Modify: `src/BigSchool.Domain/Auth/Entities/User.cs` (`UpdateProfile`, `ChangePassword`)
- Create: `src/BigSchool.Application/Auth/Commands/UpdateUser/{UpdateUserCommand,UpdateUserCommandHandler,UpdateUserCommandValidator}.cs`
- Modify: `src/BigSchool.WebApi/Controllers/Auth/UsersController.cs` (PUT /users/me)
- Test: `tests/BigSchool.Domain.Tests/Entities/Auth/UserProfileTests.cs`
- Test: `tests/BigSchool.Application.Tests/Commands/Auth/UpdateUserCommandHandlerTests.cs`, `tests/BigSchool.Application.Tests/Validators/Auth/UpdateUserCommandValidatorTests.cs`
- Test: `tests/BigSchool.Integration.Tests/Auth/PutMeTests.cs`

- [ ] **Step 1: Tests de dominio (fallan)**

`UserProfileTests.cs`:
```csharp
using BigSchool.Domain.Auth.Entities;
using FluentAssertions;
using Xunit;

namespace BigSchool.Domain.Tests.Entities.Auth;

public class UserProfileTests
{
    private static User NewUser() => User.Create("ada@example.com", "h", "s", "Ada");

    [Fact]
    public void UpdateProfile_ValidFullName_UpdatesFullNameAndUpdatedAt()
    {
        var u = NewUser();
        u.UpdateProfile("Ada L.");
        u.FullName.Should().Be("Ada L.");
        u.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdateProfile_EmptyFullName_ThrowsArgumentException()
    {
        var u = NewUser();
        FluentActions.Invoking(() => u.UpdateProfile(" ")).Should().Throw<System.ArgumentException>();
    }

    [Fact]
    public void ChangePassword_ValidHashAndSalt_UpdatesHashSaltAndUpdatedAt()
    {
        var u = NewUser();
        u.ChangePassword("newHash", "newSalt");
        u.PasswordHash.Should().Be("newHash");
        u.PasswordSalt.Should().Be("newSalt");
        u.UpdatedAt.Should().NotBeNull();
    }
}
```
Run: `dotnet test tests/BigSchool.Domain.Tests --filter UserProfileTests` → FAIL.

- [ ] **Step 2: Métodos de dominio en `User`**

Añade a `User` (junto a `UpdateLastLogin`):
```csharp
    public void UpdateProfile(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full name is required.", nameof(fullName));
        FullName = fullName.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void ChangePassword(string hash, string salt)
    {
        PasswordHash = hash;
        PasswordSalt = salt;
        UpdatedAt = DateTime.UtcNow;
    }
```

- [ ] **Step 3: Command + Validator + Handler**

`UpdateUserCommand.cs`:
```csharp
using BigSchool.Application.Auth.DTOs;
using MediatR;

namespace BigSchool.Application.Auth.Commands.UpdateUser;

public record UpdateUserCommand(int IdUser, string FullName, string? Password) : IRequest<UserProfileDto>;
```
`UpdateUserCommandValidator.cs`:
```csharp
using FluentValidation;

namespace BigSchool.Application.Auth.Commands.UpdateUser;

public class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        When(x => x.Password is not null, () =>
            RuleFor(x => x.Password!).MinimumLength(8).MaximumLength(100));
    }
}
```
`UpdateUserCommandHandler.cs`:
```csharp
using BigSchool.Application.Auth.DTOs;
using BigSchool.Application.Auth.Interfaces.Repositories;
using BigSchool.Application.Auth.Interfaces.Services;
using BigSchool.Domain.SharedKernel.Exceptions;
using MediatR;

namespace BigSchool.Application.Auth.Commands.UpdateUser;

public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, UserProfileDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    public UpdateUserCommandHandler(IUserRepository userRepository, IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<UserProfileDto> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.IdUser, cancellationToken)
            ?? throw new NotFoundException("Usuario no encontrado.");

        user.UpdateProfile(request.FullName);
        if (request.Password is not null)
        {
            var (hash, salt) = _passwordHasher.HashPassword(request.Password);
            user.ChangePassword(hash, salt);
        }

        await _userRepository.UnitOfWork.SaveChangesAsync();
        return new UserProfileDto(user.IdUser, user.Email, user.FullName,
            user.BaseCurrency.ToString(), user.LastLoginDate);
    }
}
```
> Ajusta el ctor de `NotFoundException` al existente en el repo (ver otros usos). El `ExceptionHandlingMiddleware` debe mapear `NotFoundException` → 404 `ENTITY_NOT_FOUND`; confírmalo (si no, añade el mapeo).

- [ ] **Step 4: PUT en `UsersController`**

Añade a `UsersController` (`using BigSchool.Application.Auth.Commands.UpdateUser;`):
```csharp
    public record UpdateUserRequest(string FullName, string? Password);

    [HttpPut("me")]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateUserRequest body)
    {
        var result = await _mediator.Send(new UpdateUserCommand(UserId, body.FullName, body.Password));
        return Ok(ApiResponse<UserProfileDto>.Success(result));
    }
```

- [ ] **Step 5: Unit tests de Application**

`UpdateUserCommandValidatorTests.cs` (estilo real: `TestValidate`/`ShouldHaveValidationErrorFor`, no `.Validate(...).IsValid`):
```csharp
using BigSchool.Application.Auth.Commands.UpdateUser;
using FluentValidation.TestHelper;
using Xunit;

namespace BigSchool.Application.Tests.Validators.Auth;

public class UpdateUserCommandValidatorTests
{
    private readonly UpdateUserCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommandWithoutPassword_NoErrors()
        => _validator.TestValidate(new UpdateUserCommand(1, "Ada", null)).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_EmptyFullName_HasError()
        => _validator.TestValidate(new UpdateUserCommand(1, "", null)).ShouldHaveValidationErrorFor(x => x.FullName);

    [Fact]
    public void Validate_ShortPassword_HasError()
        => _validator.TestValidate(new UpdateUserCommand(1, "Ada", "123")).ShouldHaveValidationErrorFor(x => x.Password);

    [Fact]
    public void Validate_ValidCommandWithPassword_NoErrors()
        => _validator.TestValidate(new UpdateUserCommand(1, "Ada", "Secret123!")).ShouldNotHaveAnyValidationErrors();
}
```
`UpdateUserCommandHandlerTests.cs`:
```csharp
using BigSchool.Application.Auth.Commands.UpdateUser;
using BigSchool.Application.Auth.Interfaces.Repositories;
using BigSchool.Application.Auth.Interfaces.Services;
using BigSchool.Domain.Auth.Entities;
using BigSchool.Domain.SharedKernel.Exceptions;
using BigSchool.Domain.SharedKernel.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace BigSchool.Application.Tests.Commands.Auth;

public class UpdateUserCommandHandlerTests
{
    private static User NewUser() => User.Create("ada@example.com", "h", "s", "Ada");

    [Fact]
    public async Task Handle_NullPassword_UpdatesFullNameWithoutRehashing()
    {
        var user = NewUser();
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        repo.SetupGet(r => r.UnitOfWork).Returns(Mock.Of<IUnitOfWork>());
        var hasher = new Mock<IPasswordHasher>();
        var handler = new UpdateUserCommandHandler(repo.Object, hasher.Object);

        var dto = await handler.Handle(new UpdateUserCommand(1, "Ada L.", null), CancellationToken.None);

        dto.FullName.Should().Be("Ada L.");
        hasher.Verify(h => h.HashPassword(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithPassword_RehashesPassword()
    {
        var user = NewUser();
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        repo.SetupGet(r => r.UnitOfWork).Returns(Mock.Of<IUnitOfWork>());
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.HashPassword("Secret123!")).Returns(("newHash", "newSalt"));
        var handler = new UpdateUserCommandHandler(repo.Object, hasher.Object);

        await handler.Handle(new UpdateUserCommand(1, "Ada", "Secret123!"), CancellationToken.None);

        hasher.Verify(h => h.HashPassword("Secret123!"), Times.Once);
        user.PasswordHash.Should().Be("newHash");
    }

    [Fact]
    public async Task Handle_NonExistentUser_ThrowsNotFoundException()
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        var handler = new UpdateUserCommandHandler(repo.Object, Mock.Of<IPasswordHasher>());

        await FluentActions.Invoking(() => handler.Handle(new UpdateUserCommand(99, "Ada", null), CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();
    }
}
```
> Ajusta la tupla de retorno de `HashPassword` al tipo real (revisa `IPasswordHasher`: `(string hash, string salt)`).

- [ ] **Step 6: E2E — cambia nombre; password round-trip real (Argon2)**

`PutMeTests.cs`:
```csharp
    [Fact]
    public async Task PutMe_WithoutToken_Returns401()
        => (await Factory.CreateClient().PutAsJsonAsync("/api/v1/users/me", new { fullName = "X" }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact]
    public async Task PutMe_ValidFullName_UpdatesFullName_AndPersists()
    {
        var email = $"put-{Guid.NewGuid():N}@test.com";
        var auth = await RegisterAsync(email, "Ada");
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var resp = await client.PutAsJsonAsync("/api/v1/users/me", new { fullName = "Ada Lovelace" });
        resp.EnsureSuccessStatusCode();
        var env = await resp.Content.ReadFromJsonAsync<ApiEnvelope<UserProfileResponse>>();
        env!.Data!.FullName.Should().Be("Ada Lovelace");
    }

    [Fact]
    public async Task PutMe_NewPassword_NewLoginSucceeds_AndOldLoginFails()
    {
        var email = $"pwd-{Guid.NewGuid():N}@test.com";
        var auth = await RegisterAsync(email, "Ada"); // password DefaultPassword
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        (await client.PutAsJsonAsync("/api/v1/users/me", new { fullName = "Ada", password = "NewSecret123!" }))
            .EnsureSuccessStatusCode();

        // Login con la NUEVA funciona (hashing Argon2 real, sin mocks).
        (await Factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "NewSecret123!" })).StatusCode.Should().Be(HttpStatusCode.OK);
        // Login con la VIEJA falla.
        (await Factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = DefaultPassword })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private record UserProfileResponse(int IdUser, string Email, string FullName, string BaseCurrency, string? LastLoginDate);
```
> Confirma el contrato de `LoginCommand`/`/auth/login` (campos y código de error de credenciales inválidas — típicamente 400 `INVALID_CREDENTIALS`). Ajusta el código esperado si el repo devuelve otro.

Run: `dotnet test tests/BigSchool.Integration.Tests --filter PutMeTests` → PASS.

- [ ] **Step 7: Verde + Commit**

Run: FULL. Luego:
```bash
git add -A && git commit -m "feat(auth): PUT /users/me (perfil + re-hash Argon2, dominio UpdateProfile/ChangePassword)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Verificación final (DoD — spec 008 §8)

- [ ] **Unit Domain**: `User.Create` persiste `baseCurrency`; `UpdateProfile` valida no vacío + `UpdatedAt`; `ChangePassword` cambia hash/salt + `UpdatedAt`.
- [ ] **Unit Application**: `RegisterCommandValidator` rechaza moneda omitida (default 0)/inválida; `UpdateUserCommandValidator` (password corto solo si se envía); `UpdateUserCommandHandler` (re-hash solo con password; 404 si no existe).
- [ ] **E2E**: register con `baseCurrency:"USD"` persiste USD; sin/ inválida → 400; `GET /users/me` 401 sin token, devuelve perfil del token; `PUT /users/me` cambia `fullName`; con `password` → login nueva OK / vieja falla (Argon2 real). Bases/tests de Auth ajustados al contrato.
- [ ] **Arquitectura**: `ModuleBoundaryTests` sin cambios (todo en Auth).
- [ ] **FULL** verde.

## Self-Review (cobertura de la spec 008)

| Sección | Tarea(s) |
|---|---|
| §4 #1 Moneda obligatoria (command/validator/handler + E2E rompedor) | 1 |
| §5.1 Dominio (`UpdateProfile`, `ChangePassword`) | 3 |
| §5.2 `GET /users/me` (Dapper, DTO string BaseCurrency) | 2 |
| §5.3 `PUT /users/me` (EF, re-hash condicional, 404) | 3 |
| §5.4 `UsersController` (`/api/v1/users`) | 2 (GET), 3 (PUT) |
| §8 Verificación | "Verificación final" |
