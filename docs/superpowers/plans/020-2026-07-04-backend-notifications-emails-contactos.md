# Backend — Notifications (EmailLog + Contactos + Welcome) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Crear el módulo **Notifications** (backend puro): entidades `Contact` + `EmailLog`, `POST /contacts` (público) que genera un `EmailLog` de acuse **atómicamente** vía DomainEvent, y `Register` (Auth) que genera un `EmailLog` de bienvenida **post-commit** vía IntegrationEvent + Outbox. Estrena los dos patrones de comunicación de la Spec 0.

**Architecture:** Módulo Notifications con dos AR (`Contact`, `EmailLog`) que depende **solo** de SharedKernel. Dos flujos: (A) **intra-módulo atómico** — `Contact.Create` levanta `ContactSubmittedDomainEvent`; un `INotificationHandler` dispara un command que crea el `EmailLog` de acuse en el mismo `DbContext`, persistido en la misma transacción gracias a la **UoW transaccional componible**. (B) **inter-módulo post-commit** — `User.Create` levanta `UserRegisteredDomainEvent`; un handler de Auth lo traduce a `UserRegisteredIntegrationEvent` y lo **encola en el Outbox** (atómico con el `User`); el `OutboxDispatchBehavior` lo drena post-commit y el bus invoca el handler de Notifications que dispara el command del `EmailLog` de bienvenida. Auth **no** referencia Notifications (contrato compartido en SharedKernel). Lecturas Dapper paginadas.

**Norma de EventHandlers (decisión de arquitectura):** los EventHandlers (domain e integration) **no crean flujo nuevo**: son **disparadores finos** → `_mediator.Send(Command)`. El **Command** contiene los pasos (`entity.Add/Update/Delete` + `await _repo.SaveChangesAsync()`). El implementador del command **no** necesita saber si corre dentro de una transacción externa: la UoW componible (`CurrentTransaction is null`) hace que un `SaveChangesAsync()` anidado participe en la transacción en curso sin abrir una segunda. **Única excepción, documentada en código:** el handler de Auth que encola el IntegrationEvent (`_outbox.Add()` sin guardar; lo persiste el save-3 de la UoW).

**Tech Stack:** .NET 8, C#, DDD + CQRS (MediatR), EF Core + MySQL 8, Dapper, Autofac (DI modular), FluentValidation, xUnit + FluentAssertions + Moq + `WebApplicationFactory`.

---

## Convenciones y contexto (LEER ANTES DE EMPEZAR)

> **BASE:** develop ya contiene la estructura modular (plan 018) y la paginación (plan 019). Namespace = ruta de carpeta. Este plan se ejecuta sobre `feature/007-010-backend-features` (rama de trabajo); cada tarea es una rama `feature/020-notifications-taskN` + PR con checkpoint humano.

### Piezas existentes que se reutilizan (verificadas en develop)

- `BaseEntity.RaiseDomainEvent(IDomainEvent)` / `DomainEvents` / `ClearDomainEvents()` — `BigSchool.Domain.SharedKernel.Entities`.
- `IDomainEvent` — `BigSchool.Domain.SharedKernel.Events`. `DomainEventNotification<T>` (`INotification`, propiedad `DomainEvent`) — `BigSchool.Application.SharedKernel.Events`.
- `IIntegrationEvent { Guid EventId; DateTime OccurredOn; }`, `IIntegrationEventHandler<in T>.HandleAsync(T, ct)`, `IIntegrationEventOutbox.Add(IIntegrationEvent)`, `IOutboxDispatcher`, `OutboxDispatchBehavior` (drena post-commit, ya registrado en `Program.cs`) — `BigSchool.Application.SharedKernel.IntegrationEvents` / `.Behaviors`.
- `InMemoryIntegrationEventBus` (resuelve `IIntegrationEventHandler<T>` por `IServiceProvider.GetServices` → **1:N**), `OutboxDispatcher`, `OutboxMessage` — `BigSchool.Infrastructure.SharedKernel.IntegrationEvents`.
- `IRepository<T, Y> { IUnitOfWork UnitOfWork; GetByIdAsync; AddAsync; AddRangeAsync }` — `BigSchool.Application.SharedKernel.Interfaces`. Base `EFRepository<T,Y>` — `BigSchool.Infrastructure.SharedKernel.Persistence.Repositories`.
- `IUnitOfWork.SaveChangesAsync(bool dispatchEvents = true)` — `BigSchool.Domain.SharedKernel.Interfaces`.
- `Pagination` (`NormalizePage/NormalizePageSize`, DEFAULT 20 / MAX 100), `PagedResult<T>`, `ApiResponse`, `MetaData`, `ApiError` — `BigSchool.Application.SharedKernel.Common`.
- `IDbConnectionFactory` — `BigSchool.Application.SharedKernel.Interfaces`. `EntityStatus` (`Active`, `Deleted`) — `BigSchool.Domain.SharedKernel.Enums`.
- Patrón controller `[Authorize]` con userId: `CurrentUser.GetId(User, _encryptor)` + `IUserIdEncryptor` (ver `TransactionsController`); DTO Dapper con enums string; `ApiEnvelope<T>`/`MetaPayload` en `IntegrationTestBase`.

### ⚠ Riesgo de DI conocido (lo resuelve la Tarea 5) — léelo ya

Los módulos Autofac escanean `BigSchool.Application.{Módulo}` con `.AsImplementedInterfaces()`. **MediatR** ya registra todos los handlers del ensamblado Application vía `RegisterServicesFromAssembly`. Para `IRequestHandler` el doble registro es inocuo (se resuelve 1). **Pero** para `INotificationHandler` (domain events), `_mediator.Publish` usa `GetServices` → **invocaría el handler 2 veces** (dispararía el command 2 veces → 2 EmailLogs / 2 filas de outbox). Notifications introduce los **primeros** `INotificationHandler` reales → hay que **excluir los `INotificationHandler` del escaneo Autofac** (los posee MediatR) en `NotificationsModule` **y** en `AuthModule`. Los `IIntegrationEventHandler` **no** son de MediatR: se registran por el escaneo (una vez) y el bus los resuelve por `GetServices`. Los tests de "exactamente 1 EmailLog" cazan una regresión.

### Recetas de verificación

- **BUILD**: cwd `src/backend` → `dotnet build` → `Build succeeded. 0 Error(s)`.
- **UNIT**: `dotnet test tests/BigSchool.Domain.Tests` y `dotnet test tests/BigSchool.Application.Tests`.
- **ARCH**: `dotnet test tests/BigSchool.Architecture.Tests`.
- **INTEGRATION** (MySQL de `infra/docker-compose.yml`, `localhost:3306`; `BIGSCHOOL_TEST_MYSQL` si difieren credenciales): `dotnet test tests/BigSchool.Integration.Tests`.
- **FULL**: BUILD + UNIT + ARCH + INTEGRATION.

---

## Task 1: Dominio — Contact, EmailLog, EmailType y eventos

Entidades y eventos del módulo, más el hecho de dominio `UserRegisteredDomainEvent` en Auth. Solo dominio (POCO) + unit tests.

**Files:**
- Create: `src/BigSchool.Domain/Notifications/Enums/EmailType.cs`
- Create: `src/BigSchool.Domain/Notifications/Entities/Contact.cs`, `EmailLog.cs`
- Create: `src/BigSchool.Domain/Notifications/Events/ContactSubmittedDomainEvent.cs`
- Create: `src/BigSchool.Domain/Auth/Events/UserRegisteredDomainEvent.cs`
- Modify: `src/BigSchool.Domain/Auth/Entities/User.cs` (`Create` levanta el evento)
- Test: `tests/BigSchool.Domain.Tests/Entities/Notifications/{ContactTests,EmailLogTests}.cs`, `tests/BigSchool.Domain.Tests/Entities/Auth/UserRegistrationEventTests.cs`

- [x] **Step 1: Escribir los tests de dominio (fallan)**

`ContactTests.cs`:
```csharp
using BigSchool.Domain.Notifications.Entities;
using BigSchool.Domain.Notifications.Events;
using FluentAssertions;
using Xunit;

namespace BigSchool.Domain.Tests.Entities.Notifications;

public class ContactTests
{
    [Fact]
    public void Create_valido_asigna_campos_y_levanta_ContactSubmitted()
    {
        var c = Contact.Create("Ada Lovelace", "ADA@Example.com", " Hola ");
        c.FullName.Should().Be("Ada Lovelace");
        c.Email.Should().Be("ada@example.com");
        c.Message.Should().Be("Hola");
        c.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ContactSubmittedDomainEvent>();
    }

    [Theory]
    [InlineData("", "a@b.com", "m")]
    [InlineData("Ada", "sin-arroba", "m")]
    [InlineData("Ada", "a@b.com", "")]
    public void Create_invalido_lanza(string name, string email, string msg)
        => FluentActions.Invoking(() => Contact.Create(name, email, msg)).Should().Throw<ArgumentException>();
}
```

`EmailLogTests.cs`:
```csharp
using BigSchool.Domain.Notifications.Entities;
using BigSchool.Domain.Notifications.Enums;
using FluentAssertions;
using Xunit;

namespace BigSchool.Domain.Tests.Entities.Notifications;

public class EmailLogTests
{
    [Fact]
    public void CreateWelcome_fija_Type_IdUser_y_recipient()
    {
        var e = EmailLog.CreateWelcome(42, "ada@example.com", "Ada");
        e.Type.Should().Be(EmailType.Welcome);
        e.IdUser.Should().Be(42);
        e.Recipient.Should().Be("ada@example.com");
        e.Subject.Should().NotBeNullOrWhiteSpace();
        e.Body.Should().Contain("Ada");
    }

    [Fact]
    public void CreateContactAck_fija_Type_Contact_y_IdUser_null()
    {
        var e = EmailLog.CreateContactAck("ada@example.com", "Ada");
        e.Type.Should().Be(EmailType.Contact);
        e.IdUser.Should().BeNull();
        e.Recipient.Should().Be("ada@example.com");
    }
}
```

`UserRegistrationEventTests.cs`:
```csharp
using BigSchool.Domain.Auth.Entities;
using BigSchool.Domain.Auth.Events;
using FluentAssertions;
using Xunit;

namespace BigSchool.Domain.Tests.Entities.Auth;

public class UserRegistrationEventTests
{
    [Fact]
    public void Create_levanta_UserRegisteredDomainEvent_con_la_entidad()
    {
        var u = User.Create("ada@example.com", "hash", "salt", "Ada");
        u.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<UserRegisteredDomainEvent>();
        ((UserRegisteredDomainEvent)u.DomainEvents.Single()).User.Should().BeSameAs(u);
    }
}
```

Run: `dotnet test tests/BigSchool.Domain.Tests --filter "Notifications|UserRegistration"` → *Expected:* FAIL (tipos no existen).

- [x] **Step 2: `EmailType`**

`EmailType.cs`:
```csharp
namespace BigSchool.Domain.Notifications.Enums;

public enum EmailType : short
{
    Welcome = 1,
    Contact = 2
}
```

- [x] **Step 3: `ContactSubmittedDomainEvent` y `UserRegisteredDomainEvent`**

`ContactSubmittedDomainEvent.cs`:
```csharp
using BigSchool.Domain.Notifications.Entities;
using BigSchool.Domain.SharedKernel.Events;

namespace BigSchool.Domain.Notifications.Events;

public sealed record ContactSubmittedDomainEvent(Contact Contact) : IDomainEvent;
```

`UserRegisteredDomainEvent.cs`:
```csharp
using BigSchool.Domain.Auth.Entities;
using BigSchool.Domain.SharedKernel.Events;

namespace BigSchool.Domain.Auth.Events;

public sealed record UserRegisteredDomainEvent(User User) : IDomainEvent;
```

- [x] **Step 4: `Contact` y `EmailLog`**

`Contact.cs`:
```csharp
using BigSchool.Domain.Notifications.Events;
using BigSchool.Domain.SharedKernel.Entities;
using BigSchool.Domain.SharedKernel.Enums;

namespace BigSchool.Domain.Notifications.Entities;

public class Contact : BaseEntity, IAggregateRoot
{
    public int IdContact { get; private set; }
    public string FullName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public EntityStatus IdStatus { get; private set; }
    public DateTime CreatedAt { get; private set; }

    protected Contact() { } // EF Core

    private Contact(string fullName, string email, string message, EntityStatus idStatus, DateTime createdAt)
    {
        FullName = fullName;
        Email = email;
        Message = message;
        IdStatus = idStatus;
        CreatedAt = createdAt;
    }

    public static Contact Create(string fullName, string email, string message)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full name is required.", nameof(fullName));
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new ArgumentException("Valid email is required.", nameof(email));
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message is required.", nameof(message));

        var contact = new Contact(
            fullName.Trim(), email.Trim().ToLowerInvariant(), message.Trim(),
            EntityStatus.Active, DateTime.UtcNow);
        contact.RaiseDomainEvent(new ContactSubmittedDomainEvent(contact));
        return contact;
    }
}
```

`EmailLog.cs`:
```csharp
using BigSchool.Domain.Notifications.Enums;
using BigSchool.Domain.SharedKernel.Entities;
using BigSchool.Domain.SharedKernel.Enums;

namespace BigSchool.Domain.Notifications.Entities;

public class EmailLog : BaseEntity, IAggregateRoot
{
    public int IdEmailLog { get; private set; }
    public int? IdUser { get; private set; }
    public string Recipient { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public EmailType Type { get; private set; }
    public DateTime SentAt { get; private set; }
    public EntityStatus IdStatus { get; private set; }
    public DateTime CreatedAt { get; private set; }

    protected EmailLog() { } // EF Core

    private EmailLog(int? idUser, string recipient, string subject, string body,
        EmailType type, DateTime sentAt, EntityStatus idStatus, DateTime createdAt)
    {
        IdUser = idUser;
        Recipient = recipient;
        Subject = subject;
        Body = body;
        Type = type;
        SentAt = sentAt;
        IdStatus = idStatus;
        CreatedAt = createdAt;
    }

    public static EmailLog CreateWelcome(int idUser, string recipient, string fullName)
    {
        var now = DateTime.UtcNow;
        return new EmailLog(idUser, recipient,
            "¡Bienvenido a BigSchool!",
            $"Hola {fullName}, gracias por registrarte en BigSchool. Tu cuenta ya está lista.",
            EmailType.Welcome, now, EntityStatus.Active, now);
    }

    public static EmailLog CreateContactAck(string recipient, string fullName)
    {
        var now = DateTime.UtcNow;
        return new EmailLog(null, recipient,
            "Hemos recibido tu mensaje",
            $"Hola {fullName}, hemos recibido tu mensaje de contacto y te responderemos pronto.",
            EmailType.Contact, now, EntityStatus.Active, now);
    }
}
```

- [x] **Step 5: `User.Create` levanta el evento**

En `src/BigSchool.Domain/Auth/Entities/User.cs`, añade `using BigSchool.Domain.Auth.Events;` y sustituye el `return new User(...)` del método `Create` por:
```csharp
        var user = new User(
            email.Trim().ToLowerInvariant(),
            passwordHash,
            passwordSalt,
            fullName.Trim(),
            baseCurrency,
            EntityStatus.Active,
            DateTime.UtcNow);
        user.RaiseDomainEvent(new UserRegisteredDomainEvent(user));
        return user;
```

- [x] **Step 6: Verde**

Run: `dotnet test tests/BigSchool.Domain.Tests --filter "Notifications|UserRegistration"` → PASS. Luego BUILD + UNIT completos.
> Nota: los tests de Auth existentes que hacen `User.Create(...)` siguen verdes; si alguno asertaba `DomainEvents` vacío, actualízalo (ahora hay 1 evento).

- [x] **Step 7: Commit**
```bash
git add -A && git commit -m "feat(notifications): dominio Contact/EmailLog + eventos (User/Contact)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 2: UoW transaccional componible (SharedKernel)

Hoy `SaveChangesAsync` guarda el agregado y **luego** despacha domain events sin volver a guardar → lo que un handler encola sin guardar (la fila de outbox de la excepción del §5) **no se persiste**. Lo corrige: envuelve en transacción **componible**, guarda el agregado, despacha, y **re-guarda** los efectos intra-BD que hayan quedado sin persistir, todo en la **misma** transacción.

**Files:**
- Modify: `src/BigSchool.Infrastructure/SharedKernel/Persistence/BigSchoolDbContext.cs` (`SaveChangesAsync`)

- [x] **Step 1: Reescribir `SaveChangesAsync`**

Sustituye el método `SaveChangesAsync` por:
```csharp
    public async Task<int> SaveChangesAsync(bool dispatchEvents = true)
    {
        if (!dispatchEvents)
            return await base.SaveChangesAsync(CancellationToken.None);

        // Componible: si ya hay transacción en curso (SaveChanges anidado desde un handler/command),
        // la dueña es la más externa → un único COMMIT, sin BEGIN anidado (que MySQL rechaza).
        var ownsTransaction = Database.CurrentTransaction is null && Database.IsRelational();
        await using var tx = ownsTransaction ? await Database.BeginTransactionAsync() : null;

        var result = await base.SaveChangesAsync(CancellationToken.None); // 1) agregado (Id ya asignado)
        await DispatchDomainEvents();                                     // 2) handlers reaccionan (Send command / encolan outbox)

        if (ChangeTracker.HasChanges())
            await base.SaveChangesAsync(CancellationToken.None);          // 3) persiste lo que quedó sin guardar (p.ej. fila de outbox)

        if (tx is not null) await tx.CommitAsync();
        return result;
    }
```
> `CurrentTransaction is null` hace la UoW **componible**: un command disparado por un handler puede llamar `SaveChangesAsync()` con normalidad y participará en la transacción en curso (no abre una segunda). `IsRelational()` protege proveedores sin transacción. Una sola ronda de dispatch (consciente). El save-3 cubre la **excepción** del handler de Auth (encola outbox sin guardar). El resto del `DbContext` **no cambia**.

- [x] **Step 2: Verde (sin regresión)**

Run: FULL. *Expected:* verde. Ningún flujo actual deja cambios sin guardar en handlers → el save-3 no-opea; comportamiento idéntico. (La atomicidad se ejercita en las Tareas 4 y 5.)

- [x] **Step 3: Commit**
```bash
git add -A && git commit -m "feat(sharedkernel): UoW transaccional componible (efectos de handlers atómicos con el agregado)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 3: Persistencia — configs, repos, DbSets y migración

**Files:**
- Create: `src/BigSchool.Application/Notifications/Interfaces/Repositories/IContactRepository.cs`, `IEmailLogRepository.cs`
- Create: `src/BigSchool.Infrastructure/Notifications/Persistence/Configurations/ContactConfiguration.cs`, `EmailLogConfiguration.cs`
- Create: `src/BigSchool.Infrastructure/Notifications/Persistence/Repositories/ContactRepository.cs`, `EmailLogRepository.cs`
- Modify: `src/BigSchool.Infrastructure/SharedKernel/Persistence/BigSchoolDbContext.cs` (DbSets)
- Migration: `AddNotificationsModule`

- [x] **Step 1: Interfaces de repositorio**

`IContactRepository.cs`:
```csharp
using BigSchool.Application.SharedKernel.Interfaces;
using BigSchool.Domain.Notifications.Entities;

namespace BigSchool.Application.Notifications.Interfaces.Repositories;

public interface IContactRepository : IRepository<Contact, int> { }
```
`IEmailLogRepository.cs`:
```csharp
using BigSchool.Application.SharedKernel.Interfaces;
using BigSchool.Domain.Notifications.Entities;

namespace BigSchool.Application.Notifications.Interfaces.Repositories;

public interface IEmailLogRepository : IRepository<EmailLog, int> { }
```

- [x] **Step 2: Configuraciones EF**

`ContactConfiguration.cs`:
```csharp
using BigSchool.Domain.Notifications.Entities;
using BigSchool.Domain.SharedKernel.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BigSchool.Infrastructure.Notifications.Persistence.Configurations;

public class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> b)
    {
        b.ToTable("Contacts");
        b.HasKey(c => c.IdContact);
        b.Property(c => c.IdContact).ValueGeneratedOnAdd();
        b.Property(c => c.FullName).IsRequired().HasMaxLength(200);
        b.Property(c => c.Email).IsRequired().HasMaxLength(255);
        b.Property(c => c.Message).IsRequired().HasMaxLength(2000);
        b.Property(c => c.IdStatus).IsRequired().HasDefaultValue(EntityStatus.Active).HasConversion<short>();
        b.Property(c => c.CreatedAt).IsRequired();
        b.HasQueryFilter(c => c.IdStatus != EntityStatus.Deleted);
        b.Ignore(c => c.DomainEvents);
    }
}
```

`EmailLogConfiguration.cs`:
```csharp
using BigSchool.Domain.Notifications.Entities;
using BigSchool.Domain.SharedKernel.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BigSchool.Infrastructure.Notifications.Persistence.Configurations;

public class EmailLogConfiguration : IEntityTypeConfiguration<EmailLog>
{
    public void Configure(EntityTypeBuilder<EmailLog> b)
    {
        b.ToTable("EmailLogs");
        b.HasKey(e => e.IdEmailLog);
        b.Property(e => e.IdEmailLog).ValueGeneratedOnAdd();
        b.Property(e => e.IdUser); // NULL permitido; referencia suave por Id, SIN FK dura (frontera de módulo)
        b.Property(e => e.Recipient).IsRequired().HasMaxLength(255);
        b.Property(e => e.Subject).IsRequired().HasMaxLength(300);
        b.Property(e => e.Body).IsRequired().HasMaxLength(4000);
        b.Property(e => e.Type).IsRequired().HasConversion<short>();
        b.Property(e => e.SentAt).IsRequired();
        b.Property(e => e.IdStatus).IsRequired().HasDefaultValue(EntityStatus.Active).HasConversion<short>();
        b.Property(e => e.CreatedAt).IsRequired();
        b.HasIndex(e => e.IdUser);
        b.HasQueryFilter(e => e.IdStatus != EntityStatus.Deleted);
        b.Ignore(e => e.DomainEvents);
    }
}
```

- [x] **Step 3: Repositorios**

`ContactRepository.cs`:
```csharp
using BigSchool.Application.Notifications.Interfaces.Repositories;
using BigSchool.Domain.Notifications.Entities;
using BigSchool.Infrastructure.SharedKernel.Persistence;
using BigSchool.Infrastructure.SharedKernel.Persistence.Repositories;

namespace BigSchool.Infrastructure.Notifications.Persistence.Repositories;

public class ContactRepository : EFRepository<Contact, int>, IContactRepository
{
    public ContactRepository(BigSchoolDbContext context) : base(context) { }
}
```
`EmailLogRepository.cs`:
```csharp
using BigSchool.Application.Notifications.Interfaces.Repositories;
using BigSchool.Domain.Notifications.Entities;
using BigSchool.Infrastructure.SharedKernel.Persistence;
using BigSchool.Infrastructure.SharedKernel.Persistence.Repositories;

namespace BigSchool.Infrastructure.Notifications.Persistence.Repositories;

public class EmailLogRepository : EFRepository<EmailLog, int>, IEmailLogRepository
{
    public EmailLogRepository(BigSchoolDbContext context) : base(context) { }
}
```

- [x] **Step 4: DbSets en el `DbContext`**

En `BigSchoolDbContext.cs` añade `using BigSchool.Domain.Notifications.Entities;` y, junto a los demás AR:
```csharp
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<EmailLog> EmailLogs => Set<EmailLog>();
```

- [x] **Step 5: Migración**

Run (cwd `src/backend`): `dotnet ef migrations add AddNotificationsModule --project src/BigSchool.Infrastructure --startup-project src/BigSchool.WebApi`
*Expected:* migración en `SharedKernel/Persistence/Migrations/` creando `Contacts` y `EmailLogs`. Revisa el `Up()`: `EmailLogs.IdUser` nullable **sin** FK; índice en `IdUser`. (El namespace de la migración lo hereda EF; no lo cambies.)

- [x] **Step 6: Verde**

Run: BUILD (compila aunque los repos aún no estén registrados en DI; se registran en la Tarea 4). Luego BUILD + UNIT.

- [x] **Step 7: Commit**
```bash
git add -A && git commit -m "feat(notifications): persistencia (configs, repos, DbSets) + migración AddNotificationsModule

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 4: Flujo Contacto (intra-módulo, atómico) + módulo DI + POST/GET contacts

`POST /contacts` público crea `Contact` que, atómicamente (UoW componible), genera su `EmailLog` de acuse: el DomainEvent dispara un **command** que crea el EmailLog. Estrena `NotificationsModule`.

**Files:**
- Create: `src/BigSchool.Application/Notifications/Commands/CreateContact/{CreateContactCommand,CreateContactCommandHandler,CreateContactCommandValidator}.cs`
- Create: `src/BigSchool.Application/Notifications/Commands/CreateContactAckEmail/{CreateContactAckEmailCommand,CreateContactAckEmailCommandHandler}.cs`
- Create: `src/BigSchool.Application/Notifications/EventHandlers/SendContactAckEmailOnContactSubmittedHandler.cs`
- Create: `src/BigSchool.Application/Notifications/DTOs/ContactListItemDto.cs`
- Create: `src/BigSchool.Application/Notifications/Queries/GetContacts/{GetContactsQuery,GetContactsQueryHandler}.cs`
- Create: `src/BigSchool.Infrastructure/Notifications/DI/NotificationsModule.cs`
- Modify: `src/BigSchool.WebApi/Program.cs` (registrar `NotificationsModule`)
- Create: `src/BigSchool.WebApi/Controllers/Notifications/ContactsController.cs`
- Test: `tests/BigSchool.Application.Tests/Validators/Notifications/CreateContactCommandValidatorTests.cs`, `tests/BigSchool.Application.Tests/Commands/Notifications/CreateContactAckEmailCommandHandlerTests.cs`, `tests/BigSchool.Application.Tests/EventHandlers/Notifications/SendContactAckEmailOnContactSubmittedHandlerTests.cs`
- Test: `tests/BigSchool.Integration.Tests/Notifications/{NotificationEndpointTestBase,PostContactTests,GetContactsTests}.cs`

- [x] **Step 1: Command público de alta de contacto**

`CreateContactCommand.cs`:
```csharp
using MediatR;

namespace BigSchool.Application.Notifications.Commands.CreateContact;

public record CreateContactCommand(string FullName, string Email, string Message) : IRequest<int>;
```
`CreateContactCommandValidator.cs`:
```csharp
using FluentValidation;

namespace BigSchool.Application.Notifications.Commands.CreateContact;

public class CreateContactCommandValidator : AbstractValidator<CreateContactCommand>
{
    public CreateContactCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.Message).NotEmpty().MaximumLength(2000);
    }
}
```
`CreateContactCommandHandler.cs`:
```csharp
using BigSchool.Application.Notifications.Interfaces.Repositories;
using BigSchool.Domain.Notifications.Entities;
using MediatR;

namespace BigSchool.Application.Notifications.Commands.CreateContact;

public class CreateContactCommandHandler : IRequestHandler<CreateContactCommand, int>
{
    private readonly IContactRepository _contacts;

    public CreateContactCommandHandler(IContactRepository contacts) => _contacts = contacts;

    public async Task<int> Handle(CreateContactCommand request, CancellationToken cancellationToken)
    {
        var contact = Contact.Create(request.FullName, request.Email, request.Message);
        await _contacts.AddAsync(contact, cancellationToken);
        await _contacts.UnitOfWork.SaveChangesAsync(); // dispara ContactSubmitted → command del acuse; UoW componible = atómico
        return contact.IdContact;
    }
}
```

- [x] **Step 2: Command interno del EmailLog de acuse (la acción)**

`CreateContactAckEmailCommand.cs`:
```csharp
using MediatR;

namespace BigSchool.Application.Notifications.Commands.CreateContactAckEmail;

public record CreateContactAckEmailCommand(string Recipient, string FullName) : IRequest;
```
`CreateContactAckEmailCommandHandler.cs`:
```csharp
using BigSchool.Application.Notifications.Interfaces.Repositories;
using BigSchool.Domain.Notifications.Entities;
using MediatR;

namespace BigSchool.Application.Notifications.Commands.CreateContactAckEmail;

public class CreateContactAckEmailCommandHandler : IRequestHandler<CreateContactAckEmailCommand>
{
    private readonly IEmailLogRepository _emailLogs;

    public CreateContactAckEmailCommandHandler(IEmailLogRepository emailLogs) => _emailLogs = emailLogs;

    public async Task Handle(CreateContactAckEmailCommand request, CancellationToken cancellationToken)
    {
        await _emailLogs.AddAsync(EmailLog.CreateContactAck(request.Recipient, request.FullName), cancellationToken);
        await _emailLogs.UnitOfWork.SaveChangesAsync(); // UoW componible: participa en la transacción del Contact (atómico)
    }
}
```

- [x] **Step 3: EventHandler = disparador fino (norma)**

`SendContactAckEmailOnContactSubmittedHandler.cs`:
```csharp
using BigSchool.Application.Notifications.Commands.CreateContactAckEmail;
using BigSchool.Application.SharedKernel.Events;
using BigSchool.Domain.Notifications.Events;
using MediatR;

namespace BigSchool.Application.Notifications.EventHandlers;

/// <summary>Disparador: traduce el hecho de dominio en el command que realiza la acción. No manipula estado.</summary>
public sealed class SendContactAckEmailOnContactSubmittedHandler
    : INotificationHandler<DomainEventNotification<ContactSubmittedDomainEvent>>
{
    private readonly IMediator _mediator;

    public SendContactAckEmailOnContactSubmittedHandler(IMediator mediator) => _mediator = mediator;

    public async Task Handle(DomainEventNotification<ContactSubmittedDomainEvent> notification, CancellationToken cancellationToken)
    {
        var c = notification.DomainEvent.Contact;
        await _mediator.Send(new CreateContactAckEmailCommand(c.Email, c.FullName), cancellationToken);
    }
}
```

- [x] **Step 4: DTO + Query de listado (Dapper, paginada)**

`ContactListItemDto.cs`:
```csharp
namespace BigSchool.Application.Notifications.DTOs;

public record ContactListItemDto(int IdContact, string FullName, string Email, string Message, DateTime CreatedAt);
```
`GetContactsQuery.cs`:
```csharp
using BigSchool.Application.Notifications.DTOs;
using BigSchool.Application.SharedKernel.Common;
using MediatR;

namespace BigSchool.Application.Notifications.Queries.GetContacts;

public record GetContactsQuery(int Page, int PageSize) : IRequest<PagedResult<ContactListItemDto>>;
```
`GetContactsQueryHandler.cs`:
```csharp
using BigSchool.Application.Notifications.DTOs;
using BigSchool.Application.SharedKernel.Common;
using BigSchool.Application.SharedKernel.Interfaces;
using BigSchool.Domain.SharedKernel.Enums;
using Dapper;
using MediatR;

namespace BigSchool.Application.Notifications.Queries.GetContacts;

public class GetContactsQueryHandler : IRequestHandler<GetContactsQuery, PagedResult<ContactListItemDto>>
{
    private readonly IDbConnectionFactory _dbFactory;

    public GetContactsQueryHandler(IDbConnectionFactory dbFactory) => _dbFactory = dbFactory;

    private const string GETCONTACTS_QUERY = @"SELECT COUNT(*) FROM Contacts WHERE IdStatus <> @StatusDeleted;
            SELECT IdContact, FullName, Email, Message, CreatedAt
            FROM Contacts WHERE IdStatus <> @StatusDeleted
            ORDER BY CreatedAt DESC, IdContact DESC
            LIMIT @PageSize OFFSET @Offset;";

    public async Task<PagedResult<ContactListItemDto>> Handle(GetContactsQuery request, CancellationToken cancellationToken)
    {
        var page = Pagination.NormalizePage(request.Page);
        var pageSize = Pagination.NormalizePageSize(request.PageSize);

        var parameters = new DynamicParameters();
        parameters.Add("@StatusDeleted", EntityStatus.Deleted);
        parameters.Add("@PageSize", pageSize);
        parameters.Add("@Offset", (page - 1) * pageSize);

        using var conn = _dbFactory.CreateConnection();
        using var multi = await conn.QueryMultipleAsync(GETCONTACTS_QUERY, parameters);
        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<ContactListItemDto>()).ToList();
        return new PagedResult<ContactListItemDto>(items, page, pageSize, total);
    }
}
```

- [x] **Step 5: `NotificationsModule` (repos + escaneo con exclusión de INotificationHandler)**

`NotificationsModule.cs`:
```csharp
using System.Linq;
using Autofac;
using BigSchool.Application.Notifications.Commands.CreateContact;
using BigSchool.Infrastructure.Notifications.Persistence.Repositories;
using MediatR;
using Module = Autofac.Module;

namespace BigSchool.Infrastructure.Notifications.DI;

public sealed class NotificationsModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        // Application.Notifications: validators, IRequestHandler, IIntegrationEventHandler…
        // EXCLUYE los INotificationHandler (domain events): los posee MediatR; registrarlos aquí
        // los dispararía 2 veces en Publish (GetServices) → doble command.
        builder.RegisterAssemblyTypes(typeof(CreateContactCommand).Assembly)
            .Where(t => t.Namespace is not null
                        && t.Namespace.StartsWith("BigSchool.Application.Notifications")
                        && !IsMediatrNotificationHandler(t))
            .AsImplementedInterfaces();

        // Infrastructure.Notifications: repositorios.
        builder.RegisterAssemblyTypes(typeof(ContactRepository).Assembly)
            .Where(t => t.Namespace is not null && t.Namespace.StartsWith("BigSchool.Infrastructure.Notifications"))
            .AsImplementedInterfaces();
    }

    internal static bool IsMediatrNotificationHandler(Type t) =>
        t.GetInterfaces().Any(i => i.IsGenericType
            && i.GetGenericTypeDefinition() == typeof(INotificationHandler<>));
}
```
Regístralo en `Program.cs` junto a los otros módulos:
```csharp
        containerBuilder.RegisterModule<BigSchool.Infrastructure.Notifications.DI.NotificationsModule>();
```

- [x] **Step 6: `ContactsController` (POST público, GET privado)**

`ContactsController.cs`:
```csharp
using BigSchool.Application.Notifications.Commands.CreateContact;
using BigSchool.Application.Notifications.DTOs;
using BigSchool.Application.Notifications.Queries.GetContacts;
using BigSchool.Application.SharedKernel.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BigSchool.WebApi.Controllers.Notifications;

[ApiController]
[Route("api/v1/contacts")]
public class ContactsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ContactsController(IMediator mediator) => _mediator = mediator;

    public record CreateContactRequest(string FullName, string Email, string Message);

    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateContactRequest body)
    {
        var id = await _mediator.Send(new CreateContactCommand(body.FullName, body.Email, body.Message));
        return Ok(ApiResponse<object>.Success(new { idContact = id }));
    }

    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ContactListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetContactsQuery(page, pageSize));
        var meta = new MetaData { Page = result.Page, PageSize = result.PageSize, TotalCount = result.TotalCount };
        return Ok(ApiResponse<IReadOnlyList<ContactListItemDto>>.Success(result.Items, meta));
    }
}
```

- [x] **Step 7: Unit tests (validator + command de acuse + disparador)**

`CreateContactCommandValidatorTests.cs`:
```csharp
using BigSchool.Application.Notifications.Commands.CreateContact;
using FluentAssertions;
using Xunit;

namespace BigSchool.Application.Tests.Validators.Notifications;

public class CreateContactCommandValidatorTests
{
    private readonly CreateContactCommandValidator _v = new();

    [Fact]
    public void Valido_pasa()
        => _v.Validate(new CreateContactCommand("Ada", "ada@example.com", "Hola")).IsValid.Should().BeTrue();

    [Theory]
    [InlineData("", "ada@example.com", "Hola")]
    [InlineData("Ada", "no-email", "Hola")]
    [InlineData("Ada", "ada@example.com", "")]
    public void Invalido_falla(string n, string e, string m)
        => _v.Validate(new CreateContactCommand(n, e, m)).IsValid.Should().BeFalse();
}
```
`CreateContactAckEmailCommandHandlerTests.cs`:
```csharp
using BigSchool.Application.Notifications.Commands.CreateContactAckEmail;
using BigSchool.Application.Notifications.Interfaces.Repositories;
using BigSchool.Domain.Notifications.Entities;
using BigSchool.Domain.Notifications.Enums;
using BigSchool.Domain.SharedKernel.Interfaces;
using Moq;
using Xunit;

namespace BigSchool.Application.Tests.Commands.Notifications;

public class CreateContactAckEmailCommandHandlerTests
{
    [Fact]
    public async Task Crea_EmailLog_Contact_y_guarda()
    {
        var uow = new Mock<IUnitOfWork>();
        var repo = new Mock<IEmailLogRepository>();
        repo.SetupGet(r => r.UnitOfWork).Returns(uow.Object);
        var handler = new CreateContactAckEmailCommandHandler(repo.Object);

        await handler.Handle(new CreateContactAckEmailCommand("ada@example.com", "Ada"), CancellationToken.None);

        repo.Verify(r => r.AddAsync(It.Is<EmailLog>(e => e.Type == EmailType.Contact && e.IdUser == null),
            It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(true), Times.Once);
    }
}
```
`SendContactAckEmailOnContactSubmittedHandlerTests.cs`:
```csharp
using BigSchool.Application.Notifications.Commands.CreateContactAckEmail;
using BigSchool.Application.Notifications.EventHandlers;
using BigSchool.Application.SharedKernel.Events;
using BigSchool.Domain.Notifications.Entities;
using BigSchool.Domain.Notifications.Events;
using MediatR;
using Moq;
using Xunit;

namespace BigSchool.Application.Tests.EventHandlers.Notifications;

public class SendContactAckEmailOnContactSubmittedHandlerTests
{
    [Fact]
    public async Task Dispara_CreateContactAckEmailCommand_con_datos_del_contacto()
    {
        var mediator = new Mock<IMediator>();
        var handler = new SendContactAckEmailOnContactSubmittedHandler(mediator.Object);
        var contact = Contact.Create("Ada", "ada@example.com", "Hola");

        await handler.Handle(new DomainEventNotification<ContactSubmittedDomainEvent>(
            new ContactSubmittedDomainEvent(contact)), CancellationToken.None);

        mediator.Verify(m => m.Send(
            It.Is<CreateContactAckEmailCommand>(c => c.Recipient == "ada@example.com" && c.FullName == "Ada"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

- [x] **Step 8: E2E — POST crea Contact + EXACTAMENTE 1 EmailLog (atómico); GET 401/paginado**

Crea `NotificationEndpointTestBase.cs` (mirror de `CompanyEndpointTestBase`: `SeedUserAsync`, `AuthenticatedClient`, helper `CountAsync(sql)` con `MySqlConnection(Fixture.ConnectionString)` + `ExecuteScalarAsync<int>`; DTOs de respuesta `ContactListItemResponse(int IdContact, string FullName, string Email, string Message, string CreatedAt)`). Añade a `MySqlDatabaseFixture.ResetAsync` (dentro de `FOREIGN_KEY_CHECKS=0`):
```csharp
await conn.ExecuteAsync("DELETE FROM EmailLogs;");
await conn.ExecuteAsync("DELETE FROM Contacts;");
```
`PostContactTests.cs`:
```csharp
    [Fact]
    public async Task Post_ValidContact_PersistsContactAndSingleAckEmailLog()
    {
        var client = Factory.CreateClient(); // AllowAnonymous
        var resp = await client.PostAsJsonAsync("/api/v1/contacts",
            new { fullName = "Ada Lovelace", email = "ada@example.com", message = "Hola" });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        // Atomicidad UoW componible: Contact + EmailLog persistidos en la misma transacción.
        (await CountAsync("SELECT COUNT(*) FROM Contacts WHERE Email='ada@example.com'")).Should().Be(1);
        // EXACTAMENTE 1: si SendContactAckEmailOnContactSubmittedHandler (INotificationHandler) se
        // registrara también en Autofac (además de en MediatR), Publish lo invocaría 2 veces vía
        // GetServices → 2 EmailLogs. Este assert caza esa regresión de doble instanciación.
        (await CountAsync("SELECT COUNT(*) FROM EmailLogs WHERE Recipient='ada@example.com' AND Type=2 AND IdUser IS NULL")).Should().Be(1);
        // Flujo A es DomainEvent intra-módulo (no Outbox): confirma que no se coló ninguna fila ahí.
        (await CountAsync("SELECT COUNT(*) FROM OutboxMessages")).Should().Be(0);
    }

    [Fact]
    public async Task Post_InvalidData_Returns400_AndPersistsNothing()
    {
        var resp = await Factory.CreateClient().PostAsJsonAsync("/api/v1/contacts",
            new { fullName = "", email = "no-email", message = "" });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CountAsync("SELECT COUNT(*) FROM Contacts")).Should().Be(0);
        (await CountAsync("SELECT COUNT(*) FROM EmailLogs")).Should().Be(0);
    }
```
`GetContactsTests.cs`:
```csharp
    [Fact]
    public async Task Get_WithoutToken_Returns401()
        => (await Factory.CreateClient().GetAsync("/api/v1/contacts")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact]
    public async Task Get_Authenticated_ReturnsGlobalInboxPaginated()
    {
        var (userId, email) = await SeedUserAsync();
        var pub = Factory.CreateClient();
        await pub.PostAsJsonAsync("/api/v1/contacts", new { fullName = "A", email = "a@x.com", message = "m" });
        await pub.PostAsJsonAsync("/api/v1/contacts", new { fullName = "B", email = "b@x.com", message = "m" });

        var env = await (await AuthenticatedClient(userId, email).GetAsync("/api/v1/contacts?page=1&pageSize=1"))
            .Content.ReadFromJsonAsync<ApiEnvelope<List<ContactListItemResponse>>>();
        env!.Data!.Should().HaveCount(1);
        env.Meta!.TotalCount.Should().Be(2);
    }
```

Run: `dotnet test tests/BigSchool.Integration.Tests --filter "PostContact|GetContacts"` → PASS.

- [x] **Step 9: Verde + Commit**

Run: FULL. Luego:
```bash
git add -A && git commit -m "feat(notifications): flujo Contacto (DomainEvent → command atómico) + POST/GET contacts + NotificationsModule

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 5: Flujo Welcome (inter-módulo, IntegrationEvent + Outbox) + guard tests

`Register` (Auth) → `UserRegisteredDomainEvent` → handler de Auth **encola** `UserRegisteredIntegrationEvent` (excepción documentada: no guarda) → `OutboxDispatchBehavior` drena post-commit → handler de Notifications dispara el command del `EmailLog` de bienvenida. Auth **no** referencia Notifications.

**Files:**
- Create: `src/BigSchool.Application/SharedKernel/IntegrationEvents/Contracts/UserRegisteredIntegrationEvent.cs`
- Create: `src/BigSchool.Application/Auth/EventHandlers/PublishIntegrationEventHandler.cs`
- Create: `src/BigSchool.Application/Notifications/Commands/CreateWelcomeEmail/{CreateWelcomeEmailCommand,CreateWelcomeEmailCommandHandler}.cs`
- Create: `src/BigSchool.Application/Notifications/EventHandlers/CreateWelcomeEmailOnUserRegisteredHandler.cs`
- Modify: `src/BigSchool.Infrastructure/Auth/DI/AuthModule.cs` (excluir INotificationHandler del escaneo)
- Modify: `tests/BigSchool.Architecture.Tests/ModuleBoundaryTests.cs` (incluir Notifications)
- **Fix crítico (no previsto en el plan original)**: `src/BigSchool.Infrastructure/SharedKernel/IntegrationEvents/OutboxDispatcher.cs` — reentrancia infinita. `CreateWelcomeEmailOnUserRegisteredHandler` hace `_mediator.Send(CreateWelcomeEmailCommand)`, que también pasa por `OutboxDispatchBehavior`; como el `MarkProcessed` original solo se guardaba al FINAL del `foreach` (tras invocar el handler), la consulta `WHERE ProcessedOn IS NULL` de la llamada anidada seguía viendo la fila como pendiente → recursión infinita (comprobado: 5000+ EmailLogs generados por un único registro antes de que el proceso quedara colgado). Fix: marcar `ProcessedOn` y hacer `SaveChangesAsync` ANTES de invocar el handler de cada mensaje. Añadido `ILogger<OutboxDispatcher>` para el catch. Test de regresión: `Dispatcher_HandlerReentrante_NoReprocesaLaFilaNiRecurseInfinitamente` en `OutboxTests.cs`.
- Test: `tests/BigSchool.Application.Tests/EventHandlers/Auth/PublishIntegrationEventHandlerTests.cs`; `tests/BigSchool.Application.Tests/Commands/Notifications/CreateWelcomeEmailCommandHandlerTests.cs`; `tests/BigSchool.Application.Tests/EventHandlers/Notifications/CreateWelcomeEmailTriggerTests.cs`
- Test: `tests/BigSchool.Integration.Tests/Auth/RegisterTests.cs` (nuevo caso `Register_NewUser_CreatesSingleWelcomeEmailLogAndDrainsOutbox` — vive en Auth, no en Notifications: el endpoint bajo prueba es `/api/v1/auth/register`; la consulta a `EmailLogs` queda documentada inline como cruce de frontera deliberado, solo válido en monolito modular)

- [x] **Step 1: Contrato compartido**

`UserRegisteredIntegrationEvent.cs`:
```csharp
namespace BigSchool.Application.SharedKernel.IntegrationEvents.Contracts;

public sealed record UserRegisteredIntegrationEvent(
    Guid EventId, DateTime OccurredOn, int IdUser, string Email, string FullName) : IIntegrationEvent;
```

- [x] **Step 2: Handler de Auth — EXCEPCIÓN documentada (encola, no guarda)**

`PublishIntegrationEventHandler.cs`:
```csharp
using BigSchool.Application.SharedKernel.Events;
using BigSchool.Application.SharedKernel.IntegrationEvents;
using BigSchool.Application.SharedKernel.IntegrationEvents.Contracts;
using BigSchool.Domain.Auth.Events;
using MediatR;

namespace BigSchool.Application.Auth.EventHandlers;

public sealed class PublishIntegrationEventHandler
    : INotificationHandler<DomainEventNotification<UserRegisteredDomainEvent>>
{
    private readonly IIntegrationEventOutbox _outbox;

    public PublishIntegrationEventHandler(IIntegrationEventOutbox outbox) => _outbox = outbox;

    public Task Handle(DomainEventNotification<UserRegisteredDomainEvent> notification, CancellationToken cancellationToken)
    {
        // EXCEPCIÓN CONSCIENTE a la norma "EventHandler → Send(Command)": aquí solo ENCOLAMOS la fila
        // de outbox y NO llamamos a SaveChangesAsync. El save-3 de la UoW componible la persiste en la
        // MISMA transacción que el User (atomicidad: no se "notifica" un registro que luego hace rollback).
        var u = notification.DomainEvent.User; // IdUser ya asignado (dispatch tras save-1)
        _outbox.Add(new UserRegisteredIntegrationEvent(
            Guid.NewGuid(), DateTime.UtcNow, u.IdUser, u.Email, u.FullName));
        return Task.CompletedTask;
    }
}
```

- [x] **Step 3: Command interno del welcome + disparador (norma)**

`CreateWelcomeEmailCommand.cs`:
```csharp
using MediatR;

namespace BigSchool.Application.Notifications.Commands.CreateWelcomeEmail;

public record CreateWelcomeEmailCommand(int IdUser, string Recipient, string FullName) : IRequest;
```
`CreateWelcomeEmailCommandHandler.cs`:
```csharp
using BigSchool.Application.Notifications.Interfaces.Repositories;
using BigSchool.Domain.Notifications.Entities;
using MediatR;

namespace BigSchool.Application.Notifications.Commands.CreateWelcomeEmail;

public class CreateWelcomeEmailCommandHandler : IRequestHandler<CreateWelcomeEmailCommand>
{
    private readonly IEmailLogRepository _emailLogs;

    public CreateWelcomeEmailCommandHandler(IEmailLogRepository emailLogs) => _emailLogs = emailLogs;

    public async Task Handle(CreateWelcomeEmailCommand request, CancellationToken cancellationToken)
    {
        await _emailLogs.AddAsync(EmailLog.CreateWelcome(request.IdUser, request.Recipient, request.FullName), cancellationToken);
        await _emailLogs.UnitOfWork.SaveChangesAsync();
    }
}
```
`CreateWelcomeEmailOnUserRegisteredHandler.cs` (disparador; `IIntegrationEventHandler`, lo invoca el bus post-commit):
```csharp
using BigSchool.Application.Notifications.Commands.CreateWelcomeEmail;
using BigSchool.Application.SharedKernel.IntegrationEvents;
using BigSchool.Application.SharedKernel.IntegrationEvents.Contracts;
using MediatR;

namespace BigSchool.Application.Notifications.EventHandlers;

public sealed class CreateWelcomeEmailOnUserRegisteredHandler
    : IIntegrationEventHandler<UserRegisteredIntegrationEvent>
{
    private readonly IMediator _mediator;

    public CreateWelcomeEmailOnUserRegisteredHandler(IMediator mediator) => _mediator = mediator;

    public async Task HandleAsync(UserRegisteredIntegrationEvent evt, CancellationToken cancellationToken)
    {
        await _mediator.Send(new CreateWelcomeEmailCommand(evt.IdUser, evt.Email, evt.FullName), cancellationToken);
    }
}
```
> `CreateWelcomeEmailOnUserRegisteredHandler` **no** es `INotificationHandler` → el escaneo de `NotificationsModule` (Tarea 4) lo registra como `IIntegrationEventHandler<UserRegisteredIntegrationEvent>` una sola vez; el bus lo resuelve por `GetServices`. No añadas registro explícito (duplicaría → 2 welcomes).

- [x] **Step 4: Fix DI en `AuthModule` (excluir INotificationHandler)**

En `AuthModule.cs`, añade el filtro `&& !IsMediatrNotificationHandler(t)` al escaneo de Application y el helper:
```csharp
using System.Linq;
using MediatR;
// …
        builder.RegisterAssemblyTypes(typeof(IUserRepository).Assembly)
            .Where(t => t.Namespace is not null
                        && t.Namespace.StartsWith("BigSchool.Application.Auth")
                        && !IsMediatrNotificationHandler(t))
            .AsImplementedInterfaces();
// … (el escaneo de Infrastructure.Auth NO cambia)

    internal static bool IsMediatrNotificationHandler(Type t) =>
        t.GetInterfaces().Any(i => i.IsGenericType
            && i.GetGenericTypeDefinition() == typeof(INotificationHandler<>));
```
> Sin esto, `PublishIntegrationEventHandler` se registraría en MediatR **y** en Autofac → `Publish` lo invocaría 2 veces → 2 filas de outbox → 2 welcomes. (`RegisterCommandHandler` es `IRequestHandler`, no afectado.)

- [x] **Step 5: Unit tests**

`PublishIntegrationEventHandlerTests.cs`:
```csharp
using BigSchool.Application.Auth.EventHandlers;
using BigSchool.Application.SharedKernel.Events;
using BigSchool.Application.SharedKernel.IntegrationEvents;
using BigSchool.Application.SharedKernel.IntegrationEvents.Contracts;
using BigSchool.Domain.Auth.Entities;
using BigSchool.Domain.Auth.Events;
using Moq;
using Xunit;

namespace BigSchool.Application.Tests.EventHandlers.Auth;

public class PublishIntegrationEventHandlerTests
{
    [Fact]
    public async Task Encola_UserRegisteredIntegrationEvent_con_datos_del_user()
    {
        var outbox = new Mock<IIntegrationEventOutbox>();
        var handler = new PublishIntegrationEventHandler(outbox.Object);
        var user = User.Create("ada@example.com", "hash", "salt", "Ada");

        await handler.Handle(new DomainEventNotification<UserRegisteredDomainEvent>(
            new UserRegisteredDomainEvent(user)), CancellationToken.None);

        outbox.Verify(o => o.Add(It.Is<UserRegisteredIntegrationEvent>(
            e => e.Email == "ada@example.com" && e.FullName == "Ada")), Times.Once);
    }
}
```
`CreateWelcomeEmailCommandHandlerTests.cs`:
```csharp
using BigSchool.Application.Notifications.Commands.CreateWelcomeEmail;
using BigSchool.Application.Notifications.Interfaces.Repositories;
using BigSchool.Domain.Notifications.Entities;
using BigSchool.Domain.Notifications.Enums;
using BigSchool.Domain.SharedKernel.Interfaces;
using Moq;
using Xunit;

namespace BigSchool.Application.Tests.Commands.Notifications;

public class CreateWelcomeEmailCommandHandlerTests
{
    [Fact]
    public async Task Crea_welcome_y_guarda()
    {
        var uow = new Mock<IUnitOfWork>();
        var repo = new Mock<IEmailLogRepository>();
        repo.SetupGet(r => r.UnitOfWork).Returns(uow.Object);
        var handler = new CreateWelcomeEmailCommandHandler(repo.Object);

        await handler.Handle(new CreateWelcomeEmailCommand(7, "ada@example.com", "Ada"), CancellationToken.None);

        repo.Verify(r => r.AddAsync(It.Is<EmailLog>(e => e.Type == EmailType.Welcome && e.IdUser == 7),
            It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(true), Times.Once);
    }
}
```
`CreateWelcomeEmailTriggerTests.cs`:
```csharp
using BigSchool.Application.Notifications.Commands.CreateWelcomeEmail;
using BigSchool.Application.Notifications.EventHandlers;
using BigSchool.Application.SharedKernel.IntegrationEvents.Contracts;
using MediatR;
using Moq;
using Xunit;

namespace BigSchool.Application.Tests.EventHandlers.Notifications;

public class CreateWelcomeEmailTriggerTests
{
    [Fact]
    public async Task Dispara_CreateWelcomeEmailCommand()
    {
        var mediator = new Mock<IMediator>();
        var handler = new CreateWelcomeEmailOnUserRegisteredHandler(mediator.Object);

        await handler.HandleAsync(new UserRegisteredIntegrationEvent(
            System.Guid.NewGuid(), System.DateTime.UtcNow, 7, "ada@example.com", "Ada"), CancellationToken.None);

        mediator.Verify(m => m.Send(
            It.Is<CreateWelcomeEmailCommand>(c => c.IdUser == 7 && c.Recipient == "ada@example.com"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

- [x] **Step 6: Actualizar `ModuleBoundaryTests` con Notifications**

En `ModuleBoundaryTests.cs`, añade Notifications a los `[InlineData]` (dominio y application) y prohíbe que los demás dependan de él. Dominio:
```csharp
    [InlineData("BigSchool.Domain.Auth", new[] { "BigSchool.Domain.Investments", "BigSchool.Domain.Notifications" })]
    [InlineData("BigSchool.Domain.Finanzas", new[] { "BigSchool.Domain.Auth", "BigSchool.Domain.Investments", "BigSchool.Domain.Notifications" })]
    [InlineData("BigSchool.Domain.Investments", new[] { "BigSchool.Domain.Auth", "BigSchool.Domain.Finanzas", "BigSchool.Domain.Notifications" })]
    [InlineData("BigSchool.Domain.Notifications", new[] { "BigSchool.Domain.Auth", "BigSchool.Domain.Finanzas", "BigSchool.Domain.Investments" })]
```
Y análogo para `BigSchool.Application.*` (incluye `BigSchool.Application.Notifications` como módulo y como prohibido en los demás). **Clave**: `BigSchool.Application.Auth` **no** debe depender de `BigSchool.Application.Notifications` (publica el contrato vía SharedKernel) → el test lo verifica.

- [x] **Step 7: E2E — register genera EXACTAMENTE 1 welcome tras drenar el outbox**

Añadido a `RegisterTests.cs` (Auth) — no a un fichero nuevo bajo Notifications: el endpoint bajo prueba es `/api/v1/auth/register`, responsabilidad de Auth, aunque la aserción cruce a leer `EmailLogs`/`OutboxMessages`:
```csharp
    [Fact]
    public async Task Register_NewUser_CreatesSingleWelcomeEmailLogAndDrainsOutbox()
    {
        var email = $"welcome-{Guid.NewGuid():N}@test.com";
        var resp = await RegisterRawAsync(email, DefaultPassword, "Ada Lovelace");
        resp.EnsureSuccessStatusCode();

        await using var conn = new MySqlConnection(Fixture.ConnectionString);
        var idUser = await conn.ExecuteScalarAsync<int>("SELECT IdUser FROM Users WHERE Email=@email", new { email });

        // Frontera Auth↔Notifications: lo único que Auth conoce de primera mano es que su propio
        // outbox se drenó. EXACTAMENTE 1 fila procesada: si PublishIntegrationEventHandler se
        // registrara también en Autofac (además de en MediatR/el bus), se dispararía 2 veces → 2 filas.
        (await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OutboxMessages WHERE ProcessedOn IS NOT NULL")).Should().Be(1);
        (await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OutboxMessages WHERE ProcessedOn IS NULL")).Should().Be(0);

        var payload = await conn.ExecuteScalarAsync<string>(
            "SELECT Payload FROM OutboxMessages WHERE Type LIKE '%UserRegisteredIntegrationEvent%' ORDER BY IdOutboxMessage DESC LIMIT 1");
        using var json = JsonDocument.Parse(payload);
        json.RootElement.GetProperty("IdUser").GetInt32().Should().Be(idUser);
        json.RootElement.GetProperty("Email").GetString().Should().Be(email);

        // Cruce de frontera DELIBERADO (solo válido en monolito modular con BD compartida): confirma
        // que Notifications efectivamente creó el EmailLog de bienvenida. En un microservicio estricto
        // Auth no podría consultar EmailLogs (tabla de otro servicio) y este assert se eliminaría.
        (await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM EmailLogs WHERE Recipient=@email AND Type=1 AND IdUser=@idUser", new { email, idUser }))
            .Should().Be(1);
    }
```
> El "**exactamente 1**" caza dos regresiones distintas: doble registro DI (Step 4) y la reentrada infinita del `OutboxDispatcher` (ver Fix crítico arriba).

Run: `dotnet test tests/BigSchool.Integration.Tests --filter "Register_NewUser_CreatesSingleWelcomeEmailLogAndDrainsOutbox"` y `dotnet test tests/BigSchool.Architecture.Tests` → PASS.

- [x] **Step 8: Verde + Commit**

Run: FULL. Luego:
```bash
git add -A && git commit -m "feat(notifications): welcome vía IntegrationEvent+Outbox (Auth→Notifications) + guard tests

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 6: Lecturas de emails — GET /emails (paginado, con visibilidad) + GET /emails/{id}

`GET /emails` devuelve los del usuario **o** globales (`IdUser = @user OR IdUser IS NULL`); `GET /emails/{id}` el detalle con `Body`, mismo filtro de visibilidad.

**Files:**
- Create: `src/BigSchool.Application/Notifications/DTOs/EmailLogListItemDto.cs`, `EmailLogDto.cs`
- Create: `src/BigSchool.Application/Notifications/Queries/GetEmails/{GetEmailsQuery,GetEmailsQueryHandler}.cs`
- Create: `src/BigSchool.Application/Notifications/Queries/GetEmailById/{GetEmailByIdQuery,GetEmailByIdQueryHandler}.cs`
- Create: `src/BigSchool.WebApi/Controllers/Notifications/EmailsController.cs`
- Test: `tests/BigSchool.Integration.Tests/Notifications/GetEmailsTests.cs`

- [ ] **Step 1: DTOs**

`EmailLogListItemDto.cs`:
```csharp
namespace BigSchool.Application.Notifications.DTOs;

public record EmailLogListItemDto(int IdEmailLog, int? IdUser, string Recipient, string Subject, short Type, DateTime SentAt);
```
`EmailLogDto.cs`:
```csharp
namespace BigSchool.Application.Notifications.DTOs;

public record EmailLogDto(int IdEmailLog, int? IdUser, string Recipient, string Subject, string Body, short Type, DateTime SentAt);
```

- [ ] **Step 2: Query de listado (paginada, con visibilidad)**

`GetEmailsQuery.cs`:
```csharp
using BigSchool.Application.Notifications.DTOs;
using BigSchool.Application.SharedKernel.Common;
using MediatR;

namespace BigSchool.Application.Notifications.Queries.GetEmails;

public record GetEmailsQuery(int IdUser, int Page, int PageSize) : IRequest<PagedResult<EmailLogListItemDto>>;
```
`GetEmailsQueryHandler.cs`:
```csharp
using BigSchool.Application.Notifications.DTOs;
using BigSchool.Application.SharedKernel.Common;
using BigSchool.Application.SharedKernel.Interfaces;
using BigSchool.Domain.SharedKernel.Enums;
using Dapper;
using MediatR;

namespace BigSchool.Application.Notifications.Queries.GetEmails;

public class GetEmailsQueryHandler : IRequestHandler<GetEmailsQuery, PagedResult<EmailLogListItemDto>>
{
    private readonly IDbConnectionFactory _dbFactory;

    public GetEmailsQueryHandler(IDbConnectionFactory dbFactory) => _dbFactory = dbFactory;

    private const string EMAILS_WHERE = @"WHERE IdStatus <> @StatusDeleted AND (IdUser = @IdUser OR IdUser IS NULL)";

    private const string GETEMAILS_QUERY = @"SELECT COUNT(*) FROM EmailLogs " + EMAILS_WHERE + @";
            SELECT IdEmailLog, IdUser, Recipient, Subject, Type, SentAt
            FROM EmailLogs " + EMAILS_WHERE + @"
            ORDER BY SentAt DESC, IdEmailLog DESC
            LIMIT @PageSize OFFSET @Offset;";

    public async Task<PagedResult<EmailLogListItemDto>> Handle(GetEmailsQuery request, CancellationToken cancellationToken)
    {
        var page = Pagination.NormalizePage(request.Page);
        var pageSize = Pagination.NormalizePageSize(request.PageSize);

        var parameters = new DynamicParameters();
        parameters.Add("@StatusDeleted", EntityStatus.Deleted);
        parameters.Add("@IdUser", request.IdUser);
        parameters.Add("@PageSize", pageSize);
        parameters.Add("@Offset", (page - 1) * pageSize);

        using var conn = _dbFactory.CreateConnection();
        using var multi = await conn.QueryMultipleAsync(GETEMAILS_QUERY, parameters);
        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<EmailLogListItemDto>()).ToList();
        return new PagedResult<EmailLogListItemDto>(items, page, pageSize, total);
    }
}
```

- [ ] **Step 3: Query de detalle (mismo filtro de visibilidad)**

`GetEmailByIdQuery.cs`:
```csharp
using BigSchool.Application.Notifications.DTOs;
using MediatR;

namespace BigSchool.Application.Notifications.Queries.GetEmailById;

public record GetEmailByIdQuery(int IdEmailLog, int IdUser) : IRequest<EmailLogDto?>;
```
`GetEmailByIdQueryHandler.cs`:
```csharp
using BigSchool.Application.Notifications.DTOs;
using BigSchool.Application.SharedKernel.Interfaces;
using BigSchool.Domain.SharedKernel.Enums;
using Dapper;
using MediatR;

namespace BigSchool.Application.Notifications.Queries.GetEmailById;

public class GetEmailByIdQueryHandler : IRequestHandler<GetEmailByIdQuery, EmailLogDto?>
{
    private readonly IDbConnectionFactory _dbFactory;

    public GetEmailByIdQueryHandler(IDbConnectionFactory dbFactory) => _dbFactory = dbFactory;

    private const string GETEMAILBYID_QUERY = @"SELECT IdEmailLog, IdUser, Recipient, Subject, Body, Type, SentAt
            FROM EmailLogs
            WHERE IdEmailLog = @IdEmailLog AND IdStatus <> @StatusDeleted
              AND (IdUser = @IdUser OR IdUser IS NULL)
            LIMIT 1;";

    public async Task<EmailLogDto?> Handle(GetEmailByIdQuery request, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@IdEmailLog", request.IdEmailLog);
        parameters.Add("@IdUser", request.IdUser);
        parameters.Add("@StatusDeleted", EntityStatus.Deleted);

        using var conn = _dbFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<EmailLogDto?>(GETEMAILBYID_QUERY, parameters);
    }
}
```

- [ ] **Step 4: `EmailsController` (`[Authorize]`)**

`EmailsController.cs`:
```csharp
using BigSchool.Application.Auth.Interfaces.Services;
using BigSchool.Application.Notifications.DTOs;
using BigSchool.Application.Notifications.Queries.GetEmailById;
using BigSchool.Application.Notifications.Queries.GetEmails;
using BigSchool.Application.SharedKernel.Common;
using BigSchool.WebApi.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BigSchool.WebApi.Controllers.Notifications;

[ApiController]
[Authorize]
[Route("api/v1/emails")]
public class EmailsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserIdEncryptor _encryptor;

    public EmailsController(IMediator mediator, IUserIdEncryptor encryptor)
    {
        _mediator = mediator;
        _encryptor = encryptor;
    }

    private int UserId => CurrentUser.GetId(User, _encryptor);

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<EmailLogListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetEmailsQuery(UserId, page, pageSize));
        var meta = new MetaData { Page = result.Page, PageSize = result.PageSize, TotalCount = result.TotalCount };
        return Ok(ApiResponse<IReadOnlyList<EmailLogListItemDto>>.Success(result.Items, meta));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<EmailLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _mediator.Send(new GetEmailByIdQuery(id, UserId));
        return result is null
            ? NotFound(ApiResponse.Fail(new ApiError { Code = "ENTITY_NOT_FOUND", Message = "Email no encontrado." }))
            : Ok(ApiResponse<EmailLogDto>.Success(result));
    }
}
```
> Confirma los namespaces reales de `IUserIdEncryptor` y `CurrentUser` como los usa `TransactionsController`.

- [ ] **Step 5: E2E — visibilidad + paginación + detalle ajeno 404**

`GetEmailsTests.cs`:
```csharp
    [Fact]
    public async Task GetEmails_SinToken_401()
        => (await Factory.CreateClient().GetAsync("/api/v1/emails")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact]
    public async Task GetEmails_VeSuWelcome_Y_LosDeContacto_NoElWelcomeAjeno()
    {
        var emailA = $"a-{Guid.NewGuid():N}@test.com";
        var pub = Factory.CreateClient();
        await pub.PostAsJsonAsync("/api/v1/auth/register", new { email = emailA, password = "Passw0rd!", fullName = "A" });
        await pub.PostAsJsonAsync("/api/v1/contacts", new { fullName = "Vis", email = "vis@x.com", message = "m" });
        var emailB = $"b-{Guid.NewGuid():N}@test.com";
        await pub.PostAsJsonAsync("/api/v1/auth/register", new { email = emailB, password = "Passw0rd!", fullName = "B" });

        var idA = await ScalarAsync<int>($"SELECT IdUser FROM Users WHERE Email='{emailA}'");
        var env = await (await AuthenticatedClient(idA, emailA).GetAsync("/api/v1/emails?page=1&pageSize=50"))
            .Content.ReadFromJsonAsync<ApiEnvelope<List<EmailLogListItemResponse>>>();

        env!.Data!.Should().Contain(e => e.Recipient == emailA && e.Type == 1);  // su welcome
        env.Data!.Should().Contain(e => e.Type == 2 && e.IdUser == null);         // contacto global
        env.Data!.Should().NotContain(e => e.Recipient == emailB);                // welcome ajeno NO
    }

    [Fact]
    public async Task GetEmailById_WelcomeAjeno_404()
    {
        var emailB = $"b-{Guid.NewGuid():N}@test.com";
        await Factory.CreateClient().PostAsJsonAsync("/api/v1/auth/register",
            new { email = emailB, password = "Passw0rd!", fullName = "B" });
        var idWelcomeB = await ScalarAsync<int>($"SELECT IdEmailLog FROM EmailLogs WHERE Recipient='{emailB}' AND Type=1");

        var (idA, emailA) = await SeedUserAsync();
        var resp = await AuthenticatedClient(idA, emailA).GetAsync($"/api/v1/emails/{idWelcomeB}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
```
> Añade helper `ScalarAsync<T>(sql)` al base (Dapper `ExecuteScalarAsync<T>`). `EmailLogListItemResponse(int IdEmailLog, int? IdUser, string Recipient, string Subject, short Type, string SentAt)`.

Run: `dotnet test tests/BigSchool.Integration.Tests --filter "GetEmails"` → PASS.

- [ ] **Step 6: Verde + Commit**

Run: FULL. Luego:
```bash
git add -A && git commit -m "feat(notifications): GET /emails (visibilidad + paginado) + GET /emails/{id}

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Verificación final (Definition of Done — spec 007 §14)

- [ ] **Unit Domain**: `Contact.Create` valida + levanta `ContactSubmittedDomainEvent`; `User.Create` levanta `UserRegisteredDomainEvent`; `EmailLog.CreateWelcome/CreateContactAck` fijan Type/IdUser/subject/body.
- [ ] **Unit Application**: validator; command handlers de acuse y welcome (crean EmailLog + guardan); disparadores (`Send` del command correcto); `PublishIntegrationEventHandler` encola.
- [ ] **UoW componible**: `POST /contacts` persiste `Contact` **y** `EmailLog` (Type=Contact, IdUser=null) — **exactamente 1** cada uno; register → **exactamente 1** welcome + `OutboxMessage.ProcessedOn` marcado (0 pendientes). Un doble = regresión del fix de DI (Tarea 5 Step 4).
- [ ] **E2E**: `GET /emails` ve el welcome propio + contactos globales, **no** welcomes ajenos, paginado; `GET /emails/{id}` de welcome ajeno → 404; `GET /contacts` 401 sin token, bandeja global paginada con token.
- [ ] **Arquitectura**: `ModuleBoundaryTests` en verde con Notifications; `Application.Auth` **no** depende de `Application.Notifications`.
- [ ] **FULL** verde (BUILD + UNIT + ARCH + INTEGRATION).

## Self-Review (cobertura de la spec 007)

| Sección | Tarea(s) |
|---|---|
| §5 UoW transaccional componible | 2 |
| §6 Estructura del módulo (namespace=carpeta) | 1–6 |
| §7 Modelo de datos (Contact, EmailLog, EmailType, migración) | 1, 3 |
| §8 Flujo A (Contacto, DomainEvent → command atómico) | 4 |
| §9 Flujo B (Welcome, IntegrationEvent+Outbox → command post-commit) | 5 |
| §10 Endpoints (POST/GET contacts, GET emails/{id}) | 4, 6 |
| §11 DI (NotificationsModule, exclusión INotificationHandler, boundary) | 4, 5 |
| §14 Verificación | "Verificación final" |

> **Norma aplicada:** EventHandlers = disparadores finos (`_mediator.Send(Command)`); los Commands hacen `entity.Add` + `SaveChangesAsync()`. **Excepción documentada en código:** `PublishIntegrationEventHandler` (Auth) encola la fila de outbox sin guardar (la persiste el save-3 de la UoW componible, atómico con el `User`).
