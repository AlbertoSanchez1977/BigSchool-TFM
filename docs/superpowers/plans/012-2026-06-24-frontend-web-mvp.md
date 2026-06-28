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

### Regla de huecos de backend (deuda técnica) — aplica de Task 11 en adelante

El backend tiene **deuda técnica conocida** que se abordará en **otra sesión** al cerrar este plan
de frontend. Mientras tanto, cuando un dato que la UI necesita **no** lo expone hoy el backend:

1. **NO inventes el dato cruzando fuentes** (p. ej. recalcular conversiones de divisa a partir de
   tipos de cambio históricos, derivar a mano agregados que debería dar un endpoint, sumar listas
   paginadas para fingir un total, etc.). Esos cruces son frágiles y ocultan el hueco real.
2. **Explicita el hueco en el código**: marca el campo/sección con un `TODO (deuda técnica backend)`
   y, si tienes el tipo, deja el campo **opcional** (`?`) en la interfaz TS con un comentario que
   apunte al DTO/endpoint C# que debería materializarlo. Renderiza un placeholder neutro (`—`,
   "Próximamente", o skeleton) en lugar de un valor aproximado.
3. **Prioriza pasar por el backend**: el camino correcto es que el endpoint devuelva el dato ya
   calculado. Deja el frontend **preparado** para consumirlo (campo opcional + render condicional)
   de modo que cuando el backend lo materialice, **no haga falta tocar la UI**.
4. **Documenta lo que necesitas del backend**: en el PR (o en una nota junto al `TODO`) describe el
   contrato esperado — endpoint, campos, tipos, en qué moneda/unidad — para la sesión de backend.
5. **Si hay duda razonable, PREGUNTA** antes de implementar un apaño. Mejor un hueco explícito y una
   pregunta que un dato "mágico" que parezca correcto y no lo sea.

Precedente ya aplicado: `HoldingPerformance` (Task 10) lleva campos `*Original` opcionales con
`TODO (deuda técnica)` y la UI muestra `—` hasta que el endpoint `/performance` los retorne.

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

## Task 4 — Route groups, guard, App shell y ProfileDropdown compartido

**Files**: `src/app/(private)/layout.tsx`, `src/components/app-shell/{sidebar,topbar}.tsx`,
`src/components/auth/profile-dropdown.tsx`, `src/components/navbar-private.tsx`,
ajustes en `src/components/navbar-public.tsx`, páginas placeholder.

- [x] **Step 1**: separar route groups `(public)` / `(private)`.
- [x] **Step 2 (TDD)**: guard del layout privado (sin token → redirige a `/`). Test de comportamiento.
- [x] **Step 3 (TDD)**: `ProfileDropdown` — componente **compartido** entre navbar público y privado.
  Muestra nombre/email del usuario, enlace "Editar perfil" (→ `/profile`, Task 15) y "Cerrar sesión".
  Usa `dropdown-menu` (shadcn) + `avatar`. Tests: render con `user`, acción logout, item editar.
- [x] **Step 4**: `NavbarPrivate` — topbar horizontal con logo→`/`, links de sección con item activo
  (usePathname) y `ProfileDropdown` en zona derecha. Sin sidebar lateral (se añade si escalan opciones).
- [x] **Step 5**: adaptar `NavbarPublic` al estado autenticado (sustituye la corrección mínima de
  Task 3): si `user` → ocultar login/registro, mostrar acceso destacado a Dashboard +
  `ProfileDropdown`; si no → login/registro como hasta ahora.
- [x] **Step 6**: placeholders de dashboard/expenses/investments/ai-scanner para navegar.

**Nota (decisión de Task 3)**: en Task 3 se añadió una corrección mínima en `NavbarPublic`
(si hay sesión → botón "Ir al Dashboard" + logout, sin dropdown). El `ProfileDropdown` completo
y su uso en ambos navbars se materializa aquí (Steps 3 y 5).

**Aceptación**: rutas privadas redirigen sin sesión; navegación visible; `ProfileDropdown`
funciona en navbar público y privado.
**Aprenderás**: layouts anidados, route groups, protección de rutas en cliente.

---

## Task 5 — Gastos/Ingresos: listado + filtros

**Files**: `src/services/transactionService.ts`, `src/hooks/useTransactions.ts`,
`src/app/(private)/expenses/page.tsx`.

- [x] **Step 1 (TDD)**: service + `useTransactions` (TanStack Query) con filtros (tipo, categoría,
  from/to) y paginación. Tests de hook (loading/success/error) con `QueryClient` de test.
- [x] **Step 2**: tabla con selector mes/año (por defecto mes actual) + paginación.
- [x] **Step 3 (TDD)**: estados carga/vacío/error (tests de render condicional).

**Aceptación**: lista filtrable y paginada con datos reales; estados cubiertos por test.
**Aprenderás**: TanStack Query (queryKeys, cache), tablas, estados de datos.

---

## Task 6 — Gastos/Ingresos: CRUD en panel lateral

**Files**: `expenses/` (Sheet alta/edición), `src/hooks/useCategories.ts`,
`src/lib/schemas/transaction.ts`, `tests/e2e/create-expense.spec.ts`.

- [x] **Step 1 (TDD)**: esquema zod de transacción + mutations (crear/editar/borrar) con
  invalidación de queries. Tests primero.
- [x] **Step 2**: panel lateral (Sheet) con formulario; select de categorías (`/categories`);
  borrar con confirmación; toasts.
- [x] **Step 3 (E2E — hito)**: Playwright crear gasto → aparece en la lista.

**Aceptación**: CRUD funcional con invalidación; E2E de creación verde.
**Aprenderás**: mutaciones, invalidación de cache, formularios de edición reutilizables.

---

## Task 7 — Gastos/Ingresos: pestaña de gráficas

**Files**: `src/lib/charts/aggregateByCategory.ts`, `src/components/charts/category-bars.tsx`,
pestaña en `expenses/`.

- [x] **Step 1 (TDD)**: función pura `aggregateByCategory` (últimos 4 años, agrupado por categoría
  y año). Tests exhaustivos: varios años, categorías mixtas, meses sin datos, ingreso vs gasto.
  (Es el corazón del hueco #1 de la spec.)
- [x] **Step 2**: gráfica de barras agrupadas por año (Recharts) con tokens `--chart-*`.

**Aceptación**: agregación cubierta por tests; barras por año coherentes con el design doc.
**Aprenderás**: transformar datos en cliente; separar lógica pura (testeable) de la gráfica.

---

## Task 8 — Inversiones: carteras

**Files**: `src/services/portfolioService.ts`, `src/hooks/usePortfolios.ts`,
`src/app/(private)/investments/page.tsx`.

- [x] **Step 1 (TDD)**: service + hooks de carteras (lista + crear). Tests de hook.
- [x] **Step 2**: lista de cards alargadas + crear en **modal centrado** (Dialog; no panel lateral — decisión 2026-06-27).
- [x] **Step 3**: navegación a holdings de la cartera (`investments/[id]`).

**Aceptación**: listar y crear carteras con datos reales.
**Aprenderás**: navegación con parámetros de ruta, patrón card+panel reutilizable.

---

## Task 9 — Inversiones: holdings

**Files**: `src/app/(private)/investments/[id]/page.tsx`, `src/hooks/useHoldings.ts`.

- [x] **Step 1 (TDD)**: hooks de holdings (detalle cartera, añadir, editar Notes, borrar). Tests.
- [x] **Step 2**: cabecera con **breadcrumb-header** (`← Inversiones / [nombre cartera]`) +
  cards de holdings con `openShares` + **modal centrado** (Dialog, no panel lateral) para añadir,
  editar Notes y borrar (con confirmación inline).

  **Patrón de navegación maestro-detalle** (aplica aquí y a cualquier vista de detalle futura):
  - La vista de detalle incluye siempre un `BackButton` (`← [Nombre de la lista padre]`) que
    enlaza con `<Link href="/investments">` (no `router.back()`, que falla con acceso directo).
  - Se muestra el nombre de la entidad padre como subtítulo/breadcrumb junto al botón de vuelta.
  - Formato: `[←] Inversiones  ·  [nombre cartera]` en la cabecera, con la flecha como enlace y
    el nombre de la cartera como `h1`.

**Aceptación**: alta/edición Notes/borrado de holdings con invalidación; breadcrumb-header funcional.
**Aprenderás**: vistas maestro-detalle anidadas, patrón de navegación de vuelta robusto.

---

## Task 10 — Inversiones: vender (FIFO) + performance

**Files**: `investments/[id]/` (acción vender + vista performance), `src/hooks/usePerformance.ts`,
`tests/e2e/sell-holding.spec.ts`.

- [x] **Step 1 (TDD)**: mutation de venta (`/portfolios/{id}/sales`) + hook de performance. Tests.
- [x] **Step 2**: acción "vender" en **modal centrado** (Dialog) a **nivel empresa** (FIFO);
  el modal debe explicar que puede consumir varios lotes (hueco #3). Vista performance
  (KPIs realizado/no realizado/% + tabla por holding). El breadcrumb-header de la Task 9
  permanece — no hace falta nueva navegación.
- [x] **Step 3 (E2E — hito)**: Playwright vender → realizado/holdings actualizados.

**Aceptación**: venta FIFO funcional; E2E de venta verde; performance coherente.
**Aprenderás**: alinear UX con reglas de dominio (FIFO), consolidación de métricas.

---

## Task 11 — Dashboard

**Files**: `src/app/(private)/dashboard/page.tsx`, hooks de summary/monthly-chart/performance,
`src/lib/dashboard/derive.ts`.

> ⚠️ **Aplica la _Regla de huecos de backend_** (ver "Convenciones de ejecución"). El dashboard
> compone datos de varias fuentes; **antes de cruzar datos a mano para rellenar un KPI**, comprueba
> qué expone realmente cada endpoint (`/transactions/summary`, `/transactions/monthly-chart`,
> `/portfolios`...). Lo que esos endpoints **ya** devuelven → úsalo. Lo que **no** → no lo inventes:
> marca `TODO (deuda técnica backend)`, deja el campo opcional, renderiza placeholder y documenta el
> contrato esperado para la sesión de backend. La lógica pura de `derive.ts` solo debe combinar
> datos **ya provistos** por el backend (p. ej. balance acumulado a partir de la serie mensual que
> sí existe), nunca fabricar magnitudes que el backend debería calcular. Ante la duda, **pregunta**.

- [x] **Step 1 (TDD)**: hooks de agregados + lógica de derivados (balance acumulado, tasa de
  ahorro). Tests de la lógica pura `derive`.
- [x] **Step 2**: composición — 4 KPIs + barras ingresos/gastos + línea de balance + resumen de
  inversiones + últimas 5 transacciones.

**Aceptación**: dashboard con datos reales; lógica de derivados testeada.
**Aprenderás**: componer una vista desde varias fuentes con estados independientes.

---

## Task 12 — AI Scanner (toggle + chat-esqueleto + RAG + config de keys)

**Files**: `src/app/(private)/ai-scanner/page.tsx`, `src/lib/config/aiScanner.ts`,
`src/app/(private)/ai-scanner/settings/page.tsx` (config de keys),
componentes de soporte en `src/components/ai-scanner/` (chat, file-panel, llm-selector).

> ⚠️ **Aplica la _Regla de huecos de backend_** (ver "Convenciones de ejecución"). **No existe
> backend de IA** (ni chat, ni RAG, ni almacén de keys). **Todo es esqueleto/local-only**: el chat
> no llama a ningún LLM, la subida de ficheros no persiste en ningún RAG real, y las API keys se
> guardan **solo en cliente** (estado local / `localStorage`), nunca se envían a backend. Marca cada
> punto de integración futura con `TODO (deuda técnica backend)` y **documenta el contrato esperado**
> (endpoints, payloads) en el propio código. No inventes respuestas "mágicas" del LLM: usa mensajes
> placeholder claros ("Demo — sin backend de IA conectado"). Ante la duda, **pregunta**.

### Estructura de la pantalla (cuando el toggle está ON)

Layout de dos columnas (responsive: en móvil la columna derecha colapsa debajo o en un Sheet):

1. **Columna principal (izquierda) — Chat**:
   - **Selector de LLM** arriba: dropdown para escoger entre los modelos disponibles (lista
     **local/estática** de momento: p. ej. Claude, GPT, Gemini — sin validar conexión real).
   - **Hilo de conversación**: mensajes usuario/asistente (esqueleto). El "envío" añade el mensaje
     del usuario y responde con un placeholder fijo. `TODO (deuda técnica backend)`: contrato
     esperado `POST /ai/chat { model, messages[], ragFileIds[] } → { reply }`.
   - **Caja de input** + botón enviar.
   - **Botón "Configuración"** (icono engranaje) que navega a la sub-pantalla de keys (punto 4).

2. **Columna derecha — Panel de ficheros (RAG)**:
   - **Zona de subida** (drag-and-drop o botón "Subir fichero") — solo registra el fichero en
     estado local; **no** sube a ningún sitio. `TODO (deuda técnica backend)`: contrato esperado
     `POST /ai/rag/files (multipart) → { id, name, size, status }`.
   - **Listado de ficheros subidos al RAG**: nombre, tamaño, estado (p. ej. "Pendiente de indexar"
     como placeholder) y acción de borrar (solo del estado local). Estado vacío con CTA.
     `TODO`: `GET /ai/rag/files`, `DELETE /ai/rag/files/{id}`.

3. **Toggle activar/desactivar** (feature flag local, por coste): si **OFF** → pantalla
   **"Próximamente"**; si **ON** → el layout de chat + panel RAG descrito arriba.

4. **Sub-pantalla de Configuración de keys** (`ai-scanner/settings`):
   - Formulario para introducir las **API keys** de los proveedores de LLM (Claude, OpenAI,
     Gemini…). Persistencia **solo en cliente** (`localStorage`), con aviso de que es local y
     no seguro para producción. `TODO (deuda técnica backend)`: contrato esperado
     `PUT /ai/keys { provider, key }` (cifrado en servidor) — **nunca** mandar keys en claro al
     backend en la versión real; aquí es solo demo local.
   - Botón de volver al chat.

- [x] **Step 1 (TDD)**: flag de configuración (activar/desactivar, solo local) + esquema/estado del
  selector de LLM + esquema zod del formulario de keys. Tests de: render condicional del toggle,
  cambio de modelo seleccionado, validación del formulario de keys, añadir/borrar fichero del
  estado local del panel RAG.
- [x] **Step 2**: composición de la pantalla — si OFF → "Próximamente"; si ON → chat-esqueleto con
  selector de LLM + panel RAG (subida + listado local) + botón a Configuración. Sub-pantalla de
  keys con persistencia local. Todos los puntos de integración con `TODO (deuda técnica backend)`
  y contrato documentado inline.

**Aceptación**: toggle conmuta entre "Próximamente" y la pantalla completa; selector de LLM
funcional (cambia el modelo activo); subir/listar/borrar ficheros opera sobre estado local;
formulario de keys valida y persiste en `localStorage`; navegación chat ↔ configuración. Sin
backend real: respuestas y persistencia son placeholders documentados.
**Aprenderás**: feature flags en cliente, render condicional, layout de dos columnas responsive,
manejo de subida de ficheros en cliente, persistencia local (`localStorage`), y cómo dejar
**preparados** los puntos de integración de backend sin fabricar comportamiento inexistente.

---

## Task 13 — Contacto (SOLO FRONTEND; backend diferido)

**Files**: `src/lib/schemas/contact.ts`, `src/hooks/useContacts.ts`,
`src/components/sections/contact-form.tsx` (incrustado en la sección CTA de la landing),
`src/app/(private)/contacts/page.tsx`. Acceso al listado desde `ProfileDropdown` → "Contacto".

- [x] **Step 1 (TDD)**: esquema zod (fullName, email, message) + tests. Hook `useContacts` que
  lee desde `localStorage` + tests.
- [x] **Step 2**: formulario react-hook-form en sección CTA de la landing (`sections/cta.tsx`);
  al enviar, guarda en `localStorage` y muestra feedback. Listado privado `/contacts` con tabla
  de envíos. Link "Contacto" en `ProfileDropdown`.
  `TODO (deuda técnica backend)`: `POST /contact { fullName, email, message }` (guarda en BD,
  dispara email simulado); `GET /contacts → ContactSubmission[]`.

**Nota**: backend (`EmailLog`, `POST /contact`) se especifica en **otra sesión**. El `localStorage`
permite demo funcional hasta entonces.

---

## Task 14 — Emails Logging (SOLO FRONTEND; backend diferido)

**Files**: `src/app/(private)/emails/page.tsx`, `src/hooks/useEmails.ts`.

- [x] **Step 1 (TDD)**: hook de listado contra contrato `GET /emails` (mockeado) + estados.
- [x] **Step 2**: pantalla de listado simple (contactos enviados + email de bienvenida) con estado vacío.

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
