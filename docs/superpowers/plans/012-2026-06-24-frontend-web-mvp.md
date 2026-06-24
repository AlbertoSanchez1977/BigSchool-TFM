# Plan 012 — Frontend-Web MVP

**Fecha**: 2026-06-24
**Spec**: `docs/superpowers/specs/003-2026-06-24-frontend-web-mvp-design.md`
**Diseño visual**: `docs/03-frontend-design.md` · **Convenciones**: `src/frontend-web/AGENTS.md`
**Módulo**: frontend-web

---

## Nota para el modelo ejecutor

Este plan lo redacta Opus pero lo ejecuta otro modelo (Sonnet) **sin acceso a la conversación de
diseño**. Antes de empezar **cualquier** tarea, lee:
1. `docs/superpowers/specs/003-2026-06-24-frontend-web-mvp-design.md` (alcance y decisiones).
2. `src/frontend-web/AGENTS.md` (convenciones de código + glosario React/Next ↔ C#/.NET).
3. `docs/03-frontend-design.md` (tokens, layouts, paleta).

Reglas que no se renegocian aquí: light-first con tokens (sin colores literales), TanStack Query
para datos, react-hook-form + zod para formularios, JWT en `localStorage` + `Bearer`, y **TDD**
(unit tests primero) en la lógica de cada tarea.

---

## Convenciones de ejecución

- **Flujo**: 1 tarea = 1 rama `feature/frontend-web-mvp-taskN` = 1 PR a `develop`, con **revisión
  humana** entre tareas. Marcar `[ ]` → `[x]` según se completan.
- **TDD**: primero unit tests (Vitest + RTL) de la lógica/comportamiento (red → green); luego la
  implementación. **E2E Playwright** en los hitos marcados (tareas 3, 6, 10, 16).
- **No** testear estilos/layout. El TDD aplica a hooks, lógica pura, esquemas zod, `apiClient` y
  comportamiento (estados carga/error/vacío, handlers).
- **Backend dev**: `https://localhost:7030` o `http://localhost:5285` (por defecto el HTTP), vía
  `NEXT_PUBLIC_API_URL`. Respuestas con envelope `{ data, errors, meta }`; `errors[]` = `{ code,
  message, field? }`.
- **Explicar mientras se construye** (objetivo de aprendizaje del autor).

---

## Task 1 — Fundamentos del proyecto

**Files**: `package.json`, `vitest.config.ts`, `playwright.config.ts`, `src/test/setup.ts`,
`src/app/providers.tsx`, `src/app/layout.tsx`, `.env.local`, `.env.example`,
`src/lib/apiClient.ts`, `src/types/*.ts`, `components.json`.

- [x] **Step 1**: deps de runtime (`@tanstack/react-query`, `react-hook-form`, `zod`,
  `@hookform/resolvers`) y de test (`vitest`, `@testing-library/react`, `@testing-library/jest-dom`,
  `@testing-library/user-event`, `jsdom`, `@playwright/test`). Configurar `vitest.config.ts`
  (entorno jsdom, setup) y `playwright.config.ts` (baseURL del dev server).
- [x] **Step 2**: añadir componentes shadcn que se usarán: input, label, form, table, dialog,
  sheet, tabs, select, dropdown-menu, sonner, badge, skeleton, separator, avatar. Alinear
  `components.json` al alias `@/` (tsconfig manda).
- [x] **Step 3**: `providers.tsx` (client) con `QueryClientProvider` + `<Toaster/>` (sonner);
  montarlo en `layout.tsx` envolviendo `children`.
- [x] **Step 4**: `.env.local` + `.env.example` con
  `NEXT_PUBLIC_API_URL=http://localhost:5285/api/v1` (todos los controllers cuelgan de `/api/v1`).
- [x] **Step 5 (TDD)**: `lib/apiClient.ts`. Tests Vitest primero, casos:
  - éxito → devuelve `data` desestructurado del envelope;
  - respuesta con `errors[]` → lanza error tipado con `code`/`message`/`field`;
  - HTTP no-2xx sin envelope → error genérico;
  - inyecta `Authorization: Bearer <token>` si hay token;
  - 401 → invoca el gancho de sesión (logout) [se conecta en Task 2].
- [x] **Step 6**: `types/` — interfaces TS espejo de DTOs backend, **verificadas contra el código
  fuente** (no de memoria): `enums` (TransactionType/MainCategory/Currency como nombre string),
  `auth` (`AuthResponse{accessToken,expiresAt,email,fullName}` — sin refresh token), `categories`
  (anidadas: `Category{idMainCategory,name,subCategories[]}` + `SubCategory{...,isDefault}`),
  `transactions` (`Transaction`, `CreateTransactionDto`, `Summary`, `MonthlyChartPoint`).
  Portfolios/companies quedan **provisionales** (se verifican en Tasks 8-10).
- [x] **Step 7 (TDD)**: `lib/auth/refreshPolicy.ts` — política de sesión en cliente (pura, testeable).
  Tests primero: `canRefresh` (límite 5 refrescos / 24 h) y `shouldRefreshSoon` (margen 5 min /
  ya caducado). Constantes `MAX_REFRESHES=5`, `MAX_SESSION_HOURS=24`, `ACCESS_TOKEN_MINUTES=60`.
  (El cableado al timer + llamada `/auth/refresh` es Task 2.)
- [x] **Step 8**: verificar `pnpm build` y `pnpm dev` OK; `pnpm test` (Vitest) en verde.

**Aceptación**: build limpio; tests de `apiClient` + `refreshPolicy` verdes (15); providers activos.
**Aprenderás**: estructura Next App Router, providers, `NEXT_PUBLIC_*`, HTTP centralizado (≈ `HttpClient`
tipado), lógica pura testeable (política de refresco).

---

## Task 2 — Núcleo de autenticación

**Files**: `src/lib/auth/tokenStore.ts`, `src/lib/auth/AuthProvider.tsx`, `src/hooks/useAuth.ts`,
integración en `apiClient`. Reutiliza `lib/auth/refreshPolicy` (ya creado en Task 1).

- [x] **Step 1 (TDD)**: `tokenStore` (get/set/clear de `accessToken`, `expiresAt`, `sessionStartedAt`
  y `refreshCount` en `localStorage`). Tests: set→get, clear, persistencia de contadores.
- [x] **Step 2 (TDD)**: `AuthProvider` + `useAuth` (estado `user`/`token`; `login`, `register`,
  `logout`, `refresh`). Tests de transición (login setea token + `sessionStartedAt`; logout limpia).
- [x] **Step 3 (TDD)**: refresco proactivo — timer que ante `shouldRefreshSoon` llama a
  `POST /auth/refresh` si `canRefresh`; si no, `logout`. Incrementa `refreshCount`. Tests con timers
  simulados y `refreshPolicy`.
- [x] **Step 4**: conectar `apiClient` ↔ token (Bearer, vía `getToken`) y el gancho `onUnauthorized`
  (401) → `logout` + redirección a `/login`.

**Aceptación**: tests de store, provider y refresco verdes; `apiClient` adjunta el token real.
**Aprenderás**: Context API (≈ scope DI), hooks personalizados, ciclo de sesión y refresco proactivo.

---

## Task 3 — Páginas Login y Registro

**Files**: `src/app/(public)/login/page.tsx`, `src/app/(public)/register/page.tsx`,
`src/lib/schemas/auth.ts`, `tests/e2e/login.spec.ts`.

- [x] **Step 1 (TDD)**: esquemas zod login/registro. 10 tests: email inválido, password corto,
  confirmPassword no coincide, campos requeridos.
- [x] **Step 2**: formularios react-hook-form + zod. Dualidad Desktop/Mobile:
  - Desktop navbar: `NavbarPublic` con panel CSS-absolute flotante (top-right, 8 px de aire,
    click-outside para cerrar).
  - Móvil navbar: botones redirigen a páginas físicas `/login` y `/register`.
  - CTA: `render={<Link>}` + `nativeButton={false}` — renderiza `<a>` manteniendo estilos.
  - `app/(public)/login/page.tsx` y `register/page.tsx`: card centrada en pantalla completa.
- [x] **Step 3 (E2E — hito)**: Playwright 9 tests: apertura de paneles, switch login↔registro,
  click-outside, validación client-side, flujo registro→login con backend.
  Acceso a ruta privada se completa en Task 4. `tests/e2e` excluido de Vitest.

**Aceptación**: validación testeada; E2E de login verde.
**Aprenderás**: formularios controlados, validación tipada, errores de API en UI.

---

## Task 4 — Route groups, guard y App shell

**Files**: `src/app/(private)/layout.tsx`, `src/components/app-shell/{sidebar,topbar}.tsx`,
páginas placeholder.

- [ ] **Step 1**: separar route groups `(public)` / `(private)`.
- [ ] **Step 2 (TDD)**: guard del layout privado (sin token → redirige a `/login`). Test de comportamiento.
- [ ] **Step 3**: App shell — sidebar + topbar (bloque shadcn) con navegación, item activo y logout.
- [ ] **Step 4**: placeholders de dashboard/expenses/investments/ai-scanner para navegar.

**Aceptación**: rutas privadas redirigen sin sesión; navegación visible.
**Aprenderás**: layouts anidados, route groups, protección de rutas en cliente.

---

## Task 5 — Gastos/Ingresos: listado + filtros

**Files**: `src/services/transactionService.ts`, `src/hooks/useTransactions.ts`,
`src/app/(private)/expenses/page.tsx`.

- [ ] **Step 1 (TDD)**: service + `useTransactions` (TanStack Query) con filtros (tipo, categoría,
  from/to) y paginación. Tests de hook (loading/success/error) con `QueryClient` de test.
- [ ] **Step 2**: tabla con selector mes/año (por defecto mes actual) + paginación.
- [ ] **Step 3 (TDD)**: estados carga/vacío/error (tests de render condicional).

**Aceptación**: lista filtrable y paginada con datos reales; estados cubiertos por test.
**Aprenderás**: TanStack Query (queryKeys, cache), tablas, estados de datos.

---

## Task 6 — Gastos/Ingresos: CRUD en panel lateral

**Files**: `expenses/` (Sheet alta/edición), `src/hooks/useCategories.ts`,
`src/lib/schemas/transaction.ts`, `tests/e2e/create-expense.spec.ts`.

- [ ] **Step 1 (TDD)**: esquema zod de transacción + mutations (crear/editar/borrar) con
  invalidación de queries. Tests primero.
- [ ] **Step 2**: panel lateral (Sheet) con formulario; select de categorías (`/categories`);
  borrar con confirmación; toasts.
- [ ] **Step 3 (E2E — hito)**: Playwright crear gasto → aparece en la lista.

**Aceptación**: CRUD funcional con invalidación; E2E de creación verde.
**Aprenderás**: mutaciones, invalidación de cache, formularios de edición reutilizables.

---

## Task 7 — Gastos/Ingresos: pestaña de gráficas

**Files**: `src/lib/charts/aggregateByCategory.ts`, `src/components/charts/category-bars.tsx`,
pestaña en `expenses/`.

- [ ] **Step 1 (TDD)**: función pura `aggregateByCategory` (últimos 4 años, agrupado por categoría
  y año). Tests exhaustivos: varios años, categorías mixtas, meses sin datos, ingreso vs gasto.
  (Es el corazón del hueco #1 de la spec.)
- [ ] **Step 2**: gráfica de barras agrupadas por año (Recharts) con tokens `--chart-*`.

**Aceptación**: agregación cubierta por tests; barras por año coherentes con el design doc.
**Aprenderás**: transformar datos en cliente; separar lógica pura (testeable) de la gráfica.

---

## Task 8 — Inversiones: carteras

**Files**: `src/services/portfolioService.ts`, `src/hooks/usePortfolios.ts`,
`src/app/(private)/investments/page.tsx`.

- [ ] **Step 1 (TDD)**: service + hooks de carteras (lista + crear). Tests de hook.
- [ ] **Step 2**: lista de cards alargadas + crear en panel lateral (sin renombrar/borrar, hueco #2).
- [ ] **Step 3**: navegación a holdings de la cartera (`investments/[id]`).

**Aceptación**: listar y crear carteras con datos reales.
**Aprenderás**: navegación con parámetros de ruta, patrón card+panel reutilizable.

---

## Task 9 — Inversiones: holdings

**Files**: `src/app/(private)/investments/[id]/page.tsx`, `src/hooks/useHoldings.ts`.

- [ ] **Step 1 (TDD)**: hooks de holdings (detalle cartera, añadir, editar Notes, borrar). Tests.
- [ ] **Step 2**: cards de holdings con `OpenShares` + panel lateral para añadir / editar Notes / borrar.

**Aceptación**: alta/edición Notes/borrado de holdings con invalidación.
**Aprenderás**: vistas maestro-detalle anidadas.

---

## Task 10 — Inversiones: vender (FIFO) + performance

**Files**: `investments/[id]/` (acción vender + vista performance), `src/hooks/usePerformance.ts`,
`tests/e2e/sell-holding.spec.ts`.

- [ ] **Step 1 (TDD)**: mutation de venta (`/portfolios/{id}/sales`) + hook de performance. Tests.
- [ ] **Step 2**: acción "vender" a **nivel empresa** (FIFO); la UI debe explicar que consume
  varios lotes (hueco #3). Vista performance (KPIs realizado/no realizado/% + por holding).
- [ ] **Step 3 (E2E — hito)**: Playwright vender → realizado/holdings actualizados.

**Aceptación**: venta FIFO funcional; E2E de venta verde; performance coherente.
**Aprenderás**: alinear UX con reglas de dominio (FIFO), consolidación de métricas.

---

## Task 11 — Dashboard

**Files**: `src/app/(private)/dashboard/page.tsx`, hooks de summary/monthly-chart/performance,
`src/lib/dashboard/derive.ts`.

- [ ] **Step 1 (TDD)**: hooks de agregados + lógica de derivados (balance acumulado, tasa de
  ahorro). Tests de la lógica pura `derive`.
- [ ] **Step 2**: composición — 4 KPIs + barras ingresos/gastos + línea de balance + resumen de
  inversiones + últimas 5 transacciones.

**Aceptación**: dashboard con datos reales; lógica de derivados testeada.
**Aprenderás**: componer una vista desde varias fuentes con estados independientes.

---

## Task 12 — AI Scanner (toggle + "Próximamente")

**Files**: `src/app/(private)/ai-scanner/page.tsx`, `src/lib/config/aiScanner.ts`.

- [ ] **Step 1 (TDD)**: flag de configuración (activar/desactivar, solo local). Tests del render
  condicional.
- [ ] **Step 2**: si off → pantalla "Próximamente"; si on → esqueleto de chat (sin backend real).

**Aceptación**: toggle conmuta entre chat-esqueleto y "Próximamente".
**Aprenderás**: feature flags en cliente, render condicional.

---

## Task 13 — Contacto (SOLO FRONTEND; backend diferido)

**Files**: `src/app/(public)/contact/page.tsx`, `src/lib/schemas/contact.ts`.

- [ ] **Step 1 (TDD)**: esquema zod del formulario (nombre, email, mensaje) + tests de validación.
- [ ] **Step 2**: formulario react-hook-form; al enviar, llamada al contrato `POST /contact` (aún
  sin backend) con feedback optimista; **documentar el contrato esperado** en el propio código.

**Nota**: backend (`EmailLog`, `POST /contact`) se especifica en **otra sesión**. No será
E2E-green hasta entonces.

---

## Task 14 — Emails Logging (SOLO FRONTEND; backend diferido)

**Files**: `src/app/(private)/emails/page.tsx`, `src/hooks/useEmails.ts`.

- [ ] **Step 1 (TDD)**: hook de listado contra contrato `GET /emails` (mockeado) + estados.
- [ ] **Step 2**: pantalla de listado simple (contactos enviados + email de bienvenida) con estado vacío.

**Nota**: backend diferido (otra sesión).

---

## Task 15 — Profile / Editar usuario (SOLO FRONTEND; backend diferido)

**Files**: `src/app/(private)/profile/page.tsx`, `src/lib/schemas/profile.ts`.

- [ ] **Step 1 (TDD)**: esquema zod `Full name` + `password` + `repeat password` (coincidencia).
  Tests de validación (incluye no-coincidencia).
- [ ] **Step 2**: formulario de perfil; enlace desde la zona privada. Contrato de update
  documentado en el código.

**Nota**: backend (endpoint de update de usuario) se especifica en **otra sesión**.

---

## Task 16 — Alcance + cierre

**Files**: `src/app/(public)/scope/page.tsx`, `docs/diario.md`.

- [ ] **Step 1**: página pública "Alcance y trabajos futuros" (tabla actual vs futuro: RAG, MCP,
  Mobile, backend de emails/profile).
- [ ] **Step 2 (E2E — hito)**: smoke de flujos críticos (login, crear gasto, vender) en verde.
- [ ] **Step 3**: entrada en `docs/diario.md` del Frontend-Web MVP.

**Aprenderás**: cierre de un MVP, comunicación de alcance.

---

## Handoff

Ejecutar task-by-task con gate humano. Tareas 13–15 = **solo frontend** (backend en sesión aparte).
Hitos E2E: tareas 3, 6, 10 y 16. Cada tarea empieza por sus unit tests (TDD).
