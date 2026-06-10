# Backend Cross-cutting + Auth — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementar la infraestructura cross-cutting (Envelope con Problem Details RFC 7807, Exception Middleware con protección de eventos, ValidationBehavior, Global Query Filter, EF Core configurations, migración inicial) y el sistema de autenticación completo (JWT con DPAPI + Argon2 + Register/Login) sobre el scaffolding existente.

**Architecture:** Clean Architecture con CQRS. Commands usan EF Core vía repositorios de Aggregate Roots. Queries usan Dapper vía IDbConnectionFactory. User es Aggregate Root que posee SubCategory como entidad hija (colección navegable). Transaction y RagDocument son ARs independientes que referencian User por ID. La autenticación se implementa como servicios en Infrastructure (JwtService con DPAPI para encrypt de userId, Argon2PasswordHasher) con interfaces en Application. El middleware de excepciones captura errores y los devuelve en formato Problem Details tipado. Los handlers de DomainEvents están protegidos por un NotificationExceptionBehavior.

**Tech Stack:** .NET 8, EF Core 8 + Pomelo MySQL, Dapper, MediatR 12, FluentValidation 11, Argon2 (Isopoh.Cryptography.Argon2), JWT Bearer + ASP.NET Data Protection, Autofac, xUnit + FluentAssertions + Moq

**Dependencias previas:** Scaffolding completado (PRs #1-#8). Fix agregados DDD (PRs #10-#11). Docker Compose con MySQL disponible.

---

## Decisiones de Diseño (aprobadas por el humano)

1. **Constructores de entidades**: `protected` parameterless para EF Core + constructor con todos los parámetros + factory `Create` estático
2. **SubCategory**: Entidad hija de User. Se accede exclusivamente vía `user.AddSubCategory(mainCategory, name)`. Constructor `private`. No necesita `InternalsVisibleTo`. Se testea a través de User.
3. **Envelope**: Problem Details RFC 7807 — errores como objetos `{ code, message, field }` para que el frontend haga switch/case
4. **DomainEvents protegidos**: `NotificationExceptionBehavior` que envuelve todos los `INotificationHandler` con try-catch + logging
5. **EF Core Configurations**: Métodos separados (`ConfigureProperties`, `ConfigureForeignKeys`, `ConfigureIndexes`). Seeds en Extension method
6. **DbSets**: Solo ARs tienen DbSet explícito. Entidades hijas se referencian vía navigation properties en la Configuration
7. **Argon2**: Constantes de config — `Parallelism=2`, `HashLength=16`, `MemoryCost=65536`, `Iterations=3`
8. **JWT userId**: Encrypt/decrypt con ASP.NET Data Protection API (DPAPI) — no exponer INT plano en claims
9. **Excepciones específicas**: Cada error de dominio tiene su propia excepción (ej: `InvalidCredentialsDomainException`), nunca usar `DomainException` genérica directamente

---

## Resumen de Tareas

| # | Tarea | Descripción |
|---|-------|-------------|
| 1 | Entidad User completa | Properties, constructor protected + params, factory Create, colección SubCategories, AddSubCategory |
| 2 | Entidad SubCategory (hija de User) | Constructor private, factory internal, testeada vía User.AddSubCategory, invariante unicidad |
| 3 | Excepciones de dominio tipadas | DomainException base + específicas + ApiError tipado RFC 7807 |
| 4 | Envelope + ExceptionMiddleware + NotificationExceptionBehavior | Errores tipados, protección eventos |
| 5 | EF Core Configurations + Migración | Fluent API organizado, shadow property IdUser, seeds en Extension, Global Query Filter |
| 6 | Servicios Auth: Argon2 + JWT con DPAPI | IPasswordHasher, IJwtService + encrypt userId + tests |
| 7 | Commands Auth: Register + Login | CQRS handlers, validators, DTO, excepciones específicas + tests |
| 8 | AuthController + JWT middleware + UserRepository | Controller, pipeline JWT, repo concreto |
| 9 | ValidationBehavior (Pipeline MediatR) | Validación automática + tests |
| 10 | Tests de validators + Verificación E2E | Cobertura validators, build, migración, endpoints |

---

### Task 1: Entidad User completa

**Files:**
- Modify: `src/backend/src/BigSchool.Domain/Entities/User.cs`
- Create: `src/backend/tests/BigSchool.Domain.Tests/Entities/UserTests.cs`

- [x] **Step 1: Escribir tests para la entidad User**

```csharp
// tests/BigSchool.Domain.Tests/Entities/UserTests.cs
using BigSchool.Domain.Entities;
using BigSchool.Domain.Enums;
using FluentAssertions;

namespace BigSchool.Domain.Tests.Entities;

public class UserTests
{
    [Fact]
    public void Create_WithValidData_SetsPropertiesCorrectly()
    {
        var user = User.Create("test@example.com", "hashedpwd", "salted", "John Doe");

        user.Email.Should().Be("test@example.com");
        user.PasswordHash.Should().Be("hashedpwd");
        user.PasswordSalt.Should().Be("salted");
        user.FullName.Should().Be("John Doe");
        user.IdStatus.Should().Be(EntityStatus.Active);
        user.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        user.LastLoginDate.Should().BeNull();
        user.SubCategories.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void Create_WithInvalidEmail_ThrowsArgumentException(string? email)
    {
        var act = () => User.Create(email!, "hash", "salt", "Name");
        act.Should().Throw<ArgumentException>().WithParameterName("email");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void Create_WithInvalidFullName_ThrowsArgumentException(string? fullName)
    {
        var act = () => User.Create("test@example.com", "hash", "salt", fullName!);
        act.Should().Throw<ArgumentException>().WithParameterName("fullName");
    }

    [Fact]
    public void UpdateLastLogin_SetsDateAndUpdatedAt()
    {
        var user = User.Create("test@example.com", "hash", "salt", "Name");

        user.UpdateLastLogin();

        user.LastLoginDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        user.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Create_NormalizesEmailToLowerCase()
    {
        var user = User.Create("TEST@Example.COM", "hash", "salt", "Name");

        user.Email.Should().Be("test@example.com");
    }
}
```

- [x] **Step 2: Ejecutar tests para verificar que fallan**

Run: `dotnet test src/backend/tests/BigSchool.Domain.Tests --filter "FullyQualifiedName~UserTests" --no-restore -v q`
Expected: FAIL — `User` no tiene `Create` ni propiedades

- [x] **Step 3: Implementar la entidad User**

```csharp
// src/backend/src/BigSchool.Domain/Entities/User.cs
using BigSchool.Domain.Enums;

namespace BigSchool.Domain.Entities;

public class User : BaseEntity, IAggregateRoot
{
    public int IdUser { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string PasswordSalt { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public DateTime? LastLoginDate { get; private set; }
    public EntityStatus IdStatus { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private readonly List<SubCategory> _subCategories = [];
    public IReadOnlyCollection<SubCategory> SubCategories => _subCategories.AsReadOnly();

    protected User() { } // EF Core

    private User(string email, string passwordHash, string passwordSalt,
        string fullName, EntityStatus idStatus, DateTime createdAt)
    {
        Email = email;
        PasswordHash = passwordHash;
        PasswordSalt = passwordSalt;
        FullName = fullName;
        IdStatus = idStatus;
        CreatedAt = createdAt;
    }

    public static User Create(string email, string passwordHash, string passwordSalt, string fullName)
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
            EntityStatus.Active,
            DateTime.UtcNow);
    }

    public void UpdateLastLogin()
    {
        LastLoginDate = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public SubCategory AddSubCategory(MainCategory mainCategory, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        var trimmedName = name.Trim();

        if (_subCategories.Any(s => s.IdMainCategory == mainCategory
            && s.Name.Equals(trimmedName, StringComparison.OrdinalIgnoreCase)
            && s.IdStatus != EntityStatus.Deleted))
        {
            throw new DuplicateSubCategoryDomainException(trimmedName, mainCategory);
        }

        var subCategory = SubCategory.Create(mainCategory, trimmedName);
        _subCategories.Add(subCategory);
        return subCategory;
    }
}
```

**Nota:** El constructor parametrizado es `private` — solo `Create` puede invocarlo. El constructor `protected` parameterless es para EF Core. No existe constructor público con parámetros. `DuplicateSubCategoryDomainException` se implementa en Task 3 junto con las demás excepciones. Para que compile en Task 1, se puede dejar un TODO temporal o implementar la excepción adelantada (preferible).

- [x] **Step 4: Ejecutar tests para verificar que pasan**

Run: `dotnet test src/backend/tests/BigSchool.Domain.Tests --filter "FullyQualifiedName~UserTests" --no-restore -v q`
Expected: PASS (5 tests)

- [x] **Step 5: Commit**

```bash
git add -A && git commit -m "feat: implementar entidad User con factory method, colección SubCategories y validaciones"
```

---

### Task 2: Entidad SubCategory (hija de User)

**Files:**
- Create: `src/backend/src/BigSchool.Domain/Entities/SubCategory.cs`
- Modify: `src/backend/tests/BigSchool.Domain.Tests/Entities/UserTests.cs` (añadir tests de SubCategory vía User)

**Principio DDD:** SubCategory es entidad hija de User. Su constructor es `private` — solo User puede crear instancias vía `AddSubCategory(...)`. Se testea a través del AR, no directamente. No necesita `InternalsVisibleTo`.

- [x] **Step 1: Añadir tests de SubCategory al archivo UserTests (vía AR)**

```csharp
// Añadir al archivo tests/BigSchool.Domain.Tests/Entities/UserTests.cs

public class UserSubCategoryTests
{
    [Fact]
    public void AddSubCategory_WithValidData_AddsToCollection()
    {
        var user = User.Create("test@example.com", "hash", "salt", "Name");

        var sub = user.AddSubCategory(MainCategory.Luxuries, "Conciertos");

        user.SubCategories.Should().HaveCount(1);
        sub.IdMainCategory.Should().Be(MainCategory.Luxuries);
        sub.Name.Should().Be("Conciertos");
        sub.IsDefault.Should().BeFalse();
        sub.IdStatus.Should().Be(EntityStatus.Active);
        sub.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void AddSubCategory_DuplicateNameSameCategory_ThrowsDuplicateException()
    {
        var user = User.Create("test@example.com", "hash", "salt", "Name");
        user.AddSubCategory(MainCategory.Luxuries, "Conciertos");

        var act = () => user.AddSubCategory(MainCategory.Luxuries, "Conciertos");

        act.Should().Throw<DuplicateSubCategoryDomainException>();
    }

    [Fact]
    public void AddSubCategory_SameNameDifferentCategory_Succeeds()
    {
        var user = User.Create("test@example.com", "hash", "salt", "Name");
        user.AddSubCategory(MainCategory.Luxuries, "Otros");

        var act = () => user.AddSubCategory(MainCategory.EssentialExpenses, "Otros");

        act.Should().NotThrow();
        user.SubCategories.Should().HaveCount(2);
    }

    [Fact]
    public void AddSubCategory_DuplicateNameCaseInsensitive_Throws()
    {
        var user = User.Create("test@example.com", "hash", "salt", "Name");
        user.AddSubCategory(MainCategory.Luxuries, "Conciertos");

        var act = () => user.AddSubCategory(MainCategory.Luxuries, "CONCIERTOS");

        act.Should().Throw<DuplicateSubCategoryDomainException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void AddSubCategory_WithEmptyName_ThrowsArgumentException(string? name)
    {
        var user = User.Create("test@example.com", "hash", "salt", "Name");

        var act = () => user.AddSubCategory(MainCategory.EssentialExpenses, name!);

        act.Should().Throw<ArgumentException>().WithParameterName("name");
    }

    [Fact]
    public void AddSubCategory_TrimsName()
    {
        var user = User.Create("test@example.com", "hash", "salt", "Name");

        var sub = user.AddSubCategory(MainCategory.Luxuries, "  Conciertos  ");

        sub.Name.Should().Be("Conciertos");
    }
}
```

- [x] **Step 2: Ejecutar tests para verificar que fallan**

Run: `dotnet test src/backend/tests/BigSchool.Domain.Tests --filter "FullyQualifiedName~UserSubCategoryTests" --no-restore -v q`
Expected: FAIL — `SubCategory` no existe

- [x] **Step 3: Implementar SubCategory con constructor private**

```csharp
// src/backend/src/BigSchool.Domain/Entities/SubCategory.cs
using BigSchool.Domain.Enums;

namespace BigSchool.Domain.Entities;

public class SubCategory : BaseEntity
{
    public int IdSubCategory { get; private set; }
    public MainCategory IdMainCategory { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsDefault { get; private set; }
    public EntityStatus IdStatus { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private SubCategory() { } // EF Core

    /// <summary>
    /// Factory interna — solo User.AddSubCategory() puede invocar este método.
    /// </summary>
    internal static SubCategory Create(MainCategory mainCategory, string name)
    {
        return new SubCategory
        {
            IdMainCategory = mainCategory,
            Name = name,
            IsDefault = false,
            IdStatus = EntityStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
    }
}
```

**Nota:** El constructor es `private` para EF Core. El factory `Create` es `internal` — accesible solo desde dentro del assembly Domain (donde vive User). No se necesita `InternalsVisibleTo` porque los tests no llaman a `SubCategory.Create()` directamente, sino a `user.AddSubCategory(...)`.

- [x] **Step 4: Implementar DuplicateSubCategoryDomainException (adelantada de Task 3)**

```csharp
// src/backend/src/BigSchool.Domain/Exceptions/DuplicateSubCategoryDomainException.cs
using BigSchool.Domain.Enums;

namespace BigSchool.Domain.Exceptions;

public class DuplicateSubCategoryDomainException : DomainException
{
    public DuplicateSubCategoryDomainException(string name, MainCategory mainCategory)
        : base("DUPLICATE_SUBCATEGORY",
            $"Ya existe una subcategoría '{name}' en la categoría '{mainCategory}'.") { }
}
```

**Nota:** Esto requiere que `DomainException` base ya exista. Si se ejecutan Tasks 1-2 antes de Task 3, se puede crear `DomainException` como abstract mínima aquí y completarla en Task 3. Alternativamente, se implementa Task 3 Step 3 primero (solo la clase base).

- [x] **Step 5: Ejecutar tests para verificar que pasan**

Run: `dotnet test src/backend/tests/BigSchool.Domain.Tests --filter "FullyQualifiedName~UserSubCategoryTests" --no-restore -v q`
Expected: PASS (6 tests)

- [x] **Step 6: Commit**

```bash
git add -A && git commit -m "feat: implementar entidad SubCategory como hija de User con invariante de unicidad"
```

---

### Task 3: Excepciones de Dominio Tipadas + ApiError RFC 7807

**Files:**
- Create: `src/backend/src/BigSchool.Domain/Exceptions/DomainException.cs`
- Create: `src/backend/src/BigSchool.Domain/Exceptions/NotFoundException.cs`
- Create: `src/backend/src/BigSchool.Domain/Exceptions/ConflictException.cs`
- Create: `src/backend/src/BigSchool.Domain/Exceptions/InvalidCredentialsDomainException.cs`
- Create: `src/backend/src/BigSchool.Application/Common/ApiError.cs`
- Create: `src/backend/src/BigSchool.Application/Common/ApiResponse.cs`
- Create: `src/backend/tests/BigSchool.Domain.Tests/Exceptions/DomainExceptionTests.cs`

- [x] **Step 1: Escribir tests para excepciones**

```csharp
// tests/BigSchool.Domain.Tests/Exceptions/DomainExceptionTests.cs
using BigSchool.Domain.Exceptions;
using FluentAssertions;

namespace BigSchool.Domain.Tests.Exceptions;

public class DomainExceptionTests
{
    [Fact]
    public void NotFoundException_HasCodeAndEntityInfo()
    {
        var ex = new NotFoundException("User", 42);

        ex.Message.Should().Contain("User");
        ex.Message.Should().Contain("42");
        ex.ErrorCode.Should().Be("ENTITY_NOT_FOUND");
    }

    [Fact]
    public void ConflictException_HasCodeAndMessage()
    {
        var ex = new ConflictException("EMAIL_ALREADY_EXISTS", "Ya existe un usuario con ese email.");

        ex.Message.Should().Be("Ya existe un usuario con ese email.");
        ex.ErrorCode.Should().Be("EMAIL_ALREADY_EXISTS");
    }

    [Fact]
    public void InvalidCredentialsDomainException_HasSpecificCode()
    {
        var ex = new InvalidCredentialsDomainException();

        ex.ErrorCode.Should().Be("INVALID_CREDENTIALS");
        ex.Message.Should().Contain("inválidas");
    }

    [Fact]
    public void DomainException_IsAbstract_CannotBeInstantiatedDirectly()
    {
        typeof(DomainException).IsAbstract.Should().BeTrue();
    }
}
```

- [x] **Step 2: Ejecutar tests para verificar que fallan**

Run: `dotnet test src/backend/tests/BigSchool.Domain.Tests --filter "FullyQualifiedName~DomainExceptionTests" --no-restore -v q`
Expected: FAIL

- [x] **Step 3: Implementar excepciones de dominio**

```csharp
// src/backend/src/BigSchool.Domain/Exceptions/DomainException.cs
namespace BigSchool.Domain.Exceptions;

public abstract class DomainException : Exception
{
    public string ErrorCode { get; }

    protected DomainException(string errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }

    protected DomainException(string errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}
```

```csharp
// src/backend/src/BigSchool.Domain/Exceptions/NotFoundException.cs
namespace BigSchool.Domain.Exceptions;

public class NotFoundException : DomainException
{
    public NotFoundException(string entityName, object entityId)
        : base("ENTITY_NOT_FOUND", $"Entity '{entityName}' with id '{entityId}' was not found.") { }
}
```

```csharp
// src/backend/src/BigSchool.Domain/Exceptions/ConflictException.cs
namespace BigSchool.Domain.Exceptions;

public class ConflictException : DomainException
{
    public ConflictException(string errorCode, string message) : base(errorCode, message) { }
}
```

```csharp
// src/backend/src/BigSchool.Domain/Exceptions/InvalidCredentialsDomainException.cs
namespace BigSchool.Domain.Exceptions;

public class InvalidCredentialsDomainException : DomainException
{
    public InvalidCredentialsDomainException()
        : base("INVALID_CREDENTIALS", "Las credenciales proporcionadas son inválidas.") { }
}
```

- [x] **Step 4: Ejecutar tests de excepciones**

Run: `dotnet test src/backend/tests/BigSchool.Domain.Tests --filter "FullyQualifiedName~DomainExceptionTests" --no-restore -v q`
Expected: PASS (4 tests)

- [x] **Step 5: Implementar ApiError y ApiResponse (Envelope RFC 7807)**

```csharp
// src/backend/src/BigSchool.Application/Common/ApiError.cs
namespace BigSchool.Application.Common;

/// <summary>
/// Error tipado siguiendo RFC 7807 Problem Details.
/// El frontend puede hacer switch sobre Code para gestionar cada caso.
/// </summary>
public record ApiError
{
    /// <summary>Código de error para switch/case en frontend (ej: "INVALID_CREDENTIALS", "VALIDATION_ERROR")</summary>
    public required string Code { get; init; }

    /// <summary>Mensaje legible para el usuario</summary>
    public required string Message { get; init; }

    /// <summary>Campo al que se refiere el error (null si es general)</summary>
    public string? Field { get; init; }
}
```

```csharp
// src/backend/src/BigSchool.Application/Common/ApiResponse.cs
namespace BigSchool.Application.Common;

public record ApiResponse<T>
{
    public T? Data { get; init; }
    public List<ApiError> Errors { get; init; } = [];
    public MetaData? Meta { get; init; }

    public static ApiResponse<T> Success(T data, MetaData? meta = null)
        => new() { Data = data, Meta = meta };

    public static ApiResponse<T> Fail(params ApiError[] errors)
        => new() { Errors = [.. errors] };
}

public record ApiResponse
{
    public object? Data { get; init; }
    public List<ApiError> Errors { get; init; } = [];
    public MetaData? Meta { get; init; }

    public static ApiResponse Success(object? data = null, MetaData? meta = null)
        => new() { Data = data, Meta = meta };

    public static ApiResponse Fail(params ApiError[] errors)
        => new() { Errors = [.. errors] };
}

public record MetaData
{
    public int? Page { get; init; }
    public int? PageSize { get; init; }
    public int? TotalCount { get; init; }
    public int? TotalPages => TotalCount.HasValue && PageSize.HasValue && PageSize > 0
        ? (int)Math.Ceiling((double)TotalCount.Value / PageSize.Value)
        : null;
}
```

- [x] **Step 6: Commit**

```bash
git add -A && git commit -m "feat: añadir excepciones de dominio tipadas y envelope ApiResponse con RFC 7807"
```

---

### Task 4: ExceptionMiddleware + NotificationExceptionBehavior

**Files:**
- Create: `src/backend/src/BigSchool.WebApi/Middleware/ExceptionHandlingMiddleware.cs`
- Create: `src/backend/src/BigSchool.Application/Behaviors/NotificationExceptionBehavior.cs`
- Modify: `src/backend/src/BigSchool.WebApi/Program.cs` (registrar middleware)
- Create: `src/backend/tests/BigSchool.Application.Tests/Behaviors/NotificationExceptionBehaviorTests.cs`

- [x] **Step 1: Implementar ExceptionHandlingMiddleware con errores tipados**

```csharp
// src/backend/src/BigSchool.WebApi/Middleware/ExceptionHandlingMiddleware.cs
using System.Net;
using System.Text.Json;
using BigSchool.Application.Common;
using BigSchool.Domain.Exceptions;
using FluentValidation;

namespace BigSchool.WebApi.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, errors) = exception switch
        {
            ValidationException validationEx => (
                HttpStatusCode.BadRequest,
                validationEx.Errors.Select(e => new ApiError
                {
                    Code = "VALIDATION_ERROR",
                    Message = e.ErrorMessage,
                    Field = e.PropertyName
                }).ToArray()
            ),
            NotFoundException notFoundEx => (
                HttpStatusCode.NotFound,
                new[] { new ApiError { Code = notFoundEx.ErrorCode, Message = notFoundEx.Message } }
            ),
            ConflictException conflictEx => (
                HttpStatusCode.Conflict,
                new[] { new ApiError { Code = conflictEx.ErrorCode, Message = conflictEx.Message } }
            ),
            DomainException domainEx => (
                HttpStatusCode.BadRequest,
                new[] { new ApiError { Code = domainEx.ErrorCode, Message = domainEx.Message } }
            ),
            UnauthorizedAccessException => (
                HttpStatusCode.Unauthorized,
                new[] { new ApiError { Code = "UNAUTHORIZED", Message = "No autorizado." } }
            ),
            _ => (
                HttpStatusCode.InternalServerError,
                new[] { new ApiError { Code = "INTERNAL_ERROR", Message = "Error interno del servidor." } }
            )
        };

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception");
        }
        else
        {
            _logger.LogWarning(exception, "Handled exception: {ErrorCode}", errors.FirstOrDefault()?.Code);
        }

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var response = ApiResponse.Fail(errors);
        var json = JsonSerializer.Serialize(response, JsonOptions);

        await context.Response.WriteAsync(json);
    }
}
```

- [x] **Step 2: Escribir test para NotificationExceptionBehavior**

```csharp
// tests/BigSchool.Application.Tests/Behaviors/NotificationExceptionBehaviorTests.cs
using BigSchool.Application.Behaviors;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BigSchool.Application.Tests.Behaviors;

public record TestNotification : INotification;

public class NotificationExceptionBehaviorTests
{
    [Fact]
    public async Task Handle_WhenHandlerThrows_LogsErrorAndDoesNotRethrow()
    {
        var loggerMock = new Mock<ILogger<NotificationExceptionBehavior<TestNotification>>>();
        var behavior = new NotificationExceptionBehavior<TestNotification>(loggerMock.Object);

        var act = () => behavior.Handle(
            new TestNotification(),
            () => throw new InvalidOperationException("Handler exploded"),
            CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_WhenHandlerSucceeds_CompletesNormally()
    {
        var loggerMock = new Mock<ILogger<NotificationExceptionBehavior<TestNotification>>>();
        var behavior = new NotificationExceptionBehavior<TestNotification>(loggerMock.Object);
        var called = false;

        await behavior.Handle(
            new TestNotification(),
            () => { called = true; return Task.FromResult(Unit.Value); },
            CancellationToken.None);

        called.Should().BeTrue();
    }
}
```

- [x] **Step 3: Implementar NotificationExceptionBehavior**

```csharp
// src/backend/src/BigSchool.Application/Behaviors/NotificationExceptionBehavior.cs
using MediatR;
using Microsoft.Extensions.Logging;

namespace BigSchool.Application.Behaviors;

/// <summary>
/// Envuelve todos los INotificationHandler con try-catch para evitar que
/// excepciones en handlers de DomainEvents/IntegrationEvents revienten la app.
/// </summary>
public class NotificationExceptionBehavior<TNotification> : IPipelineBehavior<TNotification, Unit>
    where TNotification : INotification
{
    private readonly ILogger<NotificationExceptionBehavior<TNotification>> _logger;

    public NotificationExceptionBehavior(ILogger<NotificationExceptionBehavior<TNotification>> logger)
    {
        _logger = logger;
    }

    public async Task<Unit> Handle(
        TNotification notification,
        RequestHandlerDelegate<Unit> next,
        CancellationToken cancellationToken)
    {
        try
        {
            await next();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error en handler de notification {NotificationType}: {Message}",
                typeof(TNotification).Name, ex.Message);
        }

        return Unit.Value;
    }
}
```

**Nota:** MediatR 12 trata las Notifications como `IRequest<Unit>` internamente cuando se usa pipeline behavior. Si esto no funciona directamente, una alternativa es un wrapper en el dispatch del DbContext. El implementador debe verificar que el behavior se registra correctamente para notifications.

- [x] **Step 4: Registrar middleware en Program.cs**

En `Program.cs`, después de `var app = builder.Build();` y antes de `app.UseSerilogRequestLogging();`:

```csharp
app.UseMiddleware<BigSchool.WebApi.Middleware.ExceptionHandlingMiddleware>();
```

- [x] **Step 5: Ejecutar tests**

Run: `dotnet test src/backend/tests/BigSchool.Application.Tests --filter "FullyQualifiedName~NotificationExceptionBehavior" --no-restore -v q`
Expected: PASS (2 tests)

- [x] **Step 6: Commit**

```bash
git add -A && git commit -m "feat: añadir ExceptionHandlingMiddleware tipado y NotificationExceptionBehavior para protección de eventos"
```

---

### Task 5: EF Core Configurations + Migración Inicial

**Files:**
- Create: `src/backend/src/BigSchool.Infrastructure/Persistence/Configurations/UserConfiguration.cs`
- Create: `src/backend/src/BigSchool.Infrastructure/Persistence/Configurations/SubCategoryConfiguration.cs`
- Create: `src/backend/src/BigSchool.Infrastructure/Persistence/Extensions/SeedDataExtensions.cs`
- Modify: `src/backend/src/BigSchool.Infrastructure/Persistence/BigSchoolDbContext.cs` (añadir SubCategory DbSet y seeds)
- Modify: `src/backend/src/BigSchool.Infrastructure/BigSchool.Infrastructure.csproj` (EF Core Design)

- [x] **Step 1: Implementar UserConfiguration con métodos organizados**

```csharp
// src/backend/src/BigSchool.Infrastructure/Persistence/Configurations/UserConfiguration.cs
using BigSchool.Domain.Entities;
using BigSchool.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BigSchool.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.IdUser);

        ConfigureProperties(builder);
        ConfigureRelationships(builder);
        ConfigureIndexes(builder);
        ConfigureFilters(builder);

        builder.Ignore(u => u.DomainEvents);
    }

    private static void ConfigureProperties(EntityTypeBuilder<User> builder)
    {
        builder.Property(u => u.IdUser).ValueGeneratedOnAdd();
        builder.Property(u => u.Email).IsRequired().HasMaxLength(255);
        builder.Property(u => u.PasswordHash).IsRequired().HasMaxLength(512);
        builder.Property(u => u.PasswordSalt).IsRequired().HasMaxLength(256);
        builder.Property(u => u.FullName).IsRequired().HasMaxLength(200);
        builder.Property(u => u.LastLoginDate);
        builder.Property(u => u.IdStatus)
            .IsRequired()
            .HasDefaultValue(EntityStatus.Active)
            .HasConversion<short>();
        builder.Property(u => u.CreatedAt).IsRequired();
        builder.Property(u => u.UpdatedAt);
    }

    private static void ConfigureRelationships(EntityTypeBuilder<User> builder)
    {
        // SubCategory es entidad hija navegable de User
        builder.HasMany(u => u.SubCategories)
            .WithOne()
            .HasForeignKey("IdUser")
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        // Acceso al backing field para la colección privada
        builder.Navigation(u => u.SubCategories)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureIndexes(EntityTypeBuilder<User> builder)
    {
        builder.HasIndex(u => u.Email).IsUnique();
    }

    private static void ConfigureFilters(EntityTypeBuilder<User> builder)
    {
        builder.HasQueryFilter(u => u.IdStatus != EntityStatus.Deleted);
    }
}
```

- [x] **Step 2: Implementar SubCategoryConfiguration**

```csharp
// src/backend/src/BigSchool.Infrastructure/Persistence/Configurations/SubCategoryConfiguration.cs
using BigSchool.Domain.Entities;
using BigSchool.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BigSchool.Infrastructure.Persistence.Configurations;

public class SubCategoryConfiguration : IEntityTypeConfiguration<SubCategory>
{
    public void Configure(EntityTypeBuilder<SubCategory> builder)
    {
        builder.ToTable("SubCategories");
        builder.HasKey(s => s.IdSubCategory);

        ConfigureProperties(builder);
        ConfigureFilters(builder);

        builder.Ignore(s => s.DomainEvents);
    }

    private static void ConfigureProperties(EntityTypeBuilder<SubCategory> builder)
    {
        builder.Property(s => s.IdSubCategory).ValueGeneratedOnAdd();
        builder.Property(s => s.IdMainCategory).IsRequired().HasConversion<int>();
        builder.Property(s => s.Name).IsRequired().HasMaxLength(100);
        builder.Property(s => s.IsDefault).HasDefaultValue(false);
        builder.Property(s => s.IdStatus)
            .IsRequired()
            .HasDefaultValue(EntityStatus.Active)
            .HasConversion<short>();
        builder.Property(s => s.CreatedAt).IsRequired();
        // Shadow property IdUser (FK gestionada en UserConfiguration)
        builder.Property<int?>("IdUser");
    }

    private static void ConfigureFilters(EntityTypeBuilder<SubCategory> builder)
    {
        builder.HasQueryFilter(s => s.IdStatus != EntityStatus.Deleted);
    }
}
```

**Nota:** La FK `IdUser` se define como shadow property en SubCategoryConfiguration y la relación se configura en UserConfiguration. SubCategory no expone `IdUser` como propiedad pública — EF Core la gestiona internamente. Las subcategorías predefinidas (seeds) tienen `IdUser = null` (globales para todos los usuarios).

- [x] **Step 3: Crear SeedDataExtensions**

```csharp
// src/backend/src/BigSchool.Infrastructure/Persistence/Extensions/SeedDataExtensions.cs
using BigSchool.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BigSchool.Infrastructure.Persistence.Extensions;

public static class SeedDataExtensions
{
    private static readonly DateTime SeedDate = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static ModelBuilder SeedSubCategories(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Domain.Entities.SubCategory>().HasData(
            // Gastos Necesarios
            new { IdSubCategory = 1, IdMainCategory = MainCategory.EssentialExpenses, Name = "Supermercado", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdSubCategory = 2, IdMainCategory = MainCategory.EssentialExpenses, Name = "Farmacia", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdSubCategory = 3, IdMainCategory = MainCategory.EssentialExpenses, Name = "Facturas", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdSubCategory = 4, IdMainCategory = MainCategory.EssentialExpenses, Name = "Seguros", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdSubCategory = 5, IdMainCategory = MainCategory.EssentialExpenses, Name = "Transporte", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            // Inversión
            new { IdSubCategory = 6, IdMainCategory = MainCategory.Investment, Name = "Bolsa", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdSubCategory = 7, IdMainCategory = MainCategory.Investment, Name = "Fondos", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdSubCategory = 8, IdMainCategory = MainCategory.Investment, Name = "Crypto", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            // Ahorro
            new { IdSubCategory = 9, IdMainCategory = MainCategory.Savings, Name = "Cuenta ahorro", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdSubCategory = 10, IdMainCategory = MainCategory.Savings, Name = "Depósitos", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            // Lujos
            new { IdSubCategory = 11, IdMainCategory = MainCategory.Luxuries, Name = "Restaurantes", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdSubCategory = 12, IdMainCategory = MainCategory.Luxuries, Name = "Ocio", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdSubCategory = 13, IdMainCategory = MainCategory.Luxuries, Name = "Viajes", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdSubCategory = 14, IdMainCategory = MainCategory.Luxuries, Name = "Ropa", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdSubCategory = 15, IdMainCategory = MainCategory.Luxuries, Name = "Tecnología", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            // Educación
            new { IdSubCategory = 16, IdMainCategory = MainCategory.Education, Name = "Cursos", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdSubCategory = 17, IdMainCategory = MainCategory.Education, Name = "Libros", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdSubCategory = 18, IdMainCategory = MainCategory.Education, Name = "Máster", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            // Donaciones
            new { IdSubCategory = 19, IdMainCategory = MainCategory.Donations, Name = "ONG", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            // Amortizaciones
            new { IdSubCategory = 20, IdMainCategory = MainCategory.Amortizations, Name = "Hipoteca", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdSubCategory = 21, IdMainCategory = MainCategory.Amortizations, Name = "Préstamo", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            // Nómina
            new { IdSubCategory = 22, IdMainCategory = MainCategory.Salary, Name = "Empresa principal", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            // Alquileres
            new { IdSubCategory = 23, IdMainCategory = MainCategory.Rentals, Name = "Vivienda", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdSubCategory = 24, IdMainCategory = MainCategory.Rentals, Name = "Local", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdSubCategory = 25, IdMainCategory = MainCategory.Rentals, Name = "Garaje", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            // Dividendos
            new { IdSubCategory = 26, IdMainCategory = MainCategory.Dividends, Name = "Acciones nacionales", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdSubCategory = 27, IdMainCategory = MainCategory.Dividends, Name = "Acciones internacionales", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            // Otros
            new { IdSubCategory = 28, IdMainCategory = MainCategory.Other, Name = "Otros ingresos", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate }
        );

        return modelBuilder;
    }
}
```

- [x] **Step 4: Actualizar BigSchoolDbContext — añadir SubCategory y llamar seeds**

```csharp
    // Añadir a la sección de entidades hijas:
    public DbSet<SubCategory> SubCategories => Set<SubCategory>();

    // En OnModelCreating, después de ApplyConfigurationsFromAssembly:
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BigSchoolDbContext).Assembly);
        modelBuilder.SeedSubCategories();
    }
```

Añadir `using BigSchool.Infrastructure.Persistence.Extensions;` al archivo.

- [x] **Step 5: Añadir paquete EF Core Design y generar migración**

```bash
cd src/backend
dotnet add src/BigSchool.Infrastructure/BigSchool.Infrastructure.csproj package Microsoft.EntityFrameworkCore.Design --version 8.0.11
dotnet ef migrations add InitialCreate --project src/BigSchool.Infrastructure --startup-project src/BigSchool.WebApi --output-dir Persistence/Migrations
```

- [x] **Step 6: Verificar build**

Run: `dotnet build src/backend --no-restore -v q`
Expected: 0 errors

- [x] **Step 7: Commit**

```bash
git add -A && git commit -m "feat: añadir EF Core configurations organizadas, seeds en extension y migración inicial"
```

---

### Task 6: Servicios Auth — Argon2 + JWT con DPAPI

**Files:**
- Create: `src/backend/src/BigSchool.Application/Interfaces/Services/IPasswordHasher.cs`
- Create: `src/backend/src/BigSchool.Application/Interfaces/Services/IJwtService.cs`
- Create: `src/backend/src/BigSchool.Application/Interfaces/Services/IUserIdEncryptor.cs`
- Create: `src/backend/src/BigSchool.Infrastructure/Services/Argon2PasswordHasher.cs`
- Create: `src/backend/src/BigSchool.Infrastructure/Services/JwtService.cs`
- Create: `src/backend/src/BigSchool.Infrastructure/Services/DataProtectionUserIdEncryptor.cs`
- Modify: `src/backend/src/BigSchool.Infrastructure/BigSchool.Infrastructure.csproj` (JWT + DataProtection packages)
- Create: `src/backend/tests/BigSchool.Application.Tests/Services/PasswordHasherTests.cs`
- Create: `src/backend/tests/BigSchool.Application.Tests/Services/JwtServiceTests.cs`

- [x] **Step 1: Definir interfaces en Application**

```csharp
// src/backend/src/BigSchool.Application/Interfaces/Services/IPasswordHasher.cs
namespace BigSchool.Application.Interfaces.Services;

public interface IPasswordHasher
{
    (string Hash, string Salt) HashPassword(string password);
    bool VerifyPassword(string password, string hash, string salt);
}
```

```csharp
// src/backend/src/BigSchool.Application/Interfaces/Services/IJwtService.cs
namespace BigSchool.Application.Interfaces.Services;

public record JwtToken(string AccessToken, DateTime ExpiresAt);

public interface IJwtService
{
    JwtToken GenerateToken(int userId, string email);
    int? ExtractUserId(string token);
}
```

```csharp
// src/backend/src/BigSchool.Application/Interfaces/Services/IUserIdEncryptor.cs
namespace BigSchool.Application.Interfaces.Services;

public interface IUserIdEncryptor
{
    string Encrypt(int userId);
    int? Decrypt(string encryptedUserId);
}
```

- [x] **Step 2: Añadir paquetes necesarios**

```bash
cd src/backend
dotnet add src/BigSchool.Infrastructure/BigSchool.Infrastructure.csproj package System.IdentityModel.Tokens.Jwt --version 8.0.2
dotnet add src/BigSchool.Infrastructure/BigSchool.Infrastructure.csproj package Microsoft.IdentityModel.Tokens --version 8.0.2
dotnet add src/BigSchool.Infrastructure/BigSchool.Infrastructure.csproj package Microsoft.AspNetCore.DataProtection --version 8.0.11
```

- [x] **Step 3: Implementar Argon2PasswordHasher con constantes**

```csharp
// src/backend/src/BigSchool.Infrastructure/Services/Argon2PasswordHasher.cs
using System.Security.Cryptography;
using System.Text;
using BigSchool.Application.Interfaces.Services;
using Isopoh.Cryptography.Argon2;

namespace BigSchool.Infrastructure.Services;

public class Argon2PasswordHasher : IPasswordHasher
{
    private const int PARALLELISM = 2;
    private const int MEMORY_COST = 65536;
    private const int ITERATIONS = 3;
    private const int HASH_LENGTH = 16;
    private const int SALT_LENGTH = 32;

    public (string Hash, string Salt) HashPassword(string password)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(SALT_LENGTH);
        var salt = Convert.ToBase64String(saltBytes);

        var config = new Argon2Config
        {
            Type = Argon2Type.HybridAddressing,
            Version = Argon2Version.Nineteen,
            Password = Encoding.UTF8.GetBytes(password),
            Salt = saltBytes,
            Threads = PARALLELISM,
            MemoryCost = MEMORY_COST,
            TimeCost = ITERATIONS,
            HashLength = HASH_LENGTH
        };

        using var argon2 = new Argon2(config);
        using var hashResult = argon2.Hash();
        var hash = Convert.ToBase64String(hashResult.Buffer);

        return (hash, salt);
    }

    public bool VerifyPassword(string password, string hash, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);

        var config = new Argon2Config
        {
            Type = Argon2Type.HybridAddressing,
            Version = Argon2Version.Nineteen,
            Password = Encoding.UTF8.GetBytes(password),
            Salt = saltBytes,
            Threads = PARALLELISM,
            MemoryCost = MEMORY_COST,
            TimeCost = ITERATIONS,
            HashLength = HASH_LENGTH
        };

        using var argon2 = new Argon2(config);
        using var hashResult = argon2.Hash();
        var computedHash = Convert.ToBase64String(hashResult.Buffer);

        return string.Equals(hash, computedHash, StringComparison.Ordinal);
    }
}
```

- [x] **Step 4: Implementar DataProtectionUserIdEncryptor**

```csharp
// src/backend/src/BigSchool.Infrastructure/Services/DataProtectionUserIdEncryptor.cs
using BigSchool.Application.Interfaces.Services;
using Microsoft.AspNetCore.DataProtection;

namespace BigSchool.Infrastructure.Services;

public class DataProtectionUserIdEncryptor : IUserIdEncryptor
{
    private const string PURPOSE = "BigSchool.UserId.v1";
    private readonly IDataProtector _protector;

    public DataProtectionUserIdEncryptor(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector(PURPOSE);
    }

    public string Encrypt(int userId)
    {
        return _protector.Protect(userId.ToString());
    }

    public int? Decrypt(string encryptedUserId)
    {
        try
        {
            var decrypted = _protector.Unprotect(encryptedUserId);
            return int.TryParse(decrypted, out var userId) ? userId : null;
        }
        catch
        {
            return null;
        }
    }
}
```

- [x] **Step 5: Implementar JwtService con userId encriptado**

```csharp
// src/backend/src/BigSchool.Infrastructure/Services/JwtService.cs
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BigSchool.Application.Configuration;
using BigSchool.Application.Interfaces.Services;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BigSchool.Infrastructure.Services;

public class JwtService : IJwtService
{
    private readonly JwtSettings _jwtSettings;
    private readonly IUserIdEncryptor _userIdEncryptor;

    public JwtService(IOptions<AppSettings> settings, IUserIdEncryptor userIdEncryptor)
    {
        _jwtSettings = settings.Value.Jwt;
        _userIdEncryptor = userIdEncryptor;
    }

    public JwtToken GenerateToken(int userId, string email)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes);

        var encryptedUserId = _userIdEncryptor.Encrypt(userId);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, encryptedUserId),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials
        );

        return new JwtToken(
            AccessToken: new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresAt: expiresAt
        );
    }

    public int? ExtractUserId(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        var sub = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;
        return sub is null ? null : _userIdEncryptor.Decrypt(sub);
    }
}
```

- [x] **Step 6: Configurar tests (refs + paquetes)**

```bash
cd src/backend
dotnet add tests/BigSchool.Application.Tests/BigSchool.Application.Tests.csproj reference src/BigSchool.Infrastructure/BigSchool.Infrastructure.csproj
dotnet add tests/BigSchool.Application.Tests/BigSchool.Application.Tests.csproj package System.IdentityModel.Tokens.Jwt --version 8.0.2
dotnet add tests/BigSchool.Application.Tests/BigSchool.Application.Tests.csproj package Microsoft.Extensions.Options --version 9.0.0
dotnet add tests/BigSchool.Application.Tests/BigSchool.Application.Tests.csproj package Microsoft.AspNetCore.DataProtection --version 8.0.11
```

- [x] **Step 7: Escribir tests para PasswordHasher**

```csharp
// tests/BigSchool.Application.Tests/Services/PasswordHasherTests.cs
using BigSchool.Infrastructure.Services;
using FluentAssertions;

namespace BigSchool.Application.Tests.Services;

public class PasswordHasherTests
{
    private readonly Argon2PasswordHasher _hasher = new();

    [Fact]
    public void HashPassword_ReturnsNonEmptyHashAndSalt()
    {
        var (hash, salt) = _hasher.HashPassword("MyP@ssw0rd!");

        hash.Should().NotBeNullOrEmpty();
        salt.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void VerifyPassword_WithCorrectPassword_ReturnsTrue()
    {
        var (hash, salt) = _hasher.HashPassword("MyP@ssw0rd!");

        _hasher.VerifyPassword("MyP@ssw0rd!", hash, salt).Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_WithWrongPassword_ReturnsFalse()
    {
        var (hash, salt) = _hasher.HashPassword("MyP@ssw0rd!");

        _hasher.VerifyPassword("WrongPassword", hash, salt).Should().BeFalse();
    }

    [Fact]
    public void HashPassword_ProducesDifferentSaltsEachTime()
    {
        var (_, salt1) = _hasher.HashPassword("same");
        var (_, salt2) = _hasher.HashPassword("same");

        salt1.Should().NotBe(salt2);
    }
}
```

- [x] **Step 8: Escribir tests para JwtService con DPAPI**

```csharp
// tests/BigSchool.Application.Tests/Services/JwtServiceTests.cs
using System.IdentityModel.Tokens.Jwt;
using BigSchool.Application.Configuration;
using BigSchool.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Xunit;

namespace BigSchool.Application.Tests.Services;

public class JwtServiceTests
{
    private readonly JwtService _jwtService;
    private readonly DataProtectionUserIdEncryptor _encryptor;

    public JwtServiceTests()
    {
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        _encryptor = new DataProtectionUserIdEncryptor(dataProtectionProvider);

        var settings = Options.Create(new AppSettings
        {
            ConnectionString = "unused",
            Jwt = new JwtSettings
            {
                Secret = "SuperSecretKeyForTestingPurposesOnly_32chars!!",
                Issuer = "BigSchool",
                Audience = "BigSchool",
                ExpirationMinutes = 60
            },
            RagService = new RagServiceSettings { BaseUrl = "http://localhost" }
        });
        _jwtService = new JwtService(settings, _encryptor);
    }

    [Fact]
    public void GenerateToken_ReturnsValidJwt()
    {
        var token = _jwtService.GenerateToken(1, "test@example.com");

        token.AccessToken.Should().NotBeNullOrEmpty();
        token.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public void GenerateToken_SubClaimIsEncrypted_NotPlainInt()
    {
        var token = _jwtService.GenerateToken(42, "user@test.com");

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token.AccessToken);
        var sub = jwt.Claims.First(c => c.Type == "sub").Value;

        // El sub NO debe ser "42" en texto plano
        sub.Should().NotBe("42");
        // Pero debe ser decryptable a 42
        _encryptor.Decrypt(sub).Should().Be(42);
    }

    [Fact]
    public void ExtractUserId_WithValidToken_ReturnsUserId()
    {
        var token = _jwtService.GenerateToken(99, "user@test.com");

        var userId = _jwtService.ExtractUserId(token.AccessToken);

        userId.Should().Be(99);
    }

    [Fact]
    public void GenerateToken_ContainsEmailClaim()
    {
        var token = _jwtService.GenerateToken(1, "user@test.com");

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token.AccessToken);

        jwt.Claims.Should().Contain(c => c.Type == "email" && c.Value == "user@test.com");
        jwt.Issuer.Should().Be("BigSchool");
    }
}
```

- [x] **Step 9: Ejecutar todos los tests de servicios**

Run: `dotnet test src/backend/tests/BigSchool.Application.Tests --filter "FullyQualifiedName~Services" --no-restore -v q`
Expected: PASS (8 tests — 4 hasher + 4 JWT)

- [x] **Step 10: Commit**

```bash
git add -A && git commit -m "feat: implementar Argon2PasswordHasher, JwtService con DPAPI y UserIdEncryptor"
```

---

### Task 7: Commands Auth — Register + Login

**Files:**
- Create: `src/backend/src/BigSchool.Application/Commands/Auth/Register/RegisterCommand.cs`
- Create: `src/backend/src/BigSchool.Application/Commands/Auth/Register/RegisterCommandHandler.cs`
- Create: `src/backend/src/BigSchool.Application/Commands/Auth/Register/RegisterCommandValidator.cs`
- Create: `src/backend/src/BigSchool.Application/Commands/Auth/Login/LoginCommand.cs`
- Create: `src/backend/src/BigSchool.Application/Commands/Auth/Login/LoginCommandHandler.cs`
- Create: `src/backend/src/BigSchool.Application/Commands/Auth/Login/LoginCommandValidator.cs`
- Create: `src/backend/src/BigSchool.Application/DTOs/Auth/AuthResponseDto.cs`
- Create: `src/backend/src/BigSchool.Domain/Exceptions/EmailAlreadyExistsDomainException.cs`
- Modify: `src/backend/src/BigSchool.Application/Interfaces/Repositories/IUserRepository.cs`
- Create: `src/backend/tests/BigSchool.Application.Tests/Commands/Auth/RegisterCommandHandlerTests.cs`
- Create: `src/backend/tests/BigSchool.Application.Tests/Commands/Auth/LoginCommandHandlerTests.cs`

- [ ] **Step 1: Crear excepción específica para email duplicado**

```csharp
// src/backend/src/BigSchool.Domain/Exceptions/EmailAlreadyExistsDomainException.cs
namespace BigSchool.Domain.Exceptions;

public class EmailAlreadyExistsDomainException : ConflictException
{
    public EmailAlreadyExistsDomainException(string email)
        : base("EMAIL_ALREADY_EXISTS", $"Ya existe un usuario registrado con el email '{email}'.") { }
}
```

- [ ] **Step 2: Ampliar IUserRepository**

```csharp
// src/backend/src/BigSchool.Application/Interfaces/Repositories/IUserRepository.cs
using BigSchool.Domain.Entities;

namespace BigSchool.Application.Interfaces.Repositories;

public interface IUserRepository : IRepository<User, int>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> ExistsWithEmailAsync(string email, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 3: Crear DTO de respuesta**

```csharp
// src/backend/src/BigSchool.Application/DTOs/Auth/AuthResponseDto.cs
namespace BigSchool.Application.DTOs.Auth;

public record AuthResponseDto(string AccessToken, DateTime ExpiresAt, string Email, string FullName);
```

- [ ] **Step 4: Implementar RegisterCommand + Validator + Handler**

```csharp
// src/backend/src/BigSchool.Application/Commands/Auth/Register/RegisterCommand.cs
using BigSchool.Application.DTOs.Auth;
using MediatR;

namespace BigSchool.Application.Commands.Auth.Register;

public record RegisterCommand(string Email, string Password, string FullName) : IRequest<AuthResponseDto>;
```

```csharp
// src/backend/src/BigSchool.Application/Commands/Auth/Register/RegisterCommandValidator.cs
using FluentValidation;

namespace BigSchool.Application.Commands.Auth.Register;

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El email es obligatorio.")
            .EmailAddress().WithMessage("El formato del email no es válido.")
            .MaximumLength(255);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contraseña es obligatoria.")
            .MinimumLength(8).WithMessage("La contraseña debe tener al menos 8 caracteres.")
            .MaximumLength(100);

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("El nombre completo es obligatorio.")
            .MaximumLength(200);
    }
}
```

```csharp
// src/backend/src/BigSchool.Application/Commands/Auth/Register/RegisterCommandHandler.cs
using BigSchool.Application.DTOs.Auth;
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Application.Interfaces.Services;
using BigSchool.Domain.Entities;
using BigSchool.Domain.Exceptions;
using MediatR;

namespace BigSchool.Application.Commands.Auth.Register;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, AuthResponseDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;

    public RegisterCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtService jwtService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
    }

    public async Task<AuthResponseDto> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        if (await _userRepository.ExistsWithEmailAsync(request.Email, cancellationToken))
        {
            throw new EmailAlreadyExistsDomainException(request.Email);
        }

        var (hash, salt) = _passwordHasher.HashPassword(request.Password);
        var user = User.Create(request.Email, hash, salt, request.FullName);

        await _userRepository.AddAsync(user, cancellationToken);
        await _userRepository.UnitOfWork.SaveChangesAsync();

        var token = _jwtService.GenerateToken(user.IdUser, user.Email);

        return new AuthResponseDto(token.AccessToken, token.ExpiresAt, user.Email, user.FullName);
    }
}
```

- [ ] **Step 5: Implementar LoginCommand + Validator + Handler**

```csharp
// src/backend/src/BigSchool.Application/Commands/Auth/Login/LoginCommand.cs
using BigSchool.Application.DTOs.Auth;
using MediatR;

namespace BigSchool.Application.Commands.Auth.Login;

public record LoginCommand(string Email, string Password) : IRequest<AuthResponseDto>;
```

```csharp
// src/backend/src/BigSchool.Application/Commands/Auth/Login/LoginCommandValidator.cs
using FluentValidation;

namespace BigSchool.Application.Commands.Auth.Login;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El email es obligatorio.")
            .EmailAddress().WithMessage("El formato del email no es válido.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contraseña es obligatoria.");
    }
}
```

```csharp
// src/backend/src/BigSchool.Application/Commands/Auth/Login/LoginCommandHandler.cs
using BigSchool.Application.DTOs.Auth;
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Application.Interfaces.Services;
using BigSchool.Domain.Exceptions;
using MediatR;

namespace BigSchool.Application.Commands.Auth.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResponseDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;

    public LoginCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtService jwtService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
    }

    public async Task<AuthResponseDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);

        if (user is null || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            throw new InvalidCredentialsDomainException();
        }

        user.UpdateLastLogin();
        await _userRepository.UnitOfWork.SaveChangesAsync();

        var token = _jwtService.GenerateToken(user.IdUser, user.Email);

        return new AuthResponseDto(token.AccessToken, token.ExpiresAt, user.Email, user.FullName);
    }
}
```

- [ ] **Step 6: Escribir tests para RegisterCommandHandler**

```csharp
// tests/BigSchool.Application.Tests/Commands/Auth/RegisterCommandHandlerTests.cs
using BigSchool.Application.Commands.Auth.Register;
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Application.Interfaces.Services;
using BigSchool.Domain.Entities;
using BigSchool.Domain.Exceptions;
using BigSchool.Domain.Interfaces;
using FluentAssertions;
using Moq;

namespace BigSchool.Application.Tests.Commands.Auth;

public class RegisterCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IPasswordHasher> _hasherMock = new();
    private readonly Mock<IJwtService> _jwtMock = new();
    private readonly RegisterCommandHandler _handler;

    public RegisterCommandHandlerTests()
    {
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<bool>())).ReturnsAsync(1);
        _userRepoMock.Setup(r => r.UnitOfWork).Returns(unitOfWorkMock.Object);

        _hasherMock.Setup(h => h.HashPassword(It.IsAny<string>()))
            .Returns(("hashed", "salted"));
        _jwtMock.Setup(j => j.GenerateToken(It.IsAny<int>(), It.IsAny<string>()))
            .Returns(new JwtToken("token123", DateTime.UtcNow.AddHours(1)));

        _handler = new RegisterCommandHandler(_userRepoMock.Object, _hasherMock.Object, _jwtMock.Object);
    }

    [Fact]
    public async Task Handle_NewUser_ReturnsTokenAndCreatesUser()
    {
        _userRepoMock.Setup(r => r.ExistsWithEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new RegisterCommand("new@test.com", "Password1!", "John Doe");
        var result = await _handler.Handle(command, CancellationToken.None);

        result.AccessToken.Should().Be("token123");
        result.Email.Should().Be("new@test.com");
        result.FullName.Should().Be("John Doe");
        _userRepoMock.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ExistingEmail_ThrowsEmailAlreadyExistsDomainException()
    {
        _userRepoMock.Setup(r => r.ExistsWithEmailAsync("existing@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new RegisterCommand("existing@test.com", "Password1!", "User");
        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<EmailAlreadyExistsDomainException>();
    }
}
```

- [ ] **Step 7: Escribir tests para LoginCommandHandler**

```csharp
// tests/BigSchool.Application.Tests/Commands/Auth/LoginCommandHandlerTests.cs
using BigSchool.Application.Commands.Auth.Login;
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Application.Interfaces.Services;
using BigSchool.Domain.Entities;
using BigSchool.Domain.Exceptions;
using BigSchool.Domain.Interfaces;
using FluentAssertions;
using Moq;

namespace BigSchool.Application.Tests.Commands.Auth;

public class LoginCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IPasswordHasher> _hasherMock = new();
    private readonly Mock<IJwtService> _jwtMock = new();
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<bool>())).ReturnsAsync(1);
        _userRepoMock.Setup(r => r.UnitOfWork).Returns(unitOfWorkMock.Object);

        _jwtMock.Setup(j => j.GenerateToken(It.IsAny<int>(), It.IsAny<string>()))
            .Returns(new JwtToken("logintoken", DateTime.UtcNow.AddHours(1)));

        _handler = new LoginCommandHandler(_userRepoMock.Object, _hasherMock.Object, _jwtMock.Object);
    }

    [Fact]
    public async Task Handle_ValidCredentials_ReturnsToken()
    {
        var user = User.Create("user@test.com", "hashed", "salted", "Test User");
        _userRepoMock.Setup(r => r.GetByEmailAsync("user@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _hasherMock.Setup(h => h.VerifyPassword("correct", "hashed", "salted"))
            .Returns(true);

        var command = new LoginCommand("user@test.com", "correct");
        var result = await _handler.Handle(command, CancellationToken.None);

        result.AccessToken.Should().Be("logintoken");
        result.Email.Should().Be("user@test.com");
    }

    [Fact]
    public async Task Handle_WrongPassword_ThrowsInvalidCredentialsDomainException()
    {
        var user = User.Create("user@test.com", "hashed", "salted", "Test User");
        _userRepoMock.Setup(r => r.GetByEmailAsync("user@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _hasherMock.Setup(h => h.VerifyPassword("wrong", "hashed", "salted"))
            .Returns(false);

        var command = new LoginCommand("user@test.com", "wrong");
        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidCredentialsDomainException>();
    }

    [Fact]
    public async Task Handle_NonExistentUser_ThrowsInvalidCredentialsDomainException()
    {
        _userRepoMock.Setup(r => r.GetByEmailAsync("ghost@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = new LoginCommand("ghost@test.com", "any");
        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidCredentialsDomainException>();
    }
}
```

- [ ] **Step 8: Ejecutar todos los tests de Auth handlers**

Run: `dotnet test src/backend/tests/BigSchool.Application.Tests --filter "FullyQualifiedName~Commands.Auth" --no-restore -v q`
Expected: PASS (5 tests)

- [ ] **Step 9: Commit**

```bash
git add -A && git commit -m "feat: implementar commands Register y Login con excepciones específicas y tests"
```

---

### Task 8: AuthController + JWT Middleware + UserRepository

**Files:**
- Create: `src/backend/src/BigSchool.Infrastructure/Persistence/Repositories/UserRepository.cs`
- Create: `src/backend/src/BigSchool.WebApi/Controllers/AuthController.cs`
- Modify: `src/backend/src/BigSchool.WebApi/Program.cs` (JWT authentication + DataProtection)

- [ ] **Step 1: Implementar UserRepository (EF Core)**

```csharp
// src/backend/src/BigSchool.Infrastructure/Persistence/Repositories/UserRepository.cs
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BigSchool.Infrastructure.Persistence.Repositories;

public class UserRepository : EFRepository<User, int>, IUserRepository
{
    public UserRepository(BigSchoolDbContext context) : base(context) { }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return await Context.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);
    }

    public async Task<bool> ExistsWithEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return await Context.Users
            .AnyAsync(u => u.Email == normalizedEmail, cancellationToken);
    }
}
```

- [ ] **Step 2: Implementar AuthController**

```csharp
// src/backend/src/BigSchool.WebApi/Controllers/AuthController.cs
using BigSchool.Application.Commands.Auth.Login;
using BigSchool.Application.Commands.Auth.Register;
using BigSchool.Application.Common;
using BigSchool.Application.DTOs.Auth;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BigSchool.WebApi.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(ApiResponse<AuthResponseDto>.Success(result));
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Login([FromBody] LoginCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(ApiResponse<AuthResponseDto>.Success(result));
    }
}
```

- [ ] **Step 3: Configurar JWT Authentication + DataProtection en Program.cs**

Añadir antes de `var app = builder.Build();`:

```csharp
// Data Protection (para encrypt de userId en JWT)
builder.Services.AddDataProtection();

// JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"]!;
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(jwtSecret))
    };
});
builder.Services.AddAuthorization();
```

En el pipeline, después de `app.UseCors();` y antes de `app.MapControllers();`:

```csharp
app.UseAuthentication();
app.UseAuthorization();
```

- [ ] **Step 4: Verificar build**

Run: `dotnet build src/backend --no-restore -v q`
Expected: 0 errors

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat: añadir AuthController, UserRepository, pipeline JWT y DataProtection"
```

---

### Task 9: ValidationBehavior (Pipeline MediatR)

**Files:**
- Create: `src/backend/src/BigSchool.Application/Behaviors/ValidationBehavior.cs`
- Modify: `src/backend/src/BigSchool.WebApi/Program.cs` (registrar behavior)
- Create: `src/backend/tests/BigSchool.Application.Tests/Behaviors/ValidationBehaviorTests.cs`

- [ ] **Step 1: Escribir tests para ValidationBehavior**

```csharp
// tests/BigSchool.Application.Tests/Behaviors/ValidationBehaviorTests.cs
using BigSchool.Application.Behaviors;
using FluentAssertions;
using FluentValidation;
using MediatR;

namespace BigSchool.Application.Tests.Behaviors;

public class ValidationBehaviorTests
{
    private record TestRequest(string Name) : IRequest<string>;

    private class TestRequestValidator : AbstractValidator<TestRequest>
    {
        public TestRequestValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("Name es obligatorio.");
        }
    }

    [Fact]
    public async Task Handle_ValidRequest_CallsNext()
    {
        var validators = new List<IValidator<TestRequest>> { new TestRequestValidator() };
        var behavior = new ValidationBehavior<TestRequest, string>(validators);
        var called = false;

        var result = await behavior.Handle(
            new TestRequest("Valid"),
            () => { called = true; return Task.FromResult("ok"); },
            CancellationToken.None);

        called.Should().BeTrue();
        result.Should().Be("ok");
    }

    [Fact]
    public async Task Handle_InvalidRequest_ThrowsValidationException()
    {
        var validators = new List<IValidator<TestRequest>> { new TestRequestValidator() };
        var behavior = new ValidationBehavior<TestRequest, string>(validators);

        var act = () => behavior.Handle(
            new TestRequest(""),
            () => Task.FromResult("should not reach"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .Where(ex => ex.Errors.Any(e => e.ErrorMessage.Contains("obligatorio")));
    }

    [Fact]
    public async Task Handle_NoValidators_CallsNext()
    {
        var validators = new List<IValidator<TestRequest>>();
        var behavior = new ValidationBehavior<TestRequest, string>(validators);
        var called = false;

        await behavior.Handle(
            new TestRequest("anything"),
            () => { called = true; return Task.FromResult("ok"); },
            CancellationToken.None);

        called.Should().BeTrue();
    }
}
```

- [ ] **Step 2: Ejecutar tests para verificar que fallan**

Run: `dotnet test src/backend/tests/BigSchool.Application.Tests --filter "FullyQualifiedName~ValidationBehaviorTests" --no-restore -v q`
Expected: FAIL — `ValidationBehavior` no existe

- [ ] **Step 3: Implementar ValidationBehavior**

```csharp
// src/backend/src/BigSchool.Application/Behaviors/ValidationBehavior.cs
using FluentValidation;
using MediatR;

namespace BigSchool.Application.Behaviors;

public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        return await next();
    }
}
```

- [ ] **Step 4: Registrar ValidationBehavior en Program.cs**

Modificar la configuración de MediatR en `Program.cs`:

```csharp
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(AppSettings).Assembly);
    cfg.AddOpenBehavior(typeof(BigSchool.Application.Behaviors.ValidationBehavior<,>));
});
```

- [ ] **Step 5: Ejecutar tests**

Run: `dotnet test src/backend/tests/BigSchool.Application.Tests --filter "FullyQualifiedName~ValidationBehaviorTests" --no-restore -v q`
Expected: PASS (3 tests)

- [ ] **Step 6: Commit**

```bash
git add -A && git commit -m "feat: añadir ValidationBehavior para validación automática en pipeline MediatR"
```

---

### Task 10: Tests de Validators + Verificación End-to-End

**Files:**
- Create: `src/backend/tests/BigSchool.Application.Tests/Validators/RegisterCommandValidatorTests.cs`
- Create: `src/backend/tests/BigSchool.Application.Tests/Validators/LoginCommandValidatorTests.cs`

- [ ] **Step 1: Tests para RegisterCommandValidator**

```csharp
// tests/BigSchool.Application.Tests/Validators/RegisterCommandValidatorTests.cs
using BigSchool.Application.Commands.Auth.Register;
using FluentValidation.TestHelper;

namespace BigSchool.Application.Tests.Validators;

public class RegisterCommandValidatorTests
{
    private readonly RegisterCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_NoErrors()
    {
        var result = _validator.TestValidate(new RegisterCommand("test@email.com", "P@ssw0rd!", "John Doe"));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyEmail_HasError()
    {
        var result = _validator.TestValidate(new RegisterCommand("", "P@ssw0rd!", "John Doe"));
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Validate_InvalidEmailFormat_HasError()
    {
        var result = _validator.TestValidate(new RegisterCommand("notanemail", "P@ssw0rd!", "John Doe"));
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Validate_ShortPassword_HasError()
    {
        var result = _validator.TestValidate(new RegisterCommand("test@email.com", "short", "John Doe"));
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Validate_EmptyFullName_HasError()
    {
        var result = _validator.TestValidate(new RegisterCommand("test@email.com", "P@ssw0rd!", ""));
        result.ShouldHaveValidationErrorFor(x => x.FullName);
    }

    [Fact]
    public void Validate_PasswordExactly8Chars_NoError()
    {
        var result = _validator.TestValidate(new RegisterCommand("test@email.com", "12345678", "John Doe"));
        result.ShouldNotHaveValidationErrorFor(x => x.Password);
    }
}
```

- [ ] **Step 2: Tests para LoginCommandValidator**

```csharp
// tests/BigSchool.Application.Tests/Validators/LoginCommandValidatorTests.cs
using BigSchool.Application.Commands.Auth.Login;
using FluentValidation.TestHelper;

namespace BigSchool.Application.Tests.Validators;

public class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_NoErrors()
    {
        var result = _validator.TestValidate(new LoginCommand("user@test.com", "password"));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyEmail_HasError()
    {
        var result = _validator.TestValidate(new LoginCommand("", "password"));
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Validate_InvalidEmail_HasError()
    {
        var result = _validator.TestValidate(new LoginCommand("notvalid", "password"));
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Validate_EmptyPassword_HasError()
    {
        var result = _validator.TestValidate(new LoginCommand("user@test.com", ""));
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }
}
```

- [ ] **Step 3: Ejecutar todos los tests del proyecto**

Run: `dotnet test src/backend --no-restore -v q`
Expected: All tests passed

- [ ] **Step 4: Verificar build completo**

```bash
cd src/backend
dotnet build -v q
```

Expected: Build succeeded, 0 errors

- [ ] **Step 5: (Con MySQL Docker) Aplicar migraciones y probar endpoints**

```bash
# Levantar MySQL
cd infra && docker compose up -d mysql

# Aplicar migración
cd src/backend
dotnet ef database update --project src/BigSchool.Infrastructure --startup-project src/BigSchool.WebApi

# Arrancar API
cd src/BigSchool.WebApi && dotnet run --urls "http://localhost:5000" &

# Probar endpoints
curl -s http://localhost:5000/health
# → Healthy

curl -s -X POST http://localhost:5000/api/v1/auth/register \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"test@bigschool.com\",\"password\":\"P@ssw0rd1!\",\"fullName\":\"Test User\"}"
# → 200: { "data": { "accessToken": "...", ... }, "errors": [], "meta": null }

curl -s -X POST http://localhost:5000/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"test@bigschool.com\",\"password\":\"P@ssw0rd1!\"}"
# → 200: token JWT

curl -s -X POST http://localhost:5000/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"test@bigschool.com\",\"password\":\"wrong\"}"
# → 400: { "errors": [{ "code": "INVALID_CREDENTIALS", "message": "Las credenciales..." }] }

curl -s -X POST http://localhost:5000/api/v1/auth/register \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"\",\"password\":\"short\",\"fullName\":\"\"}"
# → 400: { "errors": [{ "code": "VALIDATION_ERROR", "message": "...", "field": "Email" }, ...] }
```

- [ ] **Step 6: Commit final**

```bash
git add -A && git commit -m "test: añadir tests de validators y verificación end-to-end de auth"
```

---

## Notas de Implementación

- **Argon2id** con constantes: Parallelism=2, MemoryCost=65536, Iterations=3, HashLength=16
- **DPAPI** encrypta el userId antes de ponerlo en el claim `sub` del JWT — el frontend nunca ve INTs internos
- **DomainException es abstract** — fuerza a crear excepciones específicas con `ErrorCode` tipado
- **ApiError RFC 7807** — `{ code, message, field }` permite al frontend hacer `switch(error.code)` sin parsear strings
- **NotificationExceptionBehavior** — envuelve handlers de eventos con try-catch para que una excepción en un handler de DomainEvent no reviente toda la petición
- **Global Query Filter** `IdStatus != Deleted` en cada Configuration → soft delete transparente
- **SubCategory como entidad hija de User** — constructor `private`, factory `internal`, acceso exclusivamente vía `user.AddSubCategory(...)`. No necesita `InternalsVisibleTo`. Se testea a través del AR.
- **Transaction y RagDocument como ARs independientes** — referencian User solo por `IdUser` (int). Tienen su propio repositorio y se crean con `Transaction.Create(...)` / `RagDocument.Create(...)`.
- **EF Core Configurations** — organizadas en métodos `ConfigureProperties`, `ConfigureRelationships`, `ConfigureIndexes`, `ConfigureFilters`
- **Seeds** — en extension method `SeedSubCategories()` con objetos anónimos (no requiere factory en la entidad). Seeds son globales (`IdUser = null`, `IsDefault = true`)
- **Shadow property** `IdUser` en SubCategory — EF Core gestiona la FK internamente sin exponerla en el modelo de dominio

## Dependencias entre Plans

Este plan (Plan 1) es requisito previo para:
- **Plan 2: BC Finanzas Personales** — usa User AR, Auth, Envelope, Middleware, ValidationBehavior, SubCategory (hija de User). Transaction como AR independiente con ITransactionRepository.
- **Plan 3: BC Inversiones** — usa los mismos cross-cutting + Company/Portfolio ARs
- **Plan 4: RAG** — usa Auth + Envelope + RagDocument como AR independiente con IRagDocumentRepository
