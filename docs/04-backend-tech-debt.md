# BigSchool-TFM — Deuda Técnica de Backend (post-Frontend MVP)

**Fecha**: 2026-07-01
**Estado**: Backlog acotado y aprobado (alimenta specs/planes por módulo)
**Relación**: aflora tras el Frontend-Web MVP (`docs/superpowers/plans/012-2026-06-24-frontend-web-mvp.md`).

---

## Contexto

Al construir el Frontend-Web MVP sobre el Backend ya existente afloró **deuda técnica de
backend**: piezas que el frontend simula (`localStorage`, cálculo en navegador, campos
`TODO (deuda técnica)`) o que no existen aún. Este documento **no es un plan de
implementación**: es el **backlog consolidado y acotado** que alimentará las sesiones de
spec/plan posteriores. Confirma que la lista está completa y con el alcance decidido antes de
especificar nada.

Verificado contra: `docs/02-backend-design.md`,
`docs/superpowers/specs/003-2026-06-24-frontend-web-mvp-design.md`,
`docs/superpowers/plans/012-2026-06-24-frontend-web-mvp.md` y el código real de
`src/backend/src/BigSchool.WebApi/Controllers/*` + `…/BigSchool.Application/**`.

Arquitectura a respetar en todo (ver `docs/02-backend-design.md`): Clean Architecture + DDD
(Aggregate Roots con repositorio; hijas sin mutación pública), **CQRS** (Commands → EF Core +
FluentValidation + Domain Events; Queries → Dapper con SQL `private const …_QUERY` +
`DynamicParameters`), Autofac modular, AutoMapper, envelope `{ data, errors, meta }`, soft
delete por `IdStatus=4` + Global Query Filter. Tests: unit (Domain/Application) + integración
E2E sobre MySQL real. Entidades hijas se testean **a través del AR** (sin `InternalsVisibleTo`).

---

## Decisiones de alcance (acordadas 2026-07-01)

| Tema | Decisión |
|------|----------|
| **Holdings (#6)** | Solo **campos Original en los DTOs**, calculados server-side on-the-fly (Dapper). **Sin** persistir/denormalizar summaries. El objetivo "no calcular en caliente" se cumple moviendo el cálculo del **navegador al backend** (fuente única), no persistiéndolo. |
| **Companies/Valuations charts (#5)** | Serie temporal de **precio de empresa** (catálogo global `Valuations`, sin `IdUser`) con selector de periodo 3m/6m/1y/3y/5y. **No** incluye serie de valor de cartera por-usuario. |
| **Contactos vs Emails** | **Módulos separados**. Entidad `Contact` con CRUD propio; `EmailLog` aparte para la simulación de envíos. No se fusionan en una sola tabla. |
| **Extras incluidos** | C (subcategorías CRUD), D (renombrar/borrar cartera), E (summary global de inversiones). También A (`GET /users/me`) y B (evento de bienvenida en register). |
| **Monolito modular (Spec 0)** | Reubicación a **módulos funcionales** (Auth, Finance, Investments, **Notifications**) + **SharedKernel**, comunicación inter-módulo por **IntegrationEvents** sobre `IIntegrationEventBus` in-memory (simula RabbitMQ/SB sin serlo). Solo reubicaciones de carpeta/namespace + guard tests; **sin** partir en assemblies (futuro). |
| **Categorías → Finance (Spec 0)** | `SubCategory` (hoy hija del AR `User`) se **reubica al módulo Finance** junto con `Transaction`: la taxonomía de presupuesto es asunto de Finance, no de Identity/Auth. La decisión se cierra en la Spec 0; su CRUD (#8/C) se implementa en la Spec 3. |
| **Paginación (Spec 00)** | **Todos** los listados (Companies, Portfolios, Valuations, Holdings) exponen `page`/`pageSize` + `meta.totalCount`, reutilizando el patrón ya presente en Transactions (`PagedResult<T>` compartido). Series/summaries (p.ej. charts #9) **no** se paginan. |

---

## Backlog consolidado (11 ítems agrupados por módulo)

### Módulo Auth / User

**1. Moneda base obligatoria en Registro (#1)**
- Hoy: `RegisterCommand(Email, Password, FullName)` sin moneda; `Users.BaseCurrency` cae en
  `DEFAULT 'EUR'`. Todo usuario acaba en EUR.
- Objetivo: añadir `BaseCurrency` (enum `Currency`) al command + validator (`NotEmpty`), pasarlo
  a `User.Create(...)`. Front: selector obligatorio en el formulario de registro.
- Archivos: `Application/Commands/Auth/Register/{RegisterCommand,RegisterCommandValidator,RegisterCommandHandler}.cs`,
  `Domain/Entities/User.cs`, front `register/page.tsx` + schema zod.

**2. Update + lectura de usuario (#2 + A)**
- Hoy: no hay `UserController` ni `GET /users/me` ni `PUT`.
- Objetivo (contrato ya escrito en front Task 15): `GET /users/me → {idUser, email, fullName,
  baseCurrency, lastLoginDate}`; `PUT /users/me {fullName, password?} → UserProfile`. Command con
  re-hash Argon2 si cambia password.
- Archivos: nuevo `Controllers/UserController.cs`, `Commands/Users/UpdateUser/*`,
  `Queries/Users/GetMe/*`. Front: reemplazar mock de `profile/page.tsx`.

**3. Evento de bienvenida en Register (B)** — depende del Módulo EmailLog.
- Objetivo: `Register` dispara Domain Event → handler escribe un `EmailLog` de bienvenida.

### Módulo Contactos (entidad propia)

**4. CRUD de Contactos (#3)**
- Hoy: sin entidad ni controller; front en `localStorage` (`useContacts.ts`, `contacts/page.tsx`).
- Objetivo: AR `Contact` (fullName, email, message, fecha, IdStatus). `POST /contacts` (público),
  `GET /contacts`. Al crear un contacto → además dispara la simulación de email (EmailLog).
- Archivos: `Domain/Entities/Contact.cs`, `…Commands/Contacts/*`, `…Queries/Contacts/*`,
  `Controllers/ContactsController.cs`, migración.

### Módulo EmailLog (simulación de envíos)

**5. CRUD/log de SendEmails (#4)**
- Hoy: no existe tabla ni entidad.
- Objetivo: entidad `EmailLog` (destinatario, asunto, cuerpo/tipo, fecha, estado). Se escribe
  desde: (a) alta de contacto, (b) evento de bienvenida en register. `GET /emails` para el
  listado de la pantalla "Emails Logging".
- Archivos: `Domain/Entities/EmailLog.cs`, EventHandlers, `…Queries/Emails/GetEmails/*`,
  `Controllers/EmailsController.cs`, migración. Front: `useEmails.ts`, `emails/page.tsx`.

### Módulo Finance (ex-Finanzas)

**6. Gastos/Ingresos agrupados por categoría (#7)**
- Hoy: se agrega en el navegador (`aggregateByCategory`, Task 7). Spec §4.2 hueco #1.
- Objetivo: query Dapper `GET /transactions/by-category` (agrupa por `IdMainCategory`/
  `IdSubCategory` sobre `BaseAmount`, con `from/to` y `type` opcionales). Devuelve totales por
  categoría en moneda base.

**7. Gastos/Ingresos por mes/año filtrado por categoría + from/to (#8)**
- Hoy: `GET /transactions/monthly-chart?year=` (solo año, sin filtro de categoría ni rango).
- Objetivo: extender a `?from&to&category&type` agrupando por `YEAR, MONTH` sobre `BaseAmount`.
  Decidir si nuevo endpoint o parámetros opcionales al existente.
- Archivos (6 y 7): `Queries/Transactions/*`, `TransactionsController.cs`, DTOs; front sustituye
  el cálculo cliente por el endpoint.

### Módulo Categorías

**8. Subcategorías CRUD (C)**
- Hoy: marcado "⬜ Plan futuro" (`docs/02-backend-design.md`, sección *Categories*). El AR es
  `User` (SubCategory es hija).
- Objetivo: `POST /categories/sub` (crea vía `user.AddSubCategory(...)`), `DELETE /categories/sub/{id}`
  (soft delete). Validar unicidad nombre por usuario+MainCategory (invariante ya prevista).

### Módulo Inversiones (Companies / Valuations / Portfolios)

**9. Valuations: listado filtrado + charts por periodo (#5)**
- Hoy: `GET /companies/{id}/valuations` devuelve histórico completo sin periodo ni agregación.
- Objetivo: `GET /companies/{id}/valuations?period=3m|6m|1y|3y|5y` (o `from/to`) devolviendo la
  serie recortada + summary (p.ej. min/max/último/variación %). Serie de **precio de empresa**
  (catálogo global). Front: gráfica de cotización con selector de periodo.

**10. Holdings: campos Original en performance (#6)**
- Hoy: `HoldingPerformanceDto(OpenShares, CostBasis, MarketValue, UnrealizedPnL, UnrealizedPnLPct)`
  solo en moneda base. Front tiene el contrato pendiente en `src/frontend-web/src/types/portfolios.ts`
  (líneas ~106-122).
- Objetivo: añadir `buyOriginalCurrency, costBasisOriginal, marketValueOriginal,
  unrealizedPnLOriginal` al DTO, calculados en la query Dapper de `/performance`. Sin persistir.
  Al materializarse, el front quita los `TODO` y renderiza los valores (campos ya opcionales).

**11. Portfolio: renombrar/borrar (D) + summary global de inversiones (E)**
- Hoy: solo `POST /portfolios` (crear). `performance` es por-cartera. Spec §4.2 hueco #2.
- Objetivo D: `PUT /portfolios/{id}` (nombre), `DELETE /portfolios/{id}` (soft delete + reglas
  con holdings abiertos). Front: acciones de gestión de cartera.
- Objetivo E: `GET /portfolios/summary` (o `/performance/summary`) agregando market value / PnL /
  realizado de **todas** las carteras del usuario, para el Dashboard (hoy se sumaría en cliente).

---

## Fuera de alcance de este round (confirmado)

- **AI Scanner / RAG / chat / API keys**: todo esqueleto local (plan 012 Task 12). Se mantiene
  "Próximamente"; no se toca backend.
- **Refresh token / rastreo de sesión en BD**: la política vive en cliente (spec §6.1). No cambia.
- **Serie de valor de cartera por-usuario en el tiempo** (descartada frente a precio de empresa en #9).

---

## Plan de specs (orden de ejecución)

Este backlog se trocea en **specs + planes** por módulo (siguiendo el flujo superpowers y el
gate humano 1 tarea = 1 rama = 1 PR usado en el resto del repo). Dos specs **fundacionales**
(0 y 00) preceden a las de features para que estas nazcan ya en la estructura correcta. Los
ficheros continúan la numeración de `docs/superpowers/specs/` (último real: 004); "0/00" es solo
la etiqueta conceptual de *fase fundacional*.

| Orden | Spec (fichero) | Cubre | Nota |
|-------|----------------|-------|------|
| **0** | `005-…-backend-modular-monolith-design.md` | Reubicación a módulos (Auth/Finance/Investments/**Notifications**) + SharedKernel + `IIntegrationEventBus` (in-memory) + Domain/Integration events + guard tests (NetArchTest). Cierra **Categorías→Finance** | Fundacional; refactor estructural, primero para que el resto nazca aquí |
| **00** | `006-…-backend-paginacion-listados-design.md` | `page`/`pageSize` + `meta.totalCount` en Companies, Portfolios, Valuations (Holdings a decidir); `PagedResult<T>` compartido | Cross-cutting; después de 0 para escribirlo ya en la estructura modular |
| **1** | `007-…-backend-notifications-emails-contactos-design.md` | **#5** EmailLog + **#4** Contactos + **#3/B** welcome (Auth→Notifications vía IntegrationEvent) | Estrena el módulo Notifications y el bus de integración de la Spec 0 |
| **2** | `008-…-backend-user-registro-moneda-design.md` | **#1** moneda en Registro + **#2/A** update/lectura usuario | Módulo Auth |
| **3** | `009-…-backend-finanzas-agregaciones-design.md` | **#6** por categoría + **#7** por mes/año con filtros + **#8/C** subcategorías CRUD | Módulo Finance (Categorías ya reubicadas en Spec 0) |
| **4** | `010-…-backend-inversiones-summaries-design.md` | **#9** valuations por periodo + **#10** holdings Original + **#11/D/E** portfolio rename/delete + summary global | Módulo Investments |

---

## Verificación (para cada módulo cuando se implemente)

- **Unit**: Domain (invariantes del AR, p.ej. `Contact.Create`, `User` con moneda) + Application
  (validators, handlers) con xUnit/FluentAssertions.
- **Integración/E2E**: `WebApplicationFactory<Program>` + MySQL de docker-compose (`bigschool_test`):
  registrar con moneda ≠ EUR y comprobar `BaseCurrency`; alta de contacto → fila en `Contact` +
  `EmailLog`; register → `EmailLog` de bienvenida; `by-category` y `monthly` filtrados vs datos
  sembrados; `valuations?period=` recorta la serie; `/performance` trae campos `*Original`.
- **Frontend**: sustituir los `TODO (deuda técnica)` y los mocks de `localStorage` por los endpoints
  reales; que los E2E Playwright existentes (login, crear gasto, vender) sigan verdes.
