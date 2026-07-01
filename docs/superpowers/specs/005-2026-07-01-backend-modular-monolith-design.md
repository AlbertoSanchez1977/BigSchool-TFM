# Diseño: Backend — Monolito Modular (Spec 0, fundacional)

**Fecha**: 2026-07-01
**Autor**: Alberto Sánchez
**Estado**: Diseño en revisión
**Módulo**: backend (transversal)
**Backlog**: `docs/04-backend-tech-debt.md` (Spec 0). Precede a las specs de features (006–010).

---

## 1. Contexto y motivación

El backend es hoy un **monolito con Clean Architecture layer-first**: 4 proyectos
(`BigSchool.Domain`, `.Application`, `.Infrastructure`, `.WebApi`) y, dentro de cada uno,
carpetas por feature (`Commands/Auth`, `Commands/Investments`, `Commands/Transactions`,
`Queries/Categories`…). Las fronteras entre áreas funcionales existen **por convención de
carpeta**, pero nada las hace explícitas ni las protege: `Domain/Entities/` mezcla todas las
entidades en una carpeta, y la DI (`Program.cs`) registra por *ensamblado de capa*, no por módulo.

Antes de añadir features (Notifications, moneda en registro, agregaciones, summaries) queremos
convertirlo en un **monolito modular**: mismo despliegue único, pero con **módulos funcionales de
frontera explícita**, comunicación inter-módulo por **eventos de integración** sobre una interfaz
que *simula* un bus asíncrono (RabbitMQ / Service Bus) **sin serlo**, dejándolo **preparado para
extraer microservicios** el día de mañana sin reescribir la lógica.

Es un refactor **estructural** (reubicación de carpetas/namespaces + costuras nuevas), **sin
cambio de comportamiento funcional** salvo la re-modelación acotada de `SubCategory` (§7). Por eso
va **primero**: cada spec posterior nace ya en la estructura modular.

## 2. Objetivo y no-objetivos

**Objetivo**
- Módulos funcionales explícitos: **Auth**, **Finanzas**, **Investments**, **Notifications**, más
  un **SharedKernel** transversal.
- Comunicación inter-módulo por **IntegrationEvents** sobre `IIntegrationEventBus` (impl in-memory).
- Intra-módulo se mantiene con **DomainEvents** (ya existentes).
- DI **alineada al módulo** (un módulo Autofac por módulo funcional).
- **Guard tests** (NetArchTest) que impiden que las fronteras se erosionen.
- Cerrar la decisión **Categorías → Finanzas** (§7).

**No-objetivos (explícitos)**
- **No** se parte en un `.csproj` por módulo (sigue habiendo 4 proyectos de capa). El split de
  assemblies queda documentado como evolución futura (§3, Opción B).
- **No** se introduce RabbitMQ / Service Bus real, ni broker, ni colas: el bus es in-process.
- **No** se separan bases de datos ni `DbContext` por módulo (un solo `BigSchoolDbContext`); solo
  se parten las *configuraciones* EF por carpeta de módulo y se prohíben joins/FKs cross-módulo.
- **No** se implementan features del backlog (#1–#11); esta spec solo prepara el terreno.

## 3. Decisión estructural

| Opción | Descripción | Enforcement | Coste | Veredicto |
|--------|-------------|-------------|-------|-----------|
| **A — Layer-first + carpetas de módulo (recomendada)** | Se mantienen los 4 proyectos de capa; dentro de cada uno, carpeta/namespace por módulo + `SharedKernel`. | Convención + **NetArchTest** | Bajo (reubicación) | **Elegida** |
| B — Module-first (un `.csproj` por módulo) | `BigSchool.Modules.{Auth,Finanzas,Investments,Notifications}`, cada uno internamente por capa. | Compilador | Alto (churn masivo) | Futuro documentado |

Se elige **A**: encaja con "solo reubicaciones de carpeta/namespace", mantiene verde el pipeline
de tests con cambios mecánicos y entrega el 80 % del valor (fronteras visibles + bus de
integración + guard tests). La Opción B se puede acometer más adelante partiendo de A sin rehacer
la lógica.

## 4. Mapa de módulos y SharedKernel

| Módulo | Aggregate Roots / entidades | Responsabilidad |
|--------|-----------------------------|-----------------|
| **Auth** | `User` (+ `RefreshToken` futuro) | Identidad, registro, login, perfil de usuario |
| **Finanzas** | `Transaction` (AR indep.), `SubCategory` (AR indep. tras §7) | Gastos/ingresos, categorías, agregaciones |
| **Investments** | `Company` (+`Valuation`), `Portfolio` (+`Holding`+`Disposal`) | Catálogo, carteras, holdings, performance |
| **Notifications** | `Contact`, `EmailLog` *(se crean en Spec 007)* | Contacto y simulación de emails |
| **SharedKernel** | `BaseEntity`, `IAggregateRoot`, `IDomainEvent`, `IUnitOfWork`, `Money`, `MoneyConversion`, `Currency`, `EntityStatus`, envelope `ApiResponse`, `IDbConnectionFactory`, `PagedResult<T>` (Spec 00), abstracciones + `OutboxMessage`/`OutboxDispatcher` de IntegrationEvents, `ExchangeRate` + `ExchangeRateApiClient` | Contratos y building blocks compartidos por todos los módulos |

Notas:
- **`ExchangeRate`** es dato de referencia usado por Finanzas e Investments → vive en SharedKernel
  (infra), no en un módulo de negocio. Ya está fuera del UoW y sin repositorio.
- **`RagDocument`** (IA/RAG) queda en un módulo **`Rag` marcado futuro** (fuera de alcance MVP); se
  reubica su carpeta pero no se toca su lógica.
- Cada módulo referencia a los demás **solo por Id** (p. ej. `Transaction.IdUser`, sin navegación)
  y **solo a través del SharedKernel o de contratos públicos** — nunca clases internas de otro módulo.

## 5. Layout objetivo (de → a)

Namespace: **`BigSchool.{Capa}.{Módulo}`** (p. ej. `BigSchool.Domain.Finanzas`,
`BigSchool.Application.Notifications`). SharedKernel: `BigSchool.{Capa}.SharedKernel`.

```
BigSchool.Domain/
  SharedKernel/     BaseEntity, IAggregateRoot, IDomainEvent, IUnitOfWork, ValueObjects/{Money,MoneyConversion}, Enums/{Currency,EntityStatus}
  Auth/             User
  Finanzas/         Transaction, SubCategory, Enums/{TransactionType,MainCategory}
  Investments/      Company, Valuation, Portfolio, Holding, Disposal
  Notifications/    (Contact, EmailLog → Spec 007)
  Rag/ (futuro)     RagDocument, ExchangeRate→SharedKernel

BigSchool.Application/
  SharedKernel/     Interfaces (IRepository, IDbConnectionFactory), Events (DomainEventNotification),
                    IntegrationEvents (IIntegrationEventBus, IIntegrationEvent, IIntegrationEventHandler),
                    Behaviors, Common (PagedResult<T>)
  Auth/             Commands/{Register,Login,Refresh}, DTOs, Interfaces
  Finanzas/         Commands/{Transactions/*}, Queries/{Transactions/*, Categories/*}, DTOs
  Investments/      Commands/*, Queries/*, DTOs
  Notifications/    (Spec 007)

BigSchool.Infrastructure/
  SharedKernel/     BigSchoolDbContext, DbConnectionFactory, Services/{ExchangeRateApiClient},
                    IntegrationEvents/{InMemoryIntegrationEventBus, OutboxMessage, OutboxDispatcher},
                    Persistence/Configurations/OutboxMessageConfiguration, DI/SharedKernelModule
  Auth/             Persistence/{Configurations,Repositories}, Services/{JwtService,Argon2PasswordHasher}, DI/AuthModule
  Finanzas/         Configurations, Repositories, DI/FinanzasModule
  Investments/      Configurations, Repositories, DI/InvestmentsModule
  Notifications/    (Spec 007)

BigSchool.WebApi/
  Controllers/Auth, Controllers/Finanzas, Controllers/Investments, Controllers/Notifications
```

El `BigSchoolDbContext` es único y vive en SharedKernel/Infrastructure; sus `IEntityTypeConfiguration`
se **reparten por carpeta de módulo** y se aplican con `ApplyConfigurationsFromAssembly`.

## 6. Comunicación inter-módulo: Domain vs Integration events

Regla de oro:
- **DomainEvent = intra-módulo.** Se maneja dentro del mismo bounded context, en la misma unidad de
  trabajo. Lleva **la entidad de dominio** (convención ya vigente, AGENTS.md). No cruza fronteras.
- **IntegrationEvent = inter-módulo.** Publicado por el módulo origen **tras el commit local**;
  consumido por otro módulo. Lleva **solo primitivos** (`IdUser`, `Email`, `FullName`…), nunca
  entidades de dominio, y un `EventId` (Guid) + `OccurredOn`.

### 6.1 Contratos y ubicación

```csharp
// SharedKernel/Application — abstracciones
public interface IIntegrationEvent      { Guid EventId { get; } DateTime OccurredOn { get; } }
public interface IIntegrationEventHandler<in T> where T : IIntegrationEvent
{ Task HandleAsync(T @event, CancellationToken ct); }
public interface IIntegrationEventBus    { Task PublishAsync(IIntegrationEvent @event, CancellationToken ct); }
```

Los **contratos concretos** (p. ej. `UserRegisteredIntegrationEvent`) viven en un espacio
**compartido y público** (`Application/SharedKernel/IntegrationEvents/Contracts/`) que **ambos**
módulos referencian. El **publisher (Auth) nunca referencia Notifications**; el **handler** es
privado del consumidor (Notifications) y se descubre por DI.

### 6.2 Implementación in-memory (simula async, sin serlo)

`InMemoryIntegrationEventBus` (SharedKernel/Infrastructure) resuelve por reflexión los
`IIntegrationEventHandler<T>` registrados y los invoca. Interfaz idéntica a la que tendría un bus
real → el día de RabbitMQ/SB **solo cambia la implementación**, ni publishers ni handlers.

### 6.3 Publicación transaccional vía Outbox (elegido)

Un IntegrationEvent **no** debe emitirse de forma que pueda "notificar" algo que luego hace
rollback. Patrón elegido: **Outbox ligero** — atomicidad real y preparado para async:

1. El command handler, en la **misma** unidad de trabajo que muta el agregado, inserta un
   `OutboxMessage` (`Type`, `Payload` JSON, `EventId`, `OccurredOn`, `ProcessedOn = null`).
   `SaveChangesAsync()` confirma **agregado + outbox en una sola transacción** → o se guardan
   ambos, o ninguno. (Se acabó el riesgo de notificar sobre un commit que no ocurrió.)
2. Un `OutboxDispatcher` lee los pendientes (`ProcessedOn == null`), los deserializa y los publica
   por `IIntegrationEventBus.PublishAsync`; al terminar marca `ProcessedOn` (y `Error` si falla).
3. El bus in-memory invoca el `IIntegrationEventHandler<T>` del módulo consumidor, que abre **su
   propia** unidad de trabajo (p. ej. escribe un `EmailLog`).

**Disparo del dispatcher (evolución sin tocar contratos):** ahora se drena **post-commit en
proceso** (tras `SaveChangesAsync`, mismo request). El salto natural a *async real* es un
`BackgroundService` que hace polling del outbox — mismo `OutboxMessage`, mismo bus, mismos
handlers; solo cambia el disparador. `EventId` habilita además el **dedupe** idempotente cuando el
transporte sea un broker real.

**Tabla `OutboxMessage`** (SharedKernel/Infrastructure, migración EF): `IdOutboxMessage`, `EventId`
(Guid, UNIQUE), `Type` (nombre del contrato), `Payload` (JSON), `OccurredOn`, `ProcessedOn`
(NULL = pendiente), `Error` (NULL). Es **infraestructura**, no AR de negocio: sin `IdStatus` ni
Global Query Filter (como `ExchangeRate`).

### 6.4 Idempotencia

Como hoy es in-process síncrono no hace falta dedupe, pero el diseño lo contempla: el
`IIntegrationEvent` lleva `EventId`; cuando el bus sea async real, los handlers deduplicarán por
`EventId`. Se documenta como requisito futuro, no se implementa ahora.

### 6.5 Caso canónico (se materializa en Spec 007)

`Register` (Auth) → commit del `User` → `PublishAsync(UserRegisteredIntegrationEvent{IdUser,Email,
FullName})` → handler en **Notifications** escribe un `EmailLog` de bienvenida. Dentro de
Notifications, alta de `Contact` → `ContactSubmittedEvent` (**DomainEvent** intra-módulo) → escribe
`EmailLog`. Así se ejercitan **ambos** patrones.

## 7. Categorías → Finanzas (decisión cerrada)

`SubCategory` es hoy **entidad hija del AR `User`** (invariante: nombre único por
usuario+`MainCategory`). La taxonomía de presupuesto es un concepto de **Finanzas**, no de
Identity/Auth. **Decisión (cerrada en esta spec):** `SubCategory` pertenece al módulo **Finanzas**.

Implicación de modelado: al salir del agregado `User`, `SubCategory` pasa a ser **AR independiente**
en Finanzas que referencia al usuario por `IdUser` (mismo patrón que `Transaction`). La invariante
de unicidad se valida en el **command handler** (guard query previa) en vez de dentro del agregado
`User` — coherente con los otros AR independientes del proyecto.

**Secuencia:** la **decisión** se cierra aquí (Spec 0); la **ejecución** (mover físicamente
`SubCategory` a Finanzas + re-modelarla como AR independiente + su CRUD #8/C) se realiza en la
**Spec 009 (Finanzas)**, para mantener la Spec 0 como refactor puramente estructural y de bajo
riesgo. Hasta entonces, la lectura de categorías (`GET /categories`) sigue funcionando igual.
*(Alternativa a valorar en revisión: ejecutar también el movimiento en Spec 0; se descarta por
mezclar cambio de comportamiento con la reubicación.)*

## 8. Inyección de dependencias alineada al módulo

Se sustituye el registro por *ensamblado de capa* de `Program.cs` por **un módulo Autofac por
módulo funcional**, que autoregistra sus handlers, repositorios y servicios:

- `SharedKernelModule`, `AuthModule`, `FinanzasModule`, `InvestmentsModule`, `NotificationsModule`
  (en `Infrastructure/{Módulo}/DI/`).
- `Program.cs` solo compone: registra los módulos, el `DbContext`, MediatR, el
  `InMemoryIntegrationEventBus` y el pipeline HTTP.
- Opcional (evolución): patrón `IModule { RegisterServices(); MapEndpoints(); }` para que WebApi
  sea un host fino. No obligatorio en esta spec.

## 9. Enforcement de fronteras (guard tests)

Nuevo proyecto/carpeta de tests `BigSchool.Architecture.Tests` con **NetArchTest**:

- Ningún módulo de negocio depende de otro módulo de negocio (Auth ⊥ Finanzas ⊥ Investments ⊥
  Notifications); solo pueden depender de **SharedKernel**.
- La comunicación cross-módulo ocurre **solo** vía `IIntegrationEvent`/contratos públicos.
- Domain no referencia Infrastructure ni EF (regla ya vigente, ahora testeada).
- `Program`/composición es el único punto autorizado a conocer todos los módulos.

Estos tests son la red que evita que la modularidad se degrade sin coste de compilador.

## 10. Estrategia de migración y secuencia

Refactor mecánico, **tests verdes antes y después de cada paso**, en varios commits/PR pequeños
sobre la misma feature branch:

1. Crear `SharedKernel` (mover building blocks + `ExchangeRate`); ajustar namespaces.
2. Reubicar Domain por módulo (Auth/Finanzas/Investments); `Rag` a carpeta futura.
3. Reubicar Application (Commands/Queries/DTOs/Interfaces) por módulo.
4. Reubicar Infrastructure (Configurations/Repositories/Services) por módulo + repartir EF configs.
5. Reubicar Controllers por módulo.
6. Introducir `IIntegrationEventBus` + impl in-memory + contratos + tabla `OutboxMessage`
   (migración EF) + `OutboxDispatcher` post-commit (sin consumidores aún; se estrenan en Spec 007).
7. Migrar DI a módulos Autofac por módulo.
8. Añadir `BigSchool.Architecture.Tests` (guard tests) y dejarlos en verde.

Cada paso es reversible y no cambia comportamiento (salvo lo ya acotado). Los IDEs hacen la mayor
parte (rename namespace + move). Se verifica `dotnet build` + `dotnet test` tras cada paso.

## 11. Fuera de alcance

- Split en `.csproj` por módulo (Opción B) — futuro.
- Bus/broker real (RabbitMQ/Service Bus) y las políticas de reintento/poison-queue/backoff y el
  `BackgroundService` de polling — futuro. El **outbox ligero** (tabla + dispatcher post-commit) **sí
  entra** en esta spec (§6.3).
- Separación física de BD / `DbContext` por módulo — futuro.
- Cualquier feature del backlog #1–#11 y la re-modelación de `SubCategory` (esta última en Spec 009).
- Módulo Rag / IA (permanece esqueleto).

## 12. Verificación

- **Build & tests**: `dotnet build` limpio y **toda la suite existente en verde** (unit +
  integración E2E) tras el refactor — es la prueba de que no hubo cambio de comportamiento.
- **Guard tests**: `BigSchool.Architecture.Tests` en verde (fronteras respetadas).
- **Bus de integración**: test unitario del `InMemoryIntegrationEventBus` (publica → invoca el
  handler registrado; sin handler → no-op). Un test que verifique que publicar un
  `IIntegrationEvent` **no** requiere que el publisher referencie el módulo consumidor.
- **Outbox (atomicidad)**: test de integración que, si la transacción del agregado hace rollback,
  **no** deja `OutboxMessage`; si commitea, el `OutboxDispatcher` lo drena, marca `ProcessedOn` e
  invoca al handler exactamente una vez (idempotencia por `EventId`).
- **Smoke E2E**: la API arranca (`/health` 200) y los endpoints existentes responden igual que
  antes del refactor (mismos contratos, mismos códigos).
- **Sin regresión de contrato**: los tipos del frontend no cambian (esta spec no toca la API
  pública, solo su organización interna).

---

## Referencias
- Backlog y orden de specs: `docs/04-backend-tech-debt.md`.
- Modelo de datos y convenciones: `docs/02-backend-design.md`.
- Convenciones de código backend: `src/backend/AGENTS.md`.
- Siguiente spec que estrena el bus: `007-…-backend-notifications-emails-contactos-design.md`.
