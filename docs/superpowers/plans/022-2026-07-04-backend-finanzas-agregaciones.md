# Backend — Finanzas: agregaciones + subcategorías + re-modelado SubCategory Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** (1) Re-modelar `SubCategory` como **Aggregate Root independiente** de Finanzas (deja de ser hija de `User`) y **reactivar** la frontera `Domain.Auth ⊥ Domain.Finanzas`. (2) `POST/DELETE /categories/sub` (#8/C). (3) `GET /transactions/by-category` (#6) y `GET /transactions/monthly` (#7), con validación de rango compartida (`from ≤ to`, span ≤ 4 años).

**Architecture:** `SubCategory` implementa `IAggregateRoot`, `IdUser` pasa de propiedad sombra a explícita (nullable; NULL = global), con repositorio propio, unicidad validada en el command handler e **invariante de borrado en el dominio** (`Delete(requestingUserId)` rechaza predefinidas y no-propietario). `User` pierde `_subCategories`/`AddSubCategory` → deja de referenciar Finanzas → el guard test se cierra. Agregaciones Dapper sobre `BaseAmount` (espejo de `GetTransactionSummary`/`GetMonthlyChart`), no paginadas. Validación de rango como predicado FluentValidation reutilizado.

**Tech Stack:** .NET 8, C#, DDD + CQRS (MediatR), EF Core + MySQL 8, Dapper, FluentValidation, xUnit + FluentAssertions + Moq + `WebApplicationFactory`, NetArchTest.

---

## Convenciones y contexto (LEER ANTES DE EMPEZAR)

> **BASE:** develop con estructura modular (018) + paginación (019). Rama `feature/007-010-backend-features`; cada tarea = rama `feature/022-finanzas-taskN` + PR.

Estado actual verificado en develop:
- `SubCategory : BaseEntity` (NO AR), factory `internal static Create(MainCategory, string name)` con object initializer, **sin** propiedad `IdUser` (sombra `Property<int?>("IdUser")` en `SubCategoryConfiguration`). Props: `IdSubCategory`, `IdMainCategory` (`MainCategory`), `Name`, `IsDefault`, `IdStatus`, `CreatedAt` — `BigSchool.Domain.Finanzas.Entities`.
- `User` tiene `_subCategories` + `SubCategories` + `AddSubCategory(MainCategory, name)` (lanza `DuplicateSubCategoryDomainException`) y `using` de Finanzas — `BigSchool.Domain.Auth.Entities`.
- `UserConfiguration.ConfigureRelationships`: `HasMany(u => u.SubCategories).WithOne().HasForeignKey("IdUser")...` + `Navigation(...).UsePropertyAccessMode(Field)`.
- `SeedDataExtensions.SeedSubCategories`: 28 predefinidas por `HasData(new { …, IdUser = (int?)null, IsDefault = true, … })` (el nombre `IdUser` ya casa con la futura propiedad explícita → **no cambia**).
- `DuplicateSubCategoryDomainException(string name, MainCategory mc)` (code `DUPLICATE_SUBCATEGORY`) : `DomainException` — `BigSchool.Domain.Finanzas.Exceptions`.
- `MonthlyChartPointDto(int Year, int Month, decimal Income, decimal Expense)` y patrón Dapper de `GetMonthlyChartQueryHandler` — `BigSchool.Application.Finanzas.Queries.Transactions.GetMonthlyChart`.
- `CategoriesController` (`/api/v1/categories`, `[Authorize]`, ya inyecta `IUserIdEncryptor` + `CurrentUser.GetId`). `GetCategoriesQuery` lee `WHERE IdUser IS NULL OR IdUser = @IdUser` (**no cambia**).
- `ModuleBoundaryTests` tiene la línea relajada: `[InlineData("BigSchool.Domain.Auth", new[] { "BigSchool.Domain.Investments" })]` (omite Finanzas, con comentario que apunta a esta spec).
- `IRepository`/`EFRepository`, `IDbConnectionFactory`, `EntityStatus`, `NotFoundException`, `TransactionType` — ubicaciones de specs previas. `TransactionsController` en `Controllers/Finanzas` inyecta `IMediator` + `IUserIdEncryptor` (propiedad privada `UserId`).

Recetas: **BUILD** (`dotnet build`, cwd `src/backend`); **UNIT** (`dotnet test tests/BigSchool.Domain.Tests`, `.Application.Tests`); **ARCH** (`dotnet test tests/BigSchool.Architecture.Tests`); **INTEGRATION** (MySQL de `infra/docker-compose.yml`); **FULL** = todo.

---

## Task 1: Re-modelar `SubCategory` como AR independiente + cerrar la frontera

Cambio estructural atómico: `SubCategory` deja de ser hija de `User`. Debe terminar con build+tests verdes y el guard test de frontera **reactivado**.

**Files:**
- Modify: `src/BigSchool.Domain/Finanzas/Entities/SubCategory.cs`
- Modify: `src/BigSchool.Domain/Auth/Entities/User.cs` (quitar subcategorías)
- Modify: `src/BigSchool.Infrastructure/Finanzas/Persistence/Configurations/SubCategoryConfiguration.cs` (IdUser explícito)
- Modify: `src/BigSchool.Infrastructure/Auth/Persistence/Configurations/UserConfiguration.cs` (quitar relación)
- Modify: `tests/BigSchool.Architecture.Tests/ModuleBoundaryTests.cs` (reactivar frontera)
- Delete/adaptar: tests de dominio que usen `User.AddSubCategory`/`User.SubCategories`
- Test: `tests/BigSchool.Domain.Tests/Entities/Finanzas/SubCategoryTests.cs`
- Migration: `RemodelSubCategoryAggregate`

- [x] **Step 1: Test de dominio del nuevo AR (falla)**

`SubCategoryTests.cs` — la invariante de borrado vive en el dominio: `Delete(requestingUserId)` exige propietario y rechaza predefinidas:
```csharp
using BigSchool.Domain.Finanzas.Entities;
using BigSchool.Domain.Finanzas.Enums;
using BigSchool.Domain.SharedKernel.Enums;
using FluentAssertions;
using Xunit;

namespace BigSchool.Domain.Tests.Entities.Finanzas;

public class SubCategoryTests
{
    [Fact]
    public void Create_WithIdUser_SetsIdUserAndIsNotGlobalOrDefault()
    {
        var s = SubCategory.Create(MainCategory.Luxuries, " Cine ", idUser: 7);
        s.IdUser.Should().Be(7);
        s.Name.Should().Be("Cine");
        s.IsGlobal.Should().BeFalse();
        s.IsDefault.Should().BeFalse();
    }

    [Fact]
    public void Create_NullIdUser_IsGlobal()
        => SubCategory.Create(MainCategory.Salary, "Nómina", null).IsGlobal.Should().BeTrue();

    [Fact]
    public void Create_EmptyName_ThrowsArgumentException()
        => FluentActions.Invoking(() => SubCategory.Create(MainCategory.Other, " ", 1)).Should().Throw<System.ArgumentException>();

    [Fact]
    public void Delete_ByOwner_SoftDeletes()
    {
        var s = SubCategory.Create(MainCategory.Other, "X", idUser: 1);
        s.Delete(requestingUserId: 1);
        s.IdStatus.Should().Be(EntityStatus.Deleted);
    }

    [Fact]
    public void Delete_ByOtherUser_ThrowsInvalidOperationException()
    {
        var s = SubCategory.Create(MainCategory.Other, "X", idUser: 1);
        FluentActions.Invoking(() => s.Delete(requestingUserId: 2)).Should().Throw<System.InvalidOperationException>();
    }

    [Fact]
    public void Delete_GlobalSubCategory_ThrowsInvalidOperationException()
    {
        var s = SubCategory.Create(MainCategory.Other, "X", idUser: null); // global
        FluentActions.Invoking(() => s.Delete(requestingUserId: 1)).Should().Throw<System.InvalidOperationException>();
    }
}
```
> El caso `IsDefault=true` (predefinidas del seed) no es construible por la factory pública (`IsDefault` siempre `false`); su rechazo se cubre en E2E (borrar una global/predefinida → 404). El guard del dominio lo contempla igualmente.

Run: `dotnet test tests/BigSchool.Domain.Tests --filter SubCategoryTests` → FAIL.

- [x] **Step 2: `SubCategory` → AR independiente (con invariante de borrado)**

Sustituye `SubCategory.cs` por:
```csharp
using BigSchool.Domain.Finanzas.Enums;
using BigSchool.Domain.SharedKernel.Entities;
using BigSchool.Domain.SharedKernel.Enums;

namespace BigSchool.Domain.Finanzas.Entities;

public class SubCategory : BaseEntity, IAggregateRoot
{
    public int IdSubCategory { get; private set; }
    public MainCategory IdMainCategory { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int? IdUser { get; private set; }   // NULL = global predefinida (referencia suave por Id, sin FK dura)
    public bool IsDefault { get; private set; }
    public EntityStatus IdStatus { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public bool IsGlobal => IdUser is null;

    protected SubCategory() { } // EF Core

    private SubCategory(MainCategory mainCategory, string name, int? idUser, bool isDefault, EntityStatus idStatus, DateTime createdAt)
    {
        IdMainCategory = mainCategory;
        Name = name;
        IdUser = idUser;
        IsDefault = isDefault;
        IdStatus = idStatus;
        CreatedAt = createdAt;
    }

    public static SubCategory Create(MainCategory mainCategory, string name, int? idUser)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        return new SubCategory(mainCategory, name.Trim(), idUser, isDefault: false, EntityStatus.Active, DateTime.UtcNow);
    }

    /// <summary>Solo el propietario puede borrar su subcategoría; nunca una predefinida (IsDefault) ni una global.</summary>
    public void Delete(int requestingUserId)
    {
        if (IsDefault)
            throw new InvalidOperationException("No se puede borrar una subcategoría predefinida.");
        if (IdUser != requestingUserId)
            throw new InvalidOperationException("Solo el propietario puede borrar su subcategoría.");
        IdStatus = EntityStatus.Deleted;
    }
}
```

- [x] **Step 3: `User` deja de conocer Finanzas**

En `User.cs`:
- Elimina los `using` de Finanzas: `using BigSchool.Domain.Finanzas.Entities;`, `using BigSchool.Domain.Finanzas.Enums;`, `using BigSchool.Domain.Finanzas.Exceptions;`.
- Elimina el campo `_subCategories`, la propiedad `SubCategories` y el método `AddSubCategory(...)` completo.

Tras esto `User` **no** referencia ningún tipo de Finanzas (solo `Currency`/`EntityStatus` de SharedKernel).

- [x] **Step 4: Configuraciones EF**

`SubCategoryConfiguration.cs` — sustituye la línea de la sombra por propiedad explícita:
```csharp
        // IdUser explícito (nullable; NULL = global). Referencia suave por Id, SIN FK dura.
        builder.Property(s => s.IdUser);
```
(elimina `builder.Property<int?>("IdUser");`). El resto (query filter, props, `Ignore(DomainEvents)`) no cambia.

`UserConfiguration.cs` — elimina el método `ConfigureRelationships` y su llamada en `Configure`, y el `using BigSchool.Domain.Finanzas.Entities;`. (La FK `SubCategories→Users` vivía ahí; al quitarla, la migración la suelta.)

- [x] **Step 5: Adaptar tests que usaban `User.AddSubCategory`/`SubCategories`**

Run: `grep -rn "AddSubCategory\|\.SubCategories" tests src | grep -v SubCategoryTests` → elimina/adapta esos tests (dominio o integración). La cobertura de `SubCategory` pasa a `SubCategoryTests` (AR directo).

- [x] **Step 6: Reactivar el guard test de frontera**

En `ModuleBoundaryTests.cs`, cambia la línea de `Domain.Auth` y quita el comentario de excepción:
```csharp
    [InlineData("BigSchool.Domain.Auth", new[] { "BigSchool.Domain.Finanzas", "BigSchool.Domain.Investments" })]
```

- [x] **Step 7: Migración (solo suelta la FK)**

Run (cwd `src/backend`): `dotnet ef migrations add RemodelSubCategoryAggregate --project src/BigSchool.Infrastructure --startup-project src/BigSchool.WebApi`
*Expected:* el `Up()` **solo** suelta la FK `SubCategories→Users` (la columna `IdUser` y el resto del esquema no cambian; el seed no cambia). Revisa que no aparezcan alteraciones inesperadas. Luego:
Run: `dotnet ef migrations has-pending-model-changes --project src/BigSchool.Infrastructure --startup-project src/BigSchool.WebApi` → sin pendientes.

- [x] **Step 8: Verde (build + tests + ARCH)**

Run: FULL + ARCH. *Expected:* verde; `ModuleBoundaryTests` con Auth prohibiendo Finanzas **pasa** (frontera cerrada). Los E2E de `GET /categories` siguen verdes (seed intacto).

- [x] **Step 9: Commit**
```bash
git add -A && git commit -m "refactor(finanzas): SubCategory como AR independiente + cierre frontera Auth⊥Finanzas

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 2: #8/C — Subcategorías CRUD (`POST/DELETE /categories/sub`)

**Files:**
- Create: `src/BigSchool.Application/Finanzas/Interfaces/Repositories/ISubCategoryRepository.cs`
- Create: `src/BigSchool.Infrastructure/Finanzas/Persistence/Repositories/SubCategoryRepository.cs`
- Create: `src/BigSchool.Application/Finanzas/DTOs/SubCategoryDto.cs`
- Create: `src/BigSchool.Application/Finanzas/Commands/CreateSubCategory/{CreateSubCategoryCommand,CreateSubCategoryCommandHandler,CreateSubCategoryCommandValidator}.cs`
- Create: `src/BigSchool.Application/Finanzas/Commands/DeleteSubCategory/{DeleteSubCategoryCommand,DeleteSubCategoryCommandHandler}.cs`
- Modify: `src/BigSchool.WebApi/Controllers/Finanzas/CategoriesController.cs`
- Test: `tests/BigSchool.Application.Tests/Commands/Finanzas/{CreateSubCategoryCommandHandlerTests,DeleteSubCategoryCommandHandlerTests}.cs`, `tests/BigSchool.Application.Tests/Validators/Finanzas/CreateSubCategoryCommandValidatorTests.cs`
- Test: `tests/BigSchool.Integration.Tests/Transactions/SubCategoriesCrudTests.cs` (namespace `BigSchool.Integration.Tests.Transactions`; reutiliza `TransactionEndpointTestBase` — no crear carpeta/namespace "Finanzas" nueva, el módulo ya es español en Domain/Application/Infrastructure pero la carpeta E2E existente para este dominio es "Transactions")

- [x] **Step 1: Repositorio con guarda de unicidad**

`ISubCategoryRepository.cs`:
```csharp
using BigSchool.Application.SharedKernel.Interfaces;
using BigSchool.Domain.Finanzas.Entities;
using BigSchool.Domain.Finanzas.Enums;

namespace BigSchool.Application.Finanzas.Interfaces.Repositories;

public interface ISubCategoryRepository : IRepository<SubCategory, int>
{
    // Existe una activa con ese nombre+categoría entre las del usuario O las globales.
    Task<bool> ExistsActiveAsync(int? idUser, MainCategory mainCategory, string name, CancellationToken cancellationToken = default);
}
```
`SubCategoryRepository.cs`:
```csharp
using BigSchool.Application.Finanzas.Interfaces.Repositories;
using BigSchool.Domain.Finanzas.Entities;
using BigSchool.Domain.Finanzas.Enums;
using BigSchool.Infrastructure.SharedKernel.Persistence;
using BigSchool.Infrastructure.SharedKernel.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BigSchool.Infrastructure.Finanzas.Persistence.Repositories;

public class SubCategoryRepository : EFRepository<SubCategory, int>, ISubCategoryRepository
{
    public SubCategoryRepository(BigSchoolDbContext context) : base(context) { }

    public async Task<bool> ExistsActiveAsync(int? idUser, MainCategory mainCategory, string name, CancellationToken cancellationToken = default)
    {
        var trimmed = name.Trim().ToLower();
        // El global query filter ya excluye IdStatus=Deleted → "activa".
        return await Context.Set<SubCategory>().AnyAsync(s =>
            s.IdMainCategory == mainCategory
            && s.Name.ToLower() == trimmed
            && (s.IdUser == null || s.IdUser == idUser), cancellationToken);
    }
}
```
> `SubCategoryRepository` queda registrado por el escaneo Autofac de `FinanzasModule` (Infrastructure.Finanzas). No requiere cambios en DI.

- [x] **Step 2: DTO + Create (command/validator/handler)**

`SubCategoryDto.cs`:
```csharp
namespace BigSchool.Application.Finanzas.DTOs;

public record SubCategoryDto(int IdSubCategory, int IdMainCategory, string MainCategory, string Name);
```
`CreateSubCategoryCommand.cs`:
```csharp
using BigSchool.Application.Finanzas.DTOs;
using BigSchool.Domain.Finanzas.Enums;
using MediatR;

namespace BigSchool.Application.Finanzas.Commands.CreateSubCategory;

public record CreateSubCategoryCommand(int IdUser, MainCategory MainCategory, string Name) : IRequest<SubCategoryDto>;
```
`CreateSubCategoryCommandValidator.cs`:
```csharp
using FluentValidation;

namespace BigSchool.Application.Finanzas.Commands.CreateSubCategory;

public class CreateSubCategoryCommandValidator : AbstractValidator<CreateSubCategoryCommand>
{
    public CreateSubCategoryCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.MainCategory).IsInEnum();
    }
}
```
`CreateSubCategoryCommandHandler.cs`:
```csharp
using BigSchool.Application.Finanzas.DTOs;
using BigSchool.Application.Finanzas.Interfaces.Repositories;
using BigSchool.Domain.Finanzas.Entities;
using BigSchool.Domain.Finanzas.Exceptions;
using MediatR;

namespace BigSchool.Application.Finanzas.Commands.CreateSubCategory;

public class CreateSubCategoryCommandHandler : IRequestHandler<CreateSubCategoryCommand, SubCategoryDto>
{
    private readonly ISubCategoryRepository _subCategories;

    public CreateSubCategoryCommandHandler(ISubCategoryRepository subCategories) => _subCategories = subCategories;

    public async Task<SubCategoryDto> Handle(CreateSubCategoryCommand request, CancellationToken cancellationToken)
    {
        if (await _subCategories.ExistsActiveAsync(request.IdUser, request.MainCategory, request.Name, cancellationToken))
            throw new DuplicateSubCategoryDomainException(request.Name.Trim(), request.MainCategory);

        var sub = SubCategory.Create(request.MainCategory, request.Name, request.IdUser);
        await _subCategories.AddAsync(sub, cancellationToken);
        await _subCategories.UnitOfWork.SaveChangesAsync();

        return new SubCategoryDto(sub.IdSubCategory, (int)sub.IdMainCategory, sub.IdMainCategory.ToString(), sub.Name);
    }
}
```
> `DuplicateSubCategoryDomainException` debía mapear a **409** (mismo trato que `EmailAlreadyExistsDomainException`/`DuplicateTickerDomainException`/`DuplicateValuationDomainException`). Confirmado: el middleware solo mapea `ConflictException` a 409 (`DomainException` genérico → 400); la clase extendía `DomainException`, no `ConflictException`. Fix: cambiar su base a `ConflictException` (no tocar el middleware).

- [x] **Step 3: Delete (command/handler) — 404 no-leak + invariante de dominio**

`DeleteSubCategoryCommand.cs`:
```csharp
using MediatR;

namespace BigSchool.Application.Finanzas.Commands.DeleteSubCategory;

public record DeleteSubCategoryCommand(int IdUser, int IdSubCategory) : IRequest;
```
`DeleteSubCategoryCommandHandler.cs`:
```csharp
using BigSchool.Application.Finanzas.Interfaces.Repositories;
using BigSchool.Domain.SharedKernel.Exceptions;
using MediatR;

namespace BigSchool.Application.Finanzas.Commands.DeleteSubCategory;

public class DeleteSubCategoryCommandHandler : IRequestHandler<DeleteSubCategoryCommand>
{
    private readonly ISubCategoryRepository _subCategories;

    public DeleteSubCategoryCommandHandler(ISubCategoryRepository subCategories) => _subCategories = subCategories;

    public async Task Handle(DeleteSubCategoryCommand request, CancellationToken cancellationToken)
    {
        var sub = await _subCategories.GetByIdAsync(request.IdSubCategory, cancellationToken);
        // Decisión HTTP: 404 no-leak si no existe, es predefinida, global o de otro usuario.
        if (sub is null || sub.IsDefault || sub.IdUser != request.IdUser)
            throw new NotFoundException("Subcategoría no encontrada.");

        sub.Delete(request.IdUser); // el dominio reafirma la invariante (defensa en profundidad)
        await _subCategories.UnitOfWork.SaveChangesAsync();
    }
}
```
> Ajusta el ctor de `NotFoundException` al existente. El pre-check devuelve 404 sin filtrar existencia; el `Delete(requestingUserId)` del dominio nunca fallará aquí (ya se validó), pero protege la invariante ante otros llamadores.

- [x] **Step 4: `CategoriesController` — POST /sub, DELETE /sub/{id}**

Añade a `CategoriesController` (usings de los nuevos commands). **Colisión de nombres detectada**: `Application.Finanzas.Queries.Categories.GetCategories` ya define su propio `SubCategoryDto` (item anidado de `GetCategoriesQuery`, forma distinta: `IdSubCategory, Name, IsDefault`); el controller ya tiene ese `using`. No añadas `using BigSchool.Application.Finanzas.DTOs;` — cualifica el nuevo `SubCategoryDto` (CRUD) por nombre completo en los dos sitios donde se usa:
```csharp
    public record CreateSubCategoryRequest(BigSchool.Domain.Finanzas.Enums.MainCategory MainCategory, string Name);

    [HttpPost("sub")]
    [ProducesResponseType(typeof(ApiResponse<BigSchool.Application.Finanzas.DTOs.SubCategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateSub([FromBody] CreateSubCategoryRequest body)
    {
        var userId = CurrentUser.GetId(User, _encryptor);
        var result = await _mediator.Send(new CreateSubCategoryCommand(userId, body.MainCategory, body.Name));
        return Ok(ApiResponse<BigSchool.Application.Finanzas.DTOs.SubCategoryDto>.Success(result));
    }

    [HttpDelete("sub/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSub(int id)
    {
        var userId = CurrentUser.GetId(User, _encryptor);
        await _mediator.Send(new DeleteSubCategoryCommand(userId, id));
        return Ok(ApiResponse.Success());
    }
```

- [x] **Step 5: Unit tests (validator + handlers)**

`CreateSubCategoryCommandValidatorTests.cs` (estilo real: `TestValidate`/`ShouldHaveValidationErrorFor`): `Validate_EmptyName_HasError`, `Validate_NameTooLong_HasError`, `Validate_MainCategoryOutOfEnum_HasError`, `Validate_ValidCommand_NoErrors`.
`CreateSubCategoryCommandHandlerTests.cs`:
```csharp
using BigSchool.Application.Finanzas.Commands.CreateSubCategory;
using BigSchool.Application.Finanzas.Interfaces.Repositories;
using BigSchool.Domain.Finanzas.Enums;
using BigSchool.Domain.Finanzas.Exceptions;
using BigSchool.Domain.SharedKernel.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace BigSchool.Application.Tests.Commands.Finanzas;

public class CreateSubCategoryCommandHandlerTests
{
    [Fact]
    public async Task Handle_DuplicateName_ThrowsDuplicateSubCategoryDomainException()
    {
        var repo = new Mock<ISubCategoryRepository>();
        repo.Setup(r => r.ExistsActiveAsync(1, MainCategory.Luxuries, "Cine", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var handler = new CreateSubCategoryCommandHandler(repo.Object);

        await FluentActions.Invoking(() => handler.Handle(new CreateSubCategoryCommand(1, MainCategory.Luxuries, "Cine"), CancellationToken.None))
            .Should().ThrowAsync<DuplicateSubCategoryDomainException>();
    }

    [Fact]
    public async Task Handle_NewSubCategory_CreatesAndSaves()
    {
        var repo = new Mock<ISubCategoryRepository>();
        repo.Setup(r => r.ExistsActiveAsync(It.IsAny<int?>(), It.IsAny<MainCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        repo.SetupGet(r => r.UnitOfWork).Returns(Mock.Of<IUnitOfWork>());
        var handler = new CreateSubCategoryCommandHandler(repo.Object);

        var dto = await handler.Handle(new CreateSubCategoryCommand(1, MainCategory.Luxuries, "Cine"), CancellationToken.None);

        dto.Name.Should().Be("Cine");
        repo.Verify(r => r.AddAsync(It.IsAny<BigSchool.Domain.Finanzas.Entities.SubCategory>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
```
`DeleteSubCategoryCommandHandlerTests.cs`:
```csharp
using BigSchool.Application.Finanzas.Commands.DeleteSubCategory;
using BigSchool.Application.Finanzas.Interfaces.Repositories;
using BigSchool.Domain.Finanzas.Entities;
using BigSchool.Domain.Finanzas.Enums;
using BigSchool.Domain.SharedKernel.Exceptions;
using BigSchool.Domain.SharedKernel.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace BigSchool.Application.Tests.Commands.Finanzas;

public class DeleteSubCategoryCommandHandlerTests
{
    private static Mock<ISubCategoryRepository> RepoReturning(SubCategory? sub)
    {
        var repo = new Mock<ISubCategoryRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(sub);
        repo.SetupGet(r => r.UnitOfWork).Returns(Mock.Of<IUnitOfWork>());
        return repo;
    }

    [Fact]
    public async Task Handle_NonExistentSubCategory_ThrowsNotFoundException()
    {
        var handler = new DeleteSubCategoryCommandHandler(RepoReturning(null).Object);
        await FluentActions.Invoking(() => handler.Handle(new DeleteSubCategoryCommand(1, 99), CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_GlobalSubCategory_ThrowsNotFoundException()
    {
        var global = SubCategory.Create(MainCategory.Other, "X", idUser: null);
        var handler = new DeleteSubCategoryCommandHandler(RepoReturning(global).Object);
        await FluentActions.Invoking(() => handler.Handle(new DeleteSubCategoryCommand(1, 1), CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_OtherUsersSubCategory_ThrowsNotFoundException()
    {
        var other = SubCategory.Create(MainCategory.Other, "X", idUser: 2);
        var handler = new DeleteSubCategoryCommandHandler(RepoReturning(other).Object);
        await FluentActions.Invoking(() => handler.Handle(new DeleteSubCategoryCommand(1, 5), CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_OwnSubCategory_SoftDeletes()
    {
        var mine = SubCategory.Create(MainCategory.Other, "X", idUser: 1);
        var handler = new DeleteSubCategoryCommandHandler(RepoReturning(mine).Object);
        await handler.Handle(new DeleteSubCategoryCommand(1, 3), CancellationToken.None);
        mine.IdStatus.Should().Be(BigSchool.Domain.SharedKernel.Enums.EntityStatus.Deleted);
    }
}
```

- [x] **Step 6: E2E**

`SubCategoriesCrudTests.cs` (hereda `TransactionEndpointTestBase`; añade `CountAsync(sql)` genérico ahí si no existe):
```csharp
    [Fact]
    public async Task PostSub_ValidSubCategory_CreatesAndAppearsInGetCategories()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        var resp = await client.PostAsJsonAsync("/api/v1/categories/sub", new { mainCategory = "Luxuries", name = "Cine" });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        (await CountAsync($"SELECT COUNT(*) FROM SubCategories WHERE IdUser={userId} AND Name='Cine'")).Should().Be(1);
        var cats = await (await client.GetAsync("/api/v1/categories")).Content.ReadAsStringAsync();
        cats.Should().Contain("Cine");
    }

    [Fact]
    public async Task PostSub_DuplicateOfGlobal_Returns409()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        // "Supermercado" es global predefinida (seed IdSubCategory=1, EssentialExpenses)
        var resp = await client.PostAsJsonAsync("/api/v1/categories/sub", new { mainCategory = "EssentialExpenses", name = "Supermercado" });
        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DeleteSub_OwnSubCategory_SoftDeletes()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        var created = await (await client.PostAsJsonAsync("/api/v1/categories/sub", new { mainCategory = "Luxuries", name = "Cine" }))
            .Content.ReadFromJsonAsync<ApiEnvelope<SubCategoryResponse>>();
        var id = created!.Data!.IdSubCategory;

        (await client.DeleteAsync($"/api/v1/categories/sub/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await CountAsync($"SELECT COUNT(*) FROM SubCategories WHERE IdSubCategory={id} AND IdStatus=4")).Should().Be(1); // Deleted
    }

    [Fact]
    public async Task DeleteSub_GlobalSubCategory_Returns404()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        (await client.DeleteAsync("/api/v1/categories/sub/1")).StatusCode.Should().Be(HttpStatusCode.NotFound); // IdSubCategory=1 global/default
    }

    private record SubCategoryResponse(int IdSubCategory, int IdMainCategory, string MainCategory, string Name);
```
> `IdStatus=4` = `EntityStatus.Deleted` (soft-delete). Confirma el valor del enum en el proyecto.

Run: `dotnet test tests/BigSchool.Integration.Tests --filter SubCategoriesCrud` → PASS.

- [x] **Step 7: Verde + Commit**

Run: FULL. Luego:
```bash
git add -A && git commit -m "feat(finanzas): subcategorías CRUD (POST/DELETE /categories/sub) con unicidad y guarda de borrado en dominio

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 3: #6 by-category + #7 monthly + validación de rango compartida

**Files:**
- Create: `src/BigSchool.Application/SharedKernel/Common/DateRange.cs`
- Create: `src/BigSchool.Application/Finanzas/Queries/Transactions/GetByCategory/{GetTransactionsByCategoryQuery,GetTransactionsByCategoryQueryHandler,GetTransactionsByCategoryQueryValidator,CategoryTotalDto}.cs`
- Create: `src/BigSchool.Application/Finanzas/Queries/Transactions/GetMonthly/{GetMonthlyQuery,GetMonthlyQueryHandler,GetMonthlyQueryValidator}.cs`
- Modify: `src/BigSchool.WebApi/Controllers/Finanzas/TransactionsController.cs` (2 endpoints)
- Test: `tests/BigSchool.Application.Tests/Validators/Finanzas/DateRangeValidationTests.cs`
- Test: `tests/BigSchool.Integration.Tests/Transactions/{GetByCategoryTests,GetMonthlyTests}.cs` (mismo namespace/carpeta que `SubCategoriesCrudTests`)

- [ ] **Step 1: Predicado de rango compartido**

`DateRange.cs`:
```csharp
namespace BigSchool.Application.SharedKernel.Common;

public static class DateRange
{
    /// <summary>Válido si falta uno de los dos; si vienen ambos, from ≤ to y span ≤ 4 años.</summary>
    public static bool IsValid(DateOnly? from, DateOnly? to)
        => from is null || to is null || (from.Value <= to.Value && to.Value <= from.Value.AddYears(4));
}
```

- [ ] **Step 2: #6 — by-category (DTO + query + handler + validator)**

`CategoryTotalDto.cs`:
```csharp
namespace BigSchool.Application.Finanzas.Queries.Transactions.GetByCategory;

public record CategoryTotalDto(int IdMainCategory, string MainCategory, decimal Total);
```
`GetTransactionsByCategoryQuery.cs`:
```csharp
using BigSchool.Domain.Finanzas.Enums;
using MediatR;

namespace BigSchool.Application.Finanzas.Queries.Transactions.GetByCategory;

public record GetTransactionsByCategoryQuery(int IdUser, TransactionType? Type, DateOnly? From, DateOnly? To)
    : IRequest<IReadOnlyList<CategoryTotalDto>>;
```
`GetTransactionsByCategoryQueryValidator.cs`:
```csharp
using BigSchool.Application.SharedKernel.Common;
using FluentValidation;

namespace BigSchool.Application.Finanzas.Queries.Transactions.GetByCategory;

public class GetTransactionsByCategoryQueryValidator : AbstractValidator<GetTransactionsByCategoryQuery>
{
    public GetTransactionsByCategoryQueryValidator()
    {
        RuleFor(x => x).Must(q => DateRange.IsValid(q.From, q.To))
            .WithName("dateRange")
            .WithMessage("El rango de fechas es inválido (from ≤ to y máximo 4 años).");
    }
}
```
`GetTransactionsByCategoryQueryHandler.cs`:
```csharp
using BigSchool.Application.SharedKernel.Interfaces;
using BigSchool.Domain.Finanzas.Enums;
using BigSchool.Domain.SharedKernel.Enums;
using Dapper;
using MediatR;

namespace BigSchool.Application.Finanzas.Queries.Transactions.GetByCategory;

public class GetTransactionsByCategoryQueryHandler
    : IRequestHandler<GetTransactionsByCategoryQuery, IReadOnlyList<CategoryTotalDto>>
{
    private readonly IDbConnectionFactory _dbFactory;

    public GetTransactionsByCategoryQueryHandler(IDbConnectionFactory dbFactory) => _dbFactory = dbFactory;

    private const string GETBYCATEGORY_QUERY = @"SELECT IdMainCategory, SUM(BaseAmount) AS Total
            FROM Transactions
            WHERE IdUser = @IdUser AND IdStatus <> @StatusDeleted
              AND (@Type IS NULL OR Type = @Type)
              AND (@From IS NULL OR TransactionDate >= @From)
              AND (@To   IS NULL OR TransactionDate <= @To)
            GROUP BY IdMainCategory
            ORDER BY Total DESC;";

    private sealed record Row(int IdMainCategory, decimal Total);

    public async Task<IReadOnlyList<CategoryTotalDto>> Handle(GetTransactionsByCategoryQuery request, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@IdUser", request.IdUser);
        parameters.Add("@StatusDeleted", EntityStatus.Deleted);
        parameters.Add("@Type", request.Type.HasValue ? (short?)request.Type.Value : null);
        parameters.Add("@From", request.From);
        parameters.Add("@To", request.To);

        using var conn = _dbFactory.CreateConnection();
        var rows = await conn.QueryAsync<Row>(GETBYCATEGORY_QUERY, parameters);
        return rows.Select(r => new CategoryTotalDto(r.IdMainCategory, ((MainCategory)r.IdMainCategory).ToString(), r.Total)).ToList();
    }
}
```

- [ ] **Step 3: #7 — monthly (query + handler + validator; reutiliza `MonthlyChartPointDto`)**

`GetMonthlyQuery.cs`:
```csharp
using BigSchool.Application.Finanzas.Queries.Transactions.GetMonthlyChart;
using BigSchool.Domain.Finanzas.Enums;
using MediatR;

namespace BigSchool.Application.Finanzas.Queries.Transactions.GetMonthly;

public record GetMonthlyQuery(int IdUser, DateOnly? From, DateOnly? To, MainCategory? Category, TransactionType? Type)
    : IRequest<IReadOnlyList<MonthlyChartPointDto>>;
```
`GetMonthlyQueryValidator.cs`:
```csharp
using BigSchool.Application.SharedKernel.Common;
using FluentValidation;

namespace BigSchool.Application.Finanzas.Queries.Transactions.GetMonthly;

public class GetMonthlyQueryValidator : AbstractValidator<GetMonthlyQuery>
{
    public GetMonthlyQueryValidator()
    {
        RuleFor(x => x).Must(q => DateRange.IsValid(q.From, q.To))
            .WithName("dateRange")
            .WithMessage("El rango de fechas es inválido (from ≤ to y máximo 4 años).");
    }
}
```
`GetMonthlyQueryHandler.cs`:
```csharp
using BigSchool.Application.Finanzas.Queries.Transactions.GetMonthlyChart;
using BigSchool.Application.SharedKernel.Interfaces;
using BigSchool.Domain.Finanzas.Enums;
using BigSchool.Domain.SharedKernel.Enums;
using Dapper;
using MediatR;

namespace BigSchool.Application.Finanzas.Queries.Transactions.GetMonthly;

public class GetMonthlyQueryHandler : IRequestHandler<GetMonthlyQuery, IReadOnlyList<MonthlyChartPointDto>>
{
    private readonly IDbConnectionFactory _dbFactory;

    public GetMonthlyQueryHandler(IDbConnectionFactory dbFactory) => _dbFactory = dbFactory;

    private const string GETMONTHLY_QUERY = @"SELECT
                YEAR(TransactionDate)  AS Year,
                MONTH(TransactionDate) AS Month,
                COALESCE(SUM(CASE WHEN Type = @Income  THEN BaseAmount ELSE 0 END), 0) AS Income,
                COALESCE(SUM(CASE WHEN Type = @Expense THEN BaseAmount ELSE 0 END), 0) AS Expense
            FROM Transactions
            WHERE IdUser = @IdUser AND IdStatus <> @StatusDeleted
              AND (@From IS NULL OR TransactionDate >= @From)
              AND (@To   IS NULL OR TransactionDate <= @To)
              AND (@Category IS NULL OR IdMainCategory = @Category)
              AND (@Type IS NULL OR Type = @Type)
            GROUP BY YEAR(TransactionDate), MONTH(TransactionDate)
            ORDER BY Year, Month;";

    public async Task<IReadOnlyList<MonthlyChartPointDto>> Handle(GetMonthlyQuery request, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@IdUser", request.IdUser);
        parameters.Add("@StatusDeleted", EntityStatus.Deleted);
        parameters.Add("@From", request.From);
        parameters.Add("@To", request.To);
        parameters.Add("@Category", request.Category.HasValue ? (int?)request.Category.Value : null);
        parameters.Add("@Type", request.Type.HasValue ? (short?)request.Type.Value : null);
        parameters.Add("@Income", (short)TransactionType.Income);
        parameters.Add("@Expense", (short)TransactionType.Expense);

        using var conn = _dbFactory.CreateConnection();
        var rows = await conn.QueryAsync<MonthlyChartPointDto>(GETMONTHLY_QUERY, parameters);
        return rows.ToList();
    }
}
```

- [ ] **Step 4: Endpoints en `TransactionsController`**

Añade (usings de los dos handlers + DTO):
```csharp
    [HttpGet("by-category")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CategoryTotalDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ByCategory([FromQuery] TransactionType? type, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var result = await _mediator.Send(new GetTransactionsByCategoryQuery(UserId, type, from, to));
        return Ok(ApiResponse<IReadOnlyList<CategoryTotalDto>>.Success(result));
    }

    [HttpGet("monthly")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<MonthlyChartPointDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Monthly([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] MainCategory? category, [FromQuery] TransactionType? type)
    {
        var result = await _mediator.Send(new GetMonthlyQuery(UserId, from, to, category, type));
        return Ok(ApiResponse<IReadOnlyList<MonthlyChartPointDto>>.Success(result));
    }
```
> `monthly-chart?year=` (dashboard) **no se toca**. `UserId` ya es la propiedad privada del controller.

- [ ] **Step 5: Unit tests de rango**

`DateRangeValidationTests.cs`:
```csharp
using BigSchool.Application.SharedKernel.Common;
using FluentAssertions;
using Xunit;

namespace BigSchool.Application.Tests.Validators.Finanzas;

public class DateRangeValidationTests
{
    [Fact] public void IsValid_OneDateMissing_ReturnsTrue() => DateRange.IsValid(new DateOnly(2026,1,1), null).Should().BeTrue();
    [Fact] public void IsValid_FromAfterTo_ReturnsFalse() => DateRange.IsValid(new DateOnly(2026,6,1), new DateOnly(2026,1,1)).Should().BeFalse();
    [Fact] public void IsValid_SpanExactly4Years_ReturnsTrue() => DateRange.IsValid(new DateOnly(2022,1,1), new DateOnly(2026,1,1)).Should().BeTrue();
    [Fact] public void IsValid_SpanMoreThan4Years_ReturnsFalse() => DateRange.IsValid(new DateOnly(2022,1,1), new DateOnly(2026,1,2)).Should().BeFalse();
}
```

- [ ] **Step 6: E2E**

`GetByCategoryTests.cs` y `GetMonthlyTests.cs` (mirror `TransactionEndpointTestBase`): siembra transacciones multi-categoría, verifica totales sobre `BaseAmount`, filtros `type/from/to(/category)`, y **rango > 4 años → 400**. Ejemplos clave:
```csharp
    [Fact]
    public async Task ByCategory_RangeMoreThan4Years_Returns400()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        var resp = await client.GetAsync("/api/v1/transactions/by-category?from=2020-01-01&to=2026-01-01");
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ByCategory_AggregatesByCategory_OverBaseAmount()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        await CreateTransactionViaApiAsync(client, new { type = (int)TransactionType.Expense, idMainCategory = (int)MainCategory.EssentialExpenses, transactionDate = "2026-03-01", amount = 10m });
        await CreateTransactionViaApiAsync(client, new { type = (int)TransactionType.Expense, idMainCategory = (int)MainCategory.EssentialExpenses, transactionDate = "2026-03-02", amount = 20m });
        await CreateTransactionViaApiAsync(client, new { type = (int)TransactionType.Expense, idMainCategory = (int)MainCategory.Luxuries, transactionDate = "2026-03-03", amount = 5m });

        var env = await (await client.GetAsync("/api/v1/transactions/by-category?type=Expense"))
            .Content.ReadFromJsonAsync<ApiEnvelope<List<CategoryTotalResponse>>>();
        env!.Data!.Single(c => c.IdMainCategory == (int)MainCategory.EssentialExpenses).Total.Should().Be(30m);
    }

    private record CategoryTotalResponse(int IdMainCategory, string MainCategory, decimal Total);
```
`GetMonthlyTests`: crea transacciones en meses distintos, `GET /transactions/monthly?from&to` agrupa por año-mes (Income/Expense split); `category`/`type` filtran; rango > 4 años → 400. Verifica también que `GET /transactions/monthly-chart?year=` sigue verde (no tocado).

Run: `dotnet test tests/BigSchool.Integration.Tests --filter "GetByCategory|GetMonthly"` → PASS.

- [ ] **Step 7: Verde + Commit**

Run: FULL. Luego:
```bash
git add -A && git commit -m "feat(finanzas): GET /transactions/by-category y /monthly + validación de rango (≤4 años)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Verificación final (DoD — spec 009 §10)

- [ ] **Unit Domain**: `SubCategory.Create` (valida, fija `IdUser`, `IsGlobal`) + `Delete(requestingUserId)` (soft-borra solo la propia; rechaza global/predefinida/ajena); tests de `User.AddSubCategory` eliminados; `SubCategory` testeada como AR directo.
- [ ] **Unit Application**: validators create/delete; **rango 4 años** (#6 y #7); handler create (409 vs usuario y globales); handler delete (404 inexistente/global/ajena/default + invariante de dominio).
- [ ] **Arquitectura**: `ModuleBoundaryTests` con `Domain.Auth` prohibiendo `Finanzas` **en verde** (frontera cerrada) — prueba canónica del re-modelado.
- [ ] **Migración**: `RemodelSubCategoryAggregate` solo suelta la FK; `has-pending-model-changes` sin sorpresas.
- [ ] **E2E**: `POST /categories/sub` crea (409 duplicada usuario/global), `GET /categories` la incluye; `DELETE` soft-borra la propia, global/ajena → 404; `by-category` totales sobre `BaseAmount` + filtros + rango>4a→400; `monthly` agrupa año-mes + filtros + rango>4a→400; `monthly-chart`/`summary`/`categories` siguen verdes.
- [ ] **FULL** + ARCH verde.

## Self-Review (cobertura de la spec 009)

| Sección | Tarea(s) |
|---|---|
| §4 Re-modelado SubCategory (entidad, User, configs, seed, migración, guard) | 1 |
| §5 #8/C CRUD subcategorías (invariante de borrado en dominio) | 1, 2 |
| §6 #6 by-category | 3 |
| §7 #7 monthly | 3 |
| §7.1 Regla de rango compartida (`DateRange`) | 3 |
| §10 Verificación | "Verificación final" |
