# Backend Auth — Endpoint Refresh Token — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementar el endpoint `POST /api/v1/auth/refresh` documentado en `docs/02-backend-design.md` (sección API Endpoints → Auth) que quedó pendiente del Plan 1 (`2026-06-08-backend-crosscutting-auth.md`). El endpoint re-emite un nuevo access token JWT a partir de un access token aún válido, siguiendo el resto del flujo CQRS existente (Command + Handler + Validator + Controller).

**Estrategia elegida (aprobada por el humano): Re-emisión desde access token válido.**
El cliente llama a `/refresh` con su access token actual en la cabecera `Authorization: Bearer <token>`. El endpoint está protegido con `[Authorize]`, de modo que el middleware `JwtBearer` (ya configurado con `ValidateLifetime = true`, `ValidateIssuer`, `ValidateAudience`, `ValidateIssuerSigningKey`) valida firma, emisor, audiencia y expiración **antes** de llegar al handler. El handler extrae el `userId` del token (`IJwtService.ExtractUserId`, que descifra el claim `sub` con DPAPI), recarga el usuario (verifica que sigue existiendo y activo vía el Global Query Filter de soft-delete) y emite un token nuevo reutilizando `IJwtService.GenerateToken`.

- **Sin refresh token separado, sin tabla, sin migración, sin cambios en la entidad `User`.**
- Limitación conocida y aceptada: si el access token expira, no hay forma de refrescar y el usuario debe volver a hacer login. No hay rotación ni revocación (el token antiguo sigue válido hasta su propia expiración).

**Architecture:** Clean Architecture con CQRS. `RefreshTokenCommand` viaja por MediatR igual que `LoginCommand`/`RegisterCommand`. El `RefreshTokenCommandHandler` depende de `IJwtService` e `IUserRepository` (ambos ya registrados por Autofac). El `RefreshTokenCommandValidator` se engancha automáticamente al `ValidationBehavior` del pipeline. El `AuthController` lee el bearer token de la cabecera y lo pasa al command. Los errores se devuelven en formato `ApiResponse` (envelope RFC 7807) por el `ExceptionHandlingMiddleware` existente.

**Tech Stack:** .NET 8, MediatR 12, FluentValidation 11, JWT Bearer (`System.IdentityModel.Tokens.Jwt`), ASP.NET Data Protection (DPAPI ya en uso para cifrar el userId), xUnit + FluentAssertions + Moq.

**Dependencias previas:** Plan 1 completado (Tasks 1-10). Existen: `IJwtService.GenerateToken/ExtractUserId`, `IUserRepository.GetByIdAsync`, `AuthResponseDto`, `ApiResponse`, `ExceptionHandlingMiddleware`, `ValidationBehavior`, pipeline `JwtBearer` en `Program.cs`.

---

## Decisiones de Diseño (aprobadas por el humano)

1. **Estrategia**: re-emisión desde access token válido (sin refresh token persistido ni JWT de refresh separado). La más simple; suficiente para el alcance del TFM.
2. **Protección**: endpoint con `[Authorize]` — la validación de firma/expiración la hace el middleware `JwtBearer`, no el handler. El handler confía en que el token que le llega ya es válido.
3. **Origen del token en el handler**: el controller lee el token crudo de la cabecera `Authorization` y lo pasa en `RefreshTokenCommand(string AccessToken)`. El handler usa `IJwtService.ExtractUserId(token)` (reutiliza API existente; sin nuevas dependencias ni nuevos métodos en `IJwtService`).
4. **Recarga del usuario**: el handler hace `GetByIdAsync(userId)` para (a) verificar que el usuario sigue existiendo y activo (un usuario con `IdStatus = Deleted` queda fuera por el Global Query Filter → `null`) y (b) devolver `Email`/`FullName` frescos en el `AuthResponseDto`.
5. **Errores de borde**: si `ExtractUserId` devuelve `null` (token sin `sub` válido) o el usuario no existe/está borrado, se lanza `UnauthorizedAccessException` → el middleware lo mapea a `401 UNAUTHORIZED`. No se crean excepciones de dominio nuevas (alcance mínimo).
6. **DTO de respuesta**: se reutiliza `AuthResponseDto` tal cual (igual que login/register). No se añade campo de refresh token porque no hay refresh token en esta estrategia.
7. **Sin cambios de documentación de diseño**: `docs/02-backend-design.md` ya lista la fila `POST /api/v1/auth/refresh`. Solo se actualiza el diario.

---

## Resumen de Tareas

| # | Tarea | Descripción |
|---|-------|-------------|
| 1 | RefreshTokenCommand + Validator | Command CQRS con el access token + validación NotEmpty |
| 2 | RefreshTokenCommandHandler + tests | Handler TDD: extrae userId, recarga usuario, re-emite token |
| 3 | AuthController.Refresh + Swagger Bearer | Endpoint `[Authorize]`, lee cabecera, envelope; auth Bearer en Swagger |
| 4 | Verificación E2E + diario | Build, tests, prueba manual Swagger/curl, actualizar `docs/diario.md` |

---

### Task 1: RefreshTokenCommand + Validator

**Files:**
- Create: `src/backend/src/BigSchool.Application/Commands/Auth/Refresh/RefreshTokenCommand.cs`
- Create: `src/backend/src/BigSchool.Application/Commands/Auth/Refresh/RefreshTokenCommandValidator.cs`

- [x] **Step 1: Crear el Command**

```csharp
// src/backend/src/BigSchool.Application/Commands/Auth/Refresh/RefreshTokenCommand.cs
using BigSchool.Application.DTOs.Auth;
using MediatR;

namespace BigSchool.Application.Commands.Auth.Refresh;

public record RefreshTokenCommand(string AccessToken) : IRequest<AuthResponseDto>;
```

- [x] **Step 2: Crear el Validator**

```csharp
// src/backend/src/BigSchool.Application/Commands/Auth/Refresh/RefreshTokenCommandValidator.cs
using FluentValidation;

namespace BigSchool.Application.Commands.Auth.Refresh;

public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.AccessToken)
            .NotEmpty().WithMessage("El token de acceso es obligatorio.");
    }
}
```

**Nota:** El validator se registra y engancha automáticamente al `ValidationBehavior` (Autofac escanea `IValidator<>` en el assembly Application). En la práctica `[Authorize]` ya garantiza que la cabecera existe, pero el validator protege frente a llamadas mal construidas internamente y mantiene la simetría con `LoginCommand`/`RegisterCommand`.

- [x] **Step 3: Verificar build**

Run: `dotnet build src/backend/src/BigSchool.Application --no-restore -v q`
Expected: 0 errors

- [x] **Step 4: Commit**

```bash
git add -A && git commit -m "feat: añadir RefreshTokenCommand y validator para endpoint de refresh"
```

---

### Task 2: RefreshTokenCommandHandler + tests (TDD)

**Files:**
- Create: `src/backend/tests/BigSchool.Application.Tests/Commands/Auth/RefreshTokenCommandHandlerTests.cs`
- Create: `src/backend/src/BigSchool.Application/Commands/Auth/Refresh/RefreshTokenCommandHandler.cs`

- [x] **Step 1: Escribir los tests del handler (espejo de `LoginCommandHandlerTests`)**

```csharp
// tests/BigSchool.Application.Tests/Commands/Auth/RefreshTokenCommandHandlerTests.cs
using BigSchool.Application.Commands.Auth.Refresh;
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Application.Interfaces.Services;
using BigSchool.Domain.Entities;
using FluentAssertions;
using Moq;
using Xunit;

namespace BigSchool.Application.Tests.Commands.Auth;

public class RefreshTokenCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IJwtService> _jwtMock = new();
    private readonly RefreshTokenCommandHandler _handler;

    public RefreshTokenCommandHandlerTests()
    {
        _handler = new RefreshTokenCommandHandler(_userRepoMock.Object, _jwtMock.Object);
    }

    [Fact]
    public async Task Handle_ValidToken_ReturnsNewToken()
    {
        var user = User.Create("user@test.com", "hashed", "salted", "Test User");
        _jwtMock.Setup(j => j.ExtractUserId("oldtoken")).Returns(7);
        _userRepoMock.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _jwtMock.Setup(j => j.GenerateToken(It.IsAny<int>(), "user@test.com"))
            .Returns(new JwtToken("newtoken", DateTime.UtcNow.AddHours(1)));

        var result = await _handler.Handle(new RefreshTokenCommand("oldtoken"), CancellationToken.None);

        result.AccessToken.Should().Be("newtoken");
        result.Email.Should().Be("user@test.com");
        result.FullName.Should().Be("Test User");
    }

    [Fact]
    public async Task Handle_TokenWithoutUserId_ThrowsUnauthorized()
    {
        _jwtMock.Setup(j => j.ExtractUserId("badtoken")).Returns((int?)null);

        var act = () => _handler.Handle(new RefreshTokenCommand("badtoken"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_UserNotFoundOrDeleted_ThrowsUnauthorized()
    {
        _jwtMock.Setup(j => j.ExtractUserId("oldtoken")).Returns(99);
        _userRepoMock.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var act = () => _handler.Handle(new RefreshTokenCommand("oldtoken"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
```

- [x] **Step 2: Ejecutar tests para verificar que fallan**

Run: `dotnet test src/backend/tests/BigSchool.Application.Tests --filter "FullyQualifiedName~RefreshTokenCommandHandlerTests" --no-restore -v q`
Expected: FAIL — `RefreshTokenCommandHandler` no existe

- [x] **Step 3: Implementar el Handler**

```csharp
// src/backend/src/BigSchool.Application/Commands/Auth/Refresh/RefreshTokenCommandHandler.cs
using BigSchool.Application.DTOs.Auth;
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Application.Interfaces.Services;
using MediatR;

namespace BigSchool.Application.Commands.Auth.Refresh;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthResponseDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IJwtService _jwtService;

    public RefreshTokenCommandHandler(IUserRepository userRepository, IJwtService jwtService)
    {
        _userRepository = userRepository;
        _jwtService = jwtService;
    }

    public async Task<AuthResponseDto> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var userId = _jwtService.ExtractUserId(request.AccessToken);
        if (userId is null)
            throw new UnauthorizedAccessException("Token de acceso inválido.");

        var user = await _userRepository.GetByIdAsync(userId.Value, cancellationToken);
        if (user is null)
            throw new UnauthorizedAccessException("La sesión ya no es válida.");

        var token = _jwtService.GenerateToken(user.IdUser, user.Email);

        return new AuthResponseDto(token.AccessToken, token.ExpiresAt, user.Email, user.FullName);
    }
}
```

**Nota:** No se llama a `UpdateLastLogin()` ni a `SaveChangesAsync()` — un refresh no es un login y no muta estado. Solo lectura + emisión de token.

- [x] **Step 4: Ejecutar tests para verificar que pasan**

Run: `dotnet test src/backend/tests/BigSchool.Application.Tests --filter "FullyQualifiedName~RefreshTokenCommandHandlerTests" --no-restore -v q`
Expected: PASS (3 tests)

- [x] **Step 5: Commit**

```bash
git add -A && git commit -m "feat: implementar RefreshTokenCommandHandler con re-emisión de token y tests"
```

---

### Task 3: AuthController.Refresh + Swagger Bearer

**Files:**
- Modify: `src/backend/src/BigSchool.WebApi/Controllers/AuthController.cs`
- Modify: `src/backend/src/BigSchool.WebApi/Program.cs` (definición de seguridad Bearer en Swagger)

- [x] **Step 1: Añadir el endpoint `Refresh` al AuthController**

Añadir el `using` y el método. El endpoint lee el bearer token de la cabecera (no hay `SaveTokens` activado en `JwtBearer`, así que se lee directamente de `Authorization`):

```csharp
// Añadir al inicio del archivo, junto a los demás using:
using BigSchool.Application.Commands.Auth.Refresh;
using Microsoft.AspNetCore.Authorization;
```

```csharp
// Añadir como nuevo método dentro de AuthController, tras Login():
[Authorize]
[HttpPost("refresh")]
[ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
public async Task<IActionResult> Refresh()
{
    var authHeader = Request.Headers.Authorization.ToString();
    var accessToken = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
        ? authHeader["Bearer ".Length..].Trim()
        : authHeader.Trim();

    var result = await _mediator.Send(new RefreshTokenCommand(accessToken));
    return Ok(ApiResponse<AuthResponseDto>.Success(result));
}
```

**Nota:** Con `[Authorize]`, si el token falta, está mal firmado o ha expirado, el middleware `JwtBearer` responde `401` antes de entrar al método — comportamiento esperado de esta estrategia.

- [x] **Step 2: Configurar el esquema de seguridad Bearer en Swagger**

Para poder probar el endpoint protegido desde Swagger UI (botón "Authorize"), ampliar `AddSwaggerGen` en `Program.cs`. Localizar el bloque `builder.Services.AddSwaggerGen(c => { ... })` y añadir, después del `c.SwaggerDoc(...)`:

```csharp
        c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Description = "Introduce el JWT obtenido en /login o /register."
        });
        c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
```

- [x] **Step 3: Verificar build**

Run: `dotnet build src/backend --no-restore -v q`
Expected: 0 errors

- [x] **Step 4: Commit**

```bash
git add -A && git commit -m "feat: añadir endpoint POST /api/v1/auth/refresh con auth Bearer en Swagger"
```

---

### Task 4: Verificación E2E + actualizar diario

**Files:**
- Modify: `docs/diario.md`

- [ ] **Step 1: Ejecutar toda la suite de tests del backend**

Run: `dotnet test src/backend --no-restore -v q`
Expected: PASS (todos, incluyendo los 3 nuevos de `RefreshTokenCommandHandlerTests`)

- [ ] **Step 2: Verificación manual del flujo (Swagger o curl)**

Con MySQL levantado (`docker compose up -d`) y la API corriendo (`dotnet run --project src/backend/src/BigSchool.WebApi`):

1. `POST /api/v1/auth/register` (o `/login`) → copiar `accessToken` del envelope `data`.
2. `POST /api/v1/auth/refresh` con cabecera `Authorization: Bearer <accessToken>` y cuerpo vacío → debe devolver `200` con un `accessToken` nuevo y `expiresAt` posterior.
3. `POST /api/v1/auth/refresh` sin cabecera o con token basura → debe devolver `401`.

Ejemplo curl del caso correcto:

```bash
curl -i -X POST http://localhost:5000/api/v1/auth/refresh \
  -H "Authorization: Bearer <ACCESS_TOKEN>"
```

- [ ] **Step 3: Actualizar el diario**

Añadir una entrada al final de `docs/diario.md` siguiendo el formato existente (fecha `2026-06-15`, Fase: Implementar, Módulo: backend) que registre: implementación del endpoint `refresh` pendiente del Plan 1, estrategia elegida (re-emisión desde access válido), y su limitación conocida (sin rotación/revocación; si el access expira hay que volver a login).

- [ ] **Step 4: Commit**

```bash
git add -A && git commit -m "docs: registrar implementación del endpoint de refresh en el diario"
```

---

## Notas finales

- **Por qué no hay cambios en `User` ni migración**: la estrategia no persiste estado de sesión; el token nuevo se firma con los datos que ya están en el JWT validado + recarga del usuario.
- **Evolución futura (fuera de alcance)**: si más adelante se quiere refresh tras expiración del access, revocación en logout o detección de reuso, habría que migrar a un `RefreshToken` persistido con rotación (entidad/AR nueva + tabla + migración + `IRefreshTokenRepository`). Este plan deja la puerta abierta sin bloquearla.
- **Seguridad**: el `userId` viaja cifrado con DPAPI dentro del claim `sub` (igual que en login); `/refresh` no expone el INT plano en ningún momento.
