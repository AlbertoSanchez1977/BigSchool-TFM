# Diseño: Frontend-Web — MVP

**Fecha**: 2026-06-24
**Autor**: Alberto Sánchez
**Estado**: Diseño aprobado (alimenta el plan `012-2026-06-24-frontend-web-mvp.md`)
**Módulo**: frontend-web

---

## 1. Contexto y motivación

Tras el reenfoque a MVP (`docs/01-arquitectura.md`, **ADR-008**), la entrega del TFM es
**Backend (hecho) + Frontend-Web**. El sistema de diseño y la **Landing pública** ya se generaron
con v0 y están integrados, compilando y arrancando en `src/frontend-web`.

Esta spec cierra el **alcance y las decisiones** para construir el resto de la aplicación (zona
privada + páginas públicas restantes), de forma que el trabajo sobreviva a cambios de sesión y
compactaciones de contexto. El detalle visual (tokens, layouts, paleta) vive en
`docs/03-frontend-design.md`; las convenciones de código y el glosario de aprendizaje en
`src/frontend-web/AGENTS.md`.

> **Objetivo de aprendizaje.** El autor domina C#/.NET y backend pero es principiante en frontend.
> El trabajo se hace **explicando el porqué** de cada paso, con analogías a C#/.NET. Por eso la
> cadencia es de **pasos pequeños** (ver §6).

---

## 2. Decisiones de diseño (acordadas)

| Decisión | Elección | Implicación |
|---|---|---|
| Cadencia de trabajo | **Pasos pequeños + gate humano** (1 tarea = 1 rama = 1 PR) | Igual que backend; tareas pequeñas y muy explicadas para aprender |
| Almacenamiento de sesión | **JWT en `localStorage` + cabecera `Authorization: Bearer`** | Simple; encaja con el backend actual (CORS + Bearer). Sin capa server extra |
| Refresco de token | **Proactivo en cliente** (antes de caducar) con política propia: máx. **5 refrescos** o **24 h** de sesión → re-login | El backend no tiene refresh token ni rastreo en BD; `POST /api/v1/auth/refresh` es `[Authorize]` y exige token aún válido (ver §6.1) |
| Metodología de tests | **TDD con unit tests en cada tarea** (Vitest + RTL) **+ E2E Playwright en hitos** | Se testea lógica y comportamiento, no estilos/layout (ver §6) |
| Generación de UI | **v0 solo para diseño + Landing**; resto con shadcn (gratis) + código | Maximiza el plan Free de v0; ver §5 |
| Tema | **Light-first**, tokens preparados para oscuro (ya cableado por v0) | El toggle de oscuro se decide al final |
| Estado servidor | **TanStack Query** | Cache, estados de carga/error, invalidación |
| Formularios | **react-hook-form + zod** | Validación tipada declarativa (testeable en aislado) |
| Piezas con backend nuevo | **Solo alcance frontend en este ciclo** (Emails, Update usuario) | El backend de ambas se especifica en **otra sesión** (ver §4.3) |

---

## 3. Estado de partida (lo que dejó v0)

- **Stack**: Next 16 (App Router), React 19, **Tailwind v4** (config por CSS, sin
  `tailwind.config.ts`), **shadcn v4** (estilo `base-nova`), pnpm. Alias `@/*` → `./src/*`.
- **Sistema de diseño** completo en `src/app/globals.css`: tokens claro + oscuro (los nuestros +
  `sidebar`/`secondary`/`accent`/`destructive`/`popover` que añadió v0). Modo oscuro ya cableado.
- **Landing** pública con charts *fake*. Componentes shadcn presentes: solo `button` y `card`.
- **Falta** para la app: TanStack Query, react-hook-form + zod, capa auth/JWT, `apiClient`
  (fetch + Bearer + URL del backend por env), tipos de DTOs, más componentes shadcn y el
  utillaje de tests (Vitest + RTL + Playwright).
- Backend de desarrollo en `https://localhost:7030` (HTTPS) o `http://localhost:5285` (HTTP).
  Para el MVP usaremos **`http://localhost:5285`** por defecto (evita el certificado de dev), salvo
  que CORS exija el HTTPS; configurable vía `NEXT_PUBLIC_API_URL`.
- **Todos los controllers cuelgan de `/api/v1`** → `NEXT_PUBLIC_API_URL=http://localhost:5285/api/v1`.
- **Los enums viajan como nombre string** (`JsonStringEnumConverter`): `TransactionType`
  (`"Income"`/`"Expense"`), `MainCategory` (`"EssentialExpenses"`…), `Currency` (`"EUR"`…).
- **Categorías anidadas**: `CategoryDto{ idMainCategory, name, subCategories[] }` +
  `SubCategoryDto{ idSubCategory, name, isDefault }` → combos anidados en los formularios de transacción.

> **Norma de contratos.** Los tipos TS son espejo de los DTOs del backend (mismo monorepo). Se
> **verifican contra el código fuente del backend al inicio de la tarea que los consume**, no de
> memoria. En la Task 1 se fijaron auth/categories/transactions; portfolios/companies se verifican
> en las Tasks 8-10.

---

## 4. Alcance del MVP

### 4.1 Páginas y accesos

| Página | Acceso | Endpoints backend | Notas |
|--------|--------|-------------------|-------|
| Landing | Público | — (fake) | ✅ Hecha con v0 |
| Login / Registro | Público | `/auth/login`, `/auth/register` | JWT real |
| Contacto | Público | _(diferido)_ | Solo frontend en este ciclo (§4.3) |
| Alcance y trabajos futuros | Público | — | Estática, al final |
| Dashboard | Privado | `/transactions/summary`, `/transactions/monthly-chart`, `/portfolios/{id}/performance` | Compone KPIs + gráficas |
| Gastos/Ingresos | Privado | `/transactions` (CRUD, filtros, paginación), `/categories` | Master-detail + pestaña gráficas |
| Inversiones | Privado | `/portfolios`, `/portfolios/{id}`, holdings, `/sales`, `/performance`, `/companies` | Carteras → holdings → vender FIFO |
| AI Scanner | Privado | — | Toggle local + "Próximamente" |
| Emails Logging | Privado | _(diferido)_ | Solo frontend en este ciclo (§4.3) |
| Profile / Editar usuario | Privado | _(diferido)_ | Solo frontend en este ciclo (§4.3) |

### 4.2 Huecos del Backend (atajos aceptados)

1. **Sin agregación por categoría** → la gráfica "por categoría (últimos 4 años)" se calcula
   **en el navegador** desde la lista de transacciones.
2. **Sin renombrar/borrar cartera** → en el MVP la cartera solo se **crea** (luego se gestionan
   sus holdings).
3. **La venta es FIFO a nivel empresa** → `POST /portfolios/{id}/sales` vende acciones de *una
   empresa* y consume lotes (holdings) en orden FIFO. El botón "vender" de un holding debe
   comunicarlo (puede tocar varios lotes).

### 4.3 Piezas con backend diferido (solo frontend en este ciclo)

Estas piezas necesitan **backend nuevo que se especificará en otra sesión**. En este plan se
construye **solo el frontend** contra el contrato acordado; no quedarán *E2E-green* hasta que
exista el backend.

- **Emails simulados** (NOTA HUMANA del AGENTS.md):
  - *(Backend futuro)*: tabla `EmailLog`; `POST /contact` (simula envío y persiste); evento de
    dominio en `/auth/register` que persiste un email de bienvenida; `GET /emails` para el listado.
  - *(Frontend ahora)*: formulario de **Contacto** (público) + pantalla **Emails Logging**
    (privada) que lista contactos + bienvenida.
- **Update de usuario (Punto 10)**:
  - *(Backend futuro)*: endpoint de actualización de usuario.
  - *(Frontend ahora)*: pantalla **Profile** con `Full name`, `password`, `repeat password`
    (validación de coincidencia). Enlazada desde registro/login y zona privada.

---

## 5. Flujo de trabajo: tres capas (recordatorio)

| Capa | Herramienta | Qué produce | Coste |
|------|-------------|-------------|-------|
| A. Diseño + Landing | v0 | Theme/tokens + Landing | Prompts v0 (ya gastado) |
| B. "Muebles" | shadcn/ui blocks | Login, sidebar, layout, tablas, formularios | Gratis |
| C. Lógica | Código (manual, explicado) | apiClient + JWT, datos reales, gráficas, toggles, routing/guards | Tiempo |

---

## 6. Cadencia y metodología de tests (TDD)

- **1 tarea = 1 rama (`feature/frontend-web-mvp-taskN`) = 1 PR**, con **revisión humana** entre tareas.
- Tareas **pequeñas** y **explicadas** (objetivo de aprendizaje).
- **TDD en cada tarea**: para la lógica y el comportamiento se escriben primero los unit tests
  (red) y luego la implementación (green):
  - **Vitest + React Testing Library** para: hooks (`useTransactions`…), lógica de datos (p. ej.
    la **agregación por categoría** en cliente), esquemas de validación (zod), `apiClient`, y
    comportamiento de componentes (estados de **carga/error/vacío**, handlers, render condicional).
  - **Playwright E2E** en flujos críticos en los hitos: **login**, **crear gasto**, **vender holding**.
- **No** se testean estilos/layout ni la implementación interna (coherente con `AGENTS.md`).
  El TDD aplica a la lógica testeable, no al pixel.

### 6.1 Política de sesión y refresco (cliente)

El backend (`AuthController`) expone `POST /api/v1/auth/refresh` con `[Authorize]`: recibe el
access token **aún válido** en la cabecera y devuelve un `AuthResponseDto` nuevo
(`accessToken`, `expiresAt`, `email`, `fullName`). **No hay refresh token ni rastreo de tokens en
BD**, y el access token dura ~**60 min** (`JwtSettings.ExpirationMinutes`).

Estrategia en cliente (sin estado en servidor):
- **Refresco proactivo**: renovar cuando falten ≤ 5 min para `expiresAt` (`shouldRefreshSoon`).
  Reactivo-sobre-401 no sirve: si el token ya caducó, refresh también daría 401.
- **Límite de sesión**: como mucho **5 refrescos** (`MAX_REFRESHES`) **o** **24 h** desde el login
  (`MAX_SESSION_HOURS`); superado cualquiera → forzar re-login (`canRefresh`).
- **Reparto**: la política pura (`lib/auth/refreshPolicy`) se implementa con TDD en la **Task 1**;
  su cableado al timer + llamada real a `/auth/refresh` vive en el `AuthProvider` de la **Task 2**.

---

## 7. Referencias

- Reenfoque y decisión global: `docs/01-arquitectura.md` (ADR-008).
- Sistema de diseño (tokens, layouts, paleta): `docs/03-frontend-design.md`.
- Prompt de v0 (histórico): `docs/03-frontend-v0-prompt.md`.
- Convenciones de código + glosario React/Next ↔ C#/.NET: `src/frontend-web/AGENTS.md`.
- Plan de ejecución: `docs/superpowers/plans/012-2026-06-24-frontend-web-mvp.md`.
