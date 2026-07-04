# Diseño: Backend — Notifications (EmailLog + Contactos + Welcome) (Spec 1)

**Fecha**: 2026-07-04
**Autor**: Alberto Sánchez
**Estado**: Diseño en revisión
**Módulo**: backend (módulo **Notifications**, nuevo) + mejora de la UoW (SharedKernel) + toque en Auth
**Backlog**: `docs/04-backend-tech-debt.md` (#5 EmailLog, #4 Contactos, #3/B Welcome). Primera feature
tras la fase fundacional (Specs 0 y 00). **Estrena** el `IIntegrationEventBus` + Outbox de la Spec 0.

---

## 1. Contexto y motivación

El frontend (plan 012, Tasks 13–14) construyó **Contacto** (formulario en la landing pública) y
**Emails Logging** (pantalla privada) contra **`localStorage`**, con `TODO (deuda técnica backend)`.
Esta spec crea el **módulo Notifications** (hoy inexistente) que materializa esos contratos y
**estrena los dos patrones de comunicación** que dejó lista la Spec 0, cada uno en su sitio:

- **DomainEvent intra-módulo, atómico**: alta de `Contact` → `EmailLog` (ambos en Notifications).
- **IntegrationEvent inter-módulo, post-commit**: `Register` (Auth) → `EmailLog` de bienvenida
  (Notifications), vía **Outbox** + `IIntegrationEventBus`. Auth **no** referencia Notifications.

## 2. Objetivo y no-objetivos

**Objetivo**
- Mejora de la UoW **(A)** en SharedKernel (prerequisito): domain events + sus efectos intra-BD se
  confirman **atómicamente** con el agregado (§5).
- Módulo Notifications con dos AR: **`Contact`** y **`EmailLog`** (+ enum `EmailType`).
- `POST /contacts` (**público**), `GET /contacts` (**privado**, paginado), `GET /emails` (**privado**,
  paginado), `GET /emails/{id}` (**privado**, detalle completo).
- Alta de contacto → `EmailLog` (tipo `Contact`) vía **DomainEvent** (atómico).
- `Register` → `EmailLog` (tipo `Welcome`) vía **IntegrationEvent + Outbox** (post-commit).

**No-objetivos**
- **No** hay envío real de emails (SMTP): `EmailLog` **simula** el envío persistiendo la fila.
- **No** se implementa update de usuario ni moneda en registro (Spec 008), ni RAG.
- **Frontend fuera de alcance** (solo §12 como apunte): esta spec es **backend puro**.

## 3. Decisiones de alcance (cerradas en revisión)

| Tema | Decisión |
|------|----------|
| Alcance de `Contact` | **Bandeja global**: sin `IdUser` (envíos anónimos). `GET /contacts` (privado) los devuelve **todos**. |
| Alcance de `EmailLog` | `IdUser` **nullable**. `GET /emails` filtra **`IdUser = @currentUser OR IdUser IS NULL`**: el usuario ve **su** bienvenida + **todos** los emails de contacto (`IdUser = null`). El detalle `GET /emails/{id}` aplica el **mismo** filtro (no leer el welcome de otro). |
| Contact → EmailLog | El alta **sí** genera `EmailLog` (tipo `Contact`, `IdUser = null`) vía **DomainEvent intra-módulo** (atómico con el `Contact`). |
| Modelado de `EmailLog` | **Aggregate Root** con repositorio (escritura EF, lectura Dapper). |
| Auth de contacto | `POST /contacts` **`[AllowAnonymous]`**; `GET /contacts`, `GET /emails`, `GET /emails/{id}` **`[Authorize]`**. |

## 4. Regla de comunicación (la que grabamos en esta conversación)

> El criterio **no** es "toca BD o no". Es: **¿debe ser atómico con el agregado, o es un efecto
> desacoplado?**

| Efecto | Mecanismo | ¿Cuándo corre? |
|--------|-----------|----------------|
| Cambio en el **mismo `DbContext`**, atómico con el agregado (fila outbox; **EmailLog de contacto**, mismo módulo) | **DomainEvent** handler → dentro de **(A)** | dentro de la transacción |
| Efecto **desacoplado**: otro módulo (**EmailLog de bienvenida**, Auth→Notifications), sistema externo, otra UoW | **IntegrationEvent → Outbox** (fila atómica) → **dispatcher** | **post-commit** (hoy in-process; async-ready) |

**DomainEvents = hechos** (`UserRegisteredDomainEvent`, `ContactSubmittedDomainEvent`, llevan la entidad
completa). **Handlers = acciones**, nombrados por lo que hacen, **1 hecho : N handlers**.

## 5. Prerequisito: mejora de la UoW (A) — SharedKernel

Hoy `BigSchoolDbContext.SaveChangesAsync` guarda y **luego** despacha domain events, sin volver a
guardar → lo que un handler añade al `DbContext` (p. ej. una fila de `OutboxMessage`) **no se
persiste**. (A) lo corrige envolviendo en transacción y persistiendo los efectos de los handlers
en la **misma** transacción:

```csharp
public async Task<int> SaveChangesAsync(bool dispatchEvents = true)
{
    if (!dispatchEvents)
        return await base.SaveChangesAsync(CancellationToken.None);

    // Componible: si ya hay transacción en curso (llamada anidada), la dueña es la exterior.
    var ownsTransaction = Database.CurrentTransaction is null && Database.IsRelational();
    await using var tx = ownsTransaction ? await Database.BeginTransactionAsync() : null;

    var result = await base.SaveChangesAsync(CancellationToken.None); // 1) agregado (IdUser ya asignado)
    await DispatchDomainEvents();                                     // 2) handlers reaccionan (encolan outbox, crean EmailLog…)

    if (ChangeTracker.HasChanges())
        await base.SaveChangesAsync(CancellationToken.None);          // 3) persiste los efectos intra-BD de los handlers

    if (tx is not null) await tx.CommitAsync();
    return result;
}
```

- **`CurrentTransaction is null`** hace la UoW **componible** ante llamadas anidadas a
  `SaveChangesAsync` (un handler que manda un command): la transacción la posee la más externa y el
  `COMMIT` es único → toda la cadena intra-BD queda atómica; sin `BeginTransaction` anidado (que
  MySQL rechazaría).
- **`IsRelational()`** protege proveedores sin transacciones (los E2E usan MySQL real).
- **Una sola ronda de dispatch** (simplificación consciente): si un handler mutara un agregado y
  levantara *nuevos* domain events, no se re-despacharían. Basta para este proyecto; si hiciera
  falta, se convierte en un bucle.
- **Efecto externo dentro de la transacción = anti-patrón**: un push a Elastic / envío real / otro
  módulo **no** va en un handler síncrono aquí (mantendría locks + arriesga inconsistencia en
  rollback). Va por **Outbox** (§4).

Test nuevo de atomicidad: si la transacción del agregado hace rollback, **no** queda fila de outbox.

## 6. Estructura del módulo (namespace = carpeta)

```
Domain/Auth/Events/                     UserRegisteredDomainEvent               (nuevo; hecho de dominio)
Domain/Notifications/Entities/          Contact, EmailLog
Domain/Notifications/Enums/             EmailType
Domain/Notifications/Events/            ContactSubmittedDomainEvent
Application/Auth/EventHandlers/         PublishIntegrationEventHandler
Application/SharedKernel/IntegrationEvents/Contracts/   UserRegisteredIntegrationEvent  (contrato compartido)
Application/Notifications/Commands/CreateContact/       CreateContactCommand(+Handler+Validator)
Application/Notifications/EventHandlers/                SendContactAckEmailOnContactSubmittedHandler,
                                                        CreateWelcomeEmailOnUserRegisteredHandler
Application/Notifications/Queries/{GetContacts,GetEmails,GetEmailById}/
Application/Notifications/DTOs/                          ContactListItemDto, EmailLogListItemDto, EmailLogDto
Application/Notifications/Interfaces/Repositories/       IContactRepository, IEmailLogRepository
Infrastructure/Notifications/Persistence/Configurations/  ContactConfiguration, EmailLogConfiguration
Infrastructure/Notifications/Persistence/Repositories/    ContactRepository, EmailLogRepository
Infrastructure/Notifications/DI/                        NotificationsModule (Autofac)
WebApi/Controllers/Notifications/                       ContactsController, EmailsController
```
Notifications depende **solo** de SharedKernel. Auth publica el contrato `UserRegisteredIntegrationEvent`
(que vive en SharedKernel) — **no** depende de Notifications. Se actualizan los `ModuleBoundaryTests`
para incluir Notifications.

## 7. Modelo de datos

**Enum `EmailType`** (`Domain/Notifications/Enums`, SMALLINT): `Welcome = 1`, `Contact = 2`.

**Contact** (AR) — tabla `Contacts`: `IdContact` PK · `FullName` VARCHAR(200) · `Email` VARCHAR(255)
· `Message` VARCHAR(2000) · `IdStatus` SMALLINT DEFAULT 2 · `CreatedAt` DATETIME.
`Contact.Create(fullName, email, message)` valida (no vacíos, email válido) y **levanta**
`ContactSubmittedDomainEvent(this)`.

**EmailLog** (AR) — tabla `EmailLogs`:
| Campo | Tipo | Notas |
|-------|------|------|
| IdEmailLog | INT PK | |
| IdUser | INT **NULL** | referencia por Id **sin FK dura** (frontera de módulo limpia; welcome = user, contacto = null) |
| Recipient | VARCHAR(255) NOT NULL | email destinatario simulado |
| Subject | VARCHAR(300) NOT NULL | |
| Body | VARCHAR(4000) NOT NULL | contenido simulado (lo consumirá el detalle) |
| Type | SMALLINT NOT NULL | enum `EmailType` |
| SentAt | DATETIME NOT NULL | fecha del "envío" simulado |
| IdStatus | SMALLINT NOT NULL DEFAULT 2 | |
| CreatedAt | DATETIME NOT NULL | |

Factories que encapsulan `Type`/`Subject`/`Body` simulados:
`EmailLog.CreateWelcome(int idUser, string recipient, string fullName)` (Type=Welcome) ·
`EmailLog.CreateContactAck(string recipient, string fullName)` (Type=Contact, IdUser=null).
Migración `AddNotificationsModule` (2 tablas). `IdUser` **sin FK** (referencia suave por Id).

## 8. Flujo A — Contacto (intra-módulo, DomainEvent, atómico)

1. `POST /contacts` `[AllowAnonymous]` → `CreateContactCommand` (FluentValidation).
2. `CreateContactCommandHandler`: `Contact.Create(...)` → `IContactRepository.AddAsync` →
   `SaveChangesAsync()`. Dentro de **(A)**: save 1 (Contact) → dispatch → el handler añade el EmailLog
   → save 3 lo persiste en la **misma** transacción → commit. **Contact + EmailLog atómicos.**
3. `SendContactAckEmailOnContactSubmittedHandler` (`INotificationHandler<DomainEventNotification<ContactSubmittedDomainEvent>>`):
   ```csharp
   var c = notification.DomainEvent.Contact;
   await _emailLogs.AddAsync(EmailLog.CreateContactAck(recipient: c.Email, fullName: c.FullName), ct);
   // NO hace SaveChanges: lo persiste el save 3 de (A), atómico con el Contact.
   ```

## 9. Flujo B — Welcome (inter-módulo, IntegrationEvent + Outbox, post-commit)

**Hecho (Auth)**: `User.Create` levanta `UserRegisteredDomainEvent(this)` (una línea; `RegisterCommandHandler`
**no cambia**).

**Traducción a integración (Auth)** — `PublishIntegrationEventHandler`
(`INotificationHandler<DomainEventNotification<UserRegisteredDomainEvent>>`): construye el contrato y lo
**encola** (no guarda; lo persiste el save 3 de (A), atómico con el `User`):
```csharp
var u = notification.DomainEvent.User;   // IdUser ya asignado (dispatch tras save 1)
_outbox.Add(new UserRegisteredIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, u.IdUser, u.Email, u.FullName));
```

**Contrato compartido** (`Application/SharedKernel/IntegrationEvents/Contracts/`):
```csharp
public sealed record UserRegisteredIntegrationEvent(
    Guid EventId, DateTime OccurredOn, int IdUser, string Email, string FullName) : IIntegrationEvent;
```

**Consumo (Notifications)** — `CreateWelcomeEmailOnUserRegisteredHandler`
(`IIntegrationEventHandler<UserRegisteredIntegrationEvent>`): lo invoca el `OutboxDispatcher`
**post-commit** (drenado por el `OutboxDispatchBehavior`); crea el `EmailLog` en **su propia** UoW:
```csharp
await _emailLogs.AddAsync(EmailLog.CreateWelcome(evt.IdUser, evt.Email, evt.FullName), ct);
await _emailLogs.UnitOfWork.SaveChangesAsync();
```
Se registra en `NotificationsModule` para que el bus lo resuelva por `GetServices` (permite **1:N**).

## 10. Endpoints (todos bajo `/api/v1`)

| Método | Ruta | Auth | Descripción |
|--------|------|------|-------------|
| POST | `/contacts` | `[AllowAnonymous]` | Crea contacto (→ EmailLog tipo Contact, atómico) — EF |
| GET | `/contacts` | `[Authorize]` | Bandeja global paginada — Dapper |
| GET | `/emails` | `[Authorize]` | `IdUser = @user OR IdUser IS NULL`, paginado — Dapper |
| GET | `/emails/{id}` | `[Authorize]` | Detalle completo (incl. `Body`); mismo filtro de visibilidad — Dapper |

**Paginación (Spec 00)**: los listados nuevos nacen paginados (`Pagination` + `PagedResult<T>` +
`MetaData`). `ORDER BY` con desempate único: contactos `CreatedAt DESC, IdContact DESC`; emails
`SentAt DESC, IdEmailLog DESC`. El detalle devuelve `EmailLogDto` con todos los campos (para que el
frontend, fuera de alcance, pinte el email con estética).

## 11. DI y arquitectura

- `NotificationsModule` (Autofac): repos (`ContactRepository`, `EmailLogRepository`), y **registro
  explícito** de los `IIntegrationEventHandler<>` de Notifications (el bus los resuelve por
  `GetServices`). Los `INotificationHandler<>` (domain events) siguen el escaneo MediatR existente.
- `ModuleBoundaryTests` actualizado: Notifications depende solo de SharedKernel; **Auth no depende
  de Notifications** (publica el contrato vía SharedKernel).

## 12. Frontend (solo apunte — FUERA DE ALCANCE)

No se implementa en esta spec. Queda anotado para una sesión de frontend posterior: reconectar
`useContacts`/`useEmails` (hoy `localStorage`) a `POST/GET /contacts` y `GET /emails` + detalle
`GET /emails/{id}`, retirar los `TODO (deuda técnica backend)`, y presentar el email con estética
(referencia de diseño `src/frontend-web/src/app/globals.css`).

## 13. Fuera de alcance

- Envío real (SMTP), plantillas ricas, adjuntos, estados avanzados (bounce/opened).
- Borrado/gestión de contactos o emails (solo alta + listado + detalle).
- Todo el frontend (§12).

## 14. Verificación

- **Unit (Domain)**: `Contact.Create` valida y **levanta** `ContactSubmittedDomainEvent`;
  `User.Create` levanta `UserRegisteredDomainEvent`; `EmailLog.CreateWelcome/CreateContactAck` fijan
  `Type`/`IdUser`/subject/body.
- **Unit (Application)**: los tres handlers (publish, contact-ack, welcome) producen el efecto
  correcto (mocks); validador de `CreateContactCommand`.
- **Integración — UoW (A)**: rollback del agregado ⇒ **0** filas de `OutboxMessage`; commit ⇒
  agregado + outbox persistidos en la misma transacción.
- **Integración E2E** (`WebApplicationFactory` + MySQL `bigschool_test`):
  - `POST /contacts` sin token → 200; persiste `Contact` **y** `EmailLog` (`Type=Contact`,
    `IdUser=null`) **atómicamente**.
  - `POST /auth/register` → tras el drenado del outbox, existe `EmailLog` (`Type=Welcome`,
    `IdUser = nuevo`, `Recipient = email`); el `OutboxMessage` quedó con `ProcessedOn` marcado.
  - `GET /emails` (usuario recién registrado) → su welcome + los de contacto (`IdUser=null`),
    **no** welcomes de otros; paginado (`totalCount`, cap 100, sin solape). `GET /emails/{id}` de un
    welcome ajeno → 404/no visible.
  - `GET /contacts` `[Authorize]` (401 sin token) → bandeja global paginada.
- **Arquitectura**: `ModuleBoundaryTests` en verde con Notifications incluido.

---

## Referencias
- Regla y plumbing: Spec 0 (`005-…`) — `IIntegrationEventBus`, `IIntegrationEventOutbox`,
  `OutboxDispatchBehavior`, `Contracts/`. Paginación: Spec 00 (`006-…`) — `Pagination`, `PagedResult<T>`.
- Convenciones: `src/backend/AGENTS.md` (DomainEvents llevan la entidad; factories; Dapper `_QUERY`).
- Frontend a reconectar (futuro): `src/frontend-web/src/hooks/{useContacts,useEmails}.ts`, `contacts/`, `emails/`.
