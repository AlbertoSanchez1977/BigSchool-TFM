# BigSchool-TFM — Frontend-Web Iteración 2: integración con el Backend

**Fecha**: 2026-07-06
**Estado**: Diseño aprobado
**Relación**: cierra la deuda técnica de backend (`docs/04-backend-tech-debt.md`, specs 006–010 /
planes 019–023) desde el lado del Frontend-Web. Continúa el MVP
(`docs/superpowers/specs/003-2026-06-24-frontend-web-mvp-design.md`,
plan `012-2026-06-24-frontend-web-mvp.md`).

---

## 1. Contexto y alcance

El Frontend-Web MVP dejó **costuras aisladas** (`localStorage`, cálculo en navegador, campos
`TODO (deuda técnica)`, contratos futuros documentados) allí donde el backend aún no ofrecía la
capacidad. Esa deuda de backend **ya está resuelta** (specs 006–010). Esta iteración **cablea el
frontend a lo que el backend ya expone** y añade las pantallas nuevas que se desprenden de ello.

Es **una sola iteración cohesionada** ("el frontend se pone al día con el backend"), no varios
subsistemas independientes. Se materializa como **un plan con ~9 tareas ordenadas** por prioridad,
siguiendo el gate del repo: **1 tarea = 1 rama `feature/…-taskN` = 1 PR = revisión humana**.

**Fuera de alcance** (se mantiene como en el MVP): AI Scanner / RAG / chat (esqueleto local,
"Próximamente"); refresh-token real; serie de valor de cartera por-usuario en el tiempo (descartada
frente a la serie de precio de empresa).

---

## 2. Principios de ejecución

1. **Reutilizar los patrones ya montados**, no reescribir arquitectura: hooks TanStack Query +
   `services/` por recurso + `types/` espejo del DTO real + **modal centrado** para altas/acciones +
   patrón de **paginación de Transactions** (`expenses/page.tsx`: indicador "X–Y de Z" +
   Anterior/Siguiente sobre `meta.totalPages`).
2. **Norma de tipos (obligatoria).** Al empezar **cada** tarea se verifica el `types/*` afectado
   **contra el DTO/Controller real** del backend en el monorepo (nunca de memoria). Ver
   `src/frontend-web/AGENTS.md` → "Integración con el Backend".
3. **Estados carga / error / vacío** en cada vista; tokens semánticos del design system
   (verde/rojo solo para signo de dinero, azul acción, línea `--chart-line`).
4. **Aprendizaje**: el desarrollador es experto backend y principiante frontend → explicar el
   *porqué* de cada paso y apoyarse en analogías C#/.NET (norma del AGENTS.md del módulo).
5. **Testing**: Vitest + RTL en hooks/componentes nuevos; los E2E Playwright existentes (login,
   crear gasto, vender holding) deben seguir **verdes** al cerrar cada tarea.

---

## 3. Mapa trabajo ↔ endpoints reales (verificados en los controllers)

| # | Trabajo frontend | Endpoints backend |
|---|---|---|
| T1 | Contactos + Emails Logging | `POST /contacts` (público), `GET /contacts`, `GET /emails`, `GET /emails/{id}` |
| T2 | Gráficas de agregado en Finance | `GET /transactions/by-category?from&to&type`, `GET /transactions/monthly?from&to&category&type` |
| T3a | Moneda en registro | `POST /auth/register` (+`baseCurrency`) |
| T3b | Perfil (leer/editar) | `GET /users/me`, `PUT /users/me` |
| T4 | Paginación de listados | `page/pageSize` + `meta.totalCount` (patrón compartido) |
| T5 | Renombrar / borrar cartera | `PUT /portfolios/{id}`, `DELETE /portfolios/{id}` |
| T6 | Holdings `*Original` + summary global | `GET /portfolios/{id}/performance` (campos `*Original`), `GET /portfolios/summary` |
| T7 | Pantallas Companies ("Mercado") | `GET /companies`, `GET /companies/{id}`, `POST /companies` |
| T8 | Valuations + serie | `GET /companies/{id}/valuations`, `POST /companies/{id}/valuations`, `GET /companies/{id}/valuations/series?period=` |

---

## 4. Decisiones de diseño acordadas

- **Navegación de "Mercado"**: las pantallas de Companies/Valuations (catálogo **global**, sin
  `IdUser`) viven en una **entrada de nav propia** ("Mercado", 5º ítem de `navbar-private`), en ruta
  `/market`. El detalle de empresa (`/market/[id]`) contiene la serie de cotización, el listado de
  valoraciones y el alta de valoración.
- **Contactos y Emails** (vistas de tipo administración) se acceden desde el **ProfileDropdown**
  (sección Administración), no desde el nav principal. Hoy sus páginas existen bajo `(private)` pero
  están huérfanas.
- **Perfil**: se saca de "fuera de alcance MVP". `profile/page.tsx` deja de ser mock. **Dos PRs**
  adyacentes: T3a (moneda en registro) y T3b (perfil leer/editar).
- **Gráficas de Finance = 2 gráficas nativas** sobre una **ventana de 4 años** (año actual + 3
  anteriores, máx 4), cargada por defecto, con selector de año de referencia (máx = año actual) que
  desplaza la ventana:
  - **Gráfica A — "entre años/categoría"**: `by-category` **una llamada por año** (4) → barras
    **agrupadas por año × MainCategory**. La dimensión de año se obtiene con las 4 llamadas (el
    endpoint no la trae). Sustituye la agregación en cliente del MVP.
  - **Gráfica B — "mes/año"**: `monthly` filtrado **solo por Tipo** (Gastos/Ingresos) →
    **N llamadas, una por cada MainCategory del tipo** (7 gasto / 4 ingreso; el conjunto "categorías
    de ese tipo" se deriva del rango del enum: 1–7 gasto, 10–13 ingreso). Visualización: **barras
    apiladas por mes** (una pila = un mes, segmentos = categorías). Permite comparar meses entre años.
  - **Carga lazy**: las queries se disparan al abrir la pestaña de gráficas (`enabled`, como el
    `useCategoryChart` actual); React Query cachea cada serie.
- **Portfolio renombrar/borrar**: acciones en la **cabecera de la vista de detalle**
  (`investments/[id]`), no en la card del listado. `DELETE` respeta los **guards fiscales** del
  backend (no borrar con holdings abiertos) → el front maneja el 409/error con mensaje claro.
- **Holdings `*Original`**: son **exactamente** la única deuda ya "pre-renderizada" — el
  `PerformanceTab` ya pinta `costBasisOriginal`/`unrealizedPnLOriginal` con fallback `—`. La tarea
  materializa: verificar que el endpoint los envía, quitar el `TODO` de `types/portfolios.ts`, marcar
  los campos como presentes y añadir `marketValueOriginal`.
- **Summary global de inversiones**: `GET /portfolios/summary` alimenta el **mini-resumen del
  Dashboard** (valor mercado / realizado / no realizado / % total de **todas** las carteras),
  sustituyendo cualquier suma en cliente.
- **Excepción de paginación (holdings)**: los holdings dentro de `GET /portfolios/{id}` vienen
  **completos, sin paginar**. Es la **única excepción** aceptada en listados: el detalle de cartera y
  su performance necesitan todos los holdings juntos.
- **Selector de empresa (añadir holding) = combobox con typeahead**: 10 resultados iniciales;
  escribes y filtra. Fuente **ahora**: `GET /companies?pageSize=100` (el máximo) filtrado **en
  cliente**. Requiere un componente combobox nuevo (shadcn Command/Popover; hoy solo hay `Select`).

---

## 5. Detalle por tarea

### T1 — Contactos + Emails Logging (módulo Notifications)
- **Formulario de contacto público** (`components/sections/contact-form.tsx`): de simulado a
  `POST /contacts` real (sin JWT). Estados carga/error/éxito.
- **`useContacts`**: de `localStorage` a `GET /contacts` (privado) vía TanStack Query +
  `services/contactService`. Se retira `loadContacts`/`saveContact` de `localStorage`.
- **`useEmails`**: de mock derivado a `GET /emails` real (welcome + acuses de contacto). Se retira la
  derivación en cliente.
- **Navegación**: enganchar "Contactos" y "Emails" en el `ProfileDropdown`.
- **Tipos nuevos**: `types/contacts.ts`, `types/emails.ts` (espejo de `ContactDto`/`EmailLogDto`).
- **Deuda que queda**: ninguna.

### T2 — Finance: 2 gráficas nativas (módulo Finance)
- Implementar Gráfica A y Gráfica B según §4. Nuevos hooks (`useCategoryTotals`, `useMonthlySeries`)
  y wrappers de Recharts; adaptar/retirar `components/charts/category-bars.tsx`.
- `lib/charts/aggregateByCategory.ts` y `useCategoryChart` (agregación en cliente) se retiran o se
  reducen a utilidad de test.
- **Deuda que queda**: ninguna (la dimensión de año se resuelve con las 4 llamadas).

### T3a — Moneda en registro (módulo Auth)
- Selector obligatorio de `Currency` en `register-form.tsx` + schema Zod (`NotEmpty`), enviado en el
  body de `POST /auth/register`. Actualizar `lib/schemas/auth.ts`.

### T3b — Perfil (módulo Auth)
- `profile/page.tsx` a datos reales: `GET /users/me` (email, fullName, baseCurrency, lastLoginDate) +
  `PUT /users/me` (editar fullName, cambio opcional de password) con modal centrado/inline.
- **Tipos**: `types/users.ts` (`UserProfileDto`); actualizar `lib/schemas/profile.ts`.

### T4 — Paginación de listados (transversal)
- Extraer el patrón de paginación de `expenses/page.tsx` a un `components/ui/pagination.tsx`
  reutilizable.
- Aplicarlo a la **lista de carteras** (`usePortfolios`). En **Companies/Valuations** la paginación
  se hornea dentro de T7/T8 (sus listas nacen paginadas).
- No afecta a los holdings (ver excepción §4).

### T5 — Renombrar / borrar cartera (módulo Investments)
- **Renombrar**: `PUT /portfolios/{id}` con modal centrado (nombre). Hooks `useRenamePortfolio`.
- **Borrar**: `DELETE /portfolios/{id}` con confirmación; manejar 409 (holdings abiertos) con mensaje
  claro. Hook `useDeletePortfolio`; al borrar, invalidar la lista y navegar a `/investments`.
- Acciones en la cabecera de `investments/[id]`.

### T6 — Holdings `*Original` + summary global (módulo Investments)
- Materializar los campos `*Original` en `types/portfolios.ts` (quitar `TODO`, marcar presentes,
  añadir `marketValueOriginal`) y completar el `PerformanceTab`.
- `GET /portfolios/summary` → hook `usePortfoliosSummary`, consumido por el mini-resumen del
  Dashboard (`dashboard/page.tsx`), sustituyendo el cálculo cliente.

### T7 — Pantallas Companies ("Mercado") (módulo Investments)
- **Nueva entrada de nav "Mercado"** en `navbar-private` (desktop + móvil).
- **Listado** `/market` → `GET /companies` **paginado** (patrón T4).
- **Crear empresa**: modal centrado → `POST /companies`.
- **Detalle** `/market/[id]` → `GET /companies/{id}` (cabecera: ticker, nombre, moneda, último precio;
  breadcrumb `← Mercado`).
- **Combobox de empresa** del alta de holding (ver §4) pasa a este patrón, alimentado por
  `?pageSize=100` + filtro cliente.
- **Tipos**: revisar/ampliar `types/companies.ts` contra `CompanyDto`.
- **Deuda que queda**: sin `PUT/DELETE` de empresa → **costuras documentadas**, sin botones
  editar/borrar.

### T8 — Valuations: listado + crear + serie (módulo Investments)
- Dentro de `/market/[id]`:
  - **Serie de cotización**: `GET /companies/{id}/valuations/series?period=3m|6m|1y|3y|5y` → gráfico
    de líneas (`--chart-line`) con selector de periodo + summary (min/max/último/variación %).
  - **Listado de valoraciones**: `GET /companies/{id}/valuations` **paginado**.
  - **Crear valoración**: modal → `POST /companies/{id}/valuations`.
- **Tipos**: `types/valuations.ts` (espejo de `ValuationDto` y del DTO de la serie/summary).
- **Deuda que queda**: sin `GET /valuations/{id}` ni `DELETE` → **costuras documentadas**, sin
  ver-detalle-individual ni borrar valoración.

---

## 6. Registro de deuda (3ª ronda — pendiente de backend)

Se anota lo que aflore; hoy el conjunto conocido es:

1. **Companies sin `PUT/DELETE`** — no se puede editar ni borrar una empresa desde el frontend.
2. **Valuations sin `GET{id}` ni `DELETE`** — no hay ver-detalle-individual ni borrar valoración.
3. **Búsqueda server-side de empresas (`?search=`)** — hoy el combobox filtra **en cliente** sobre
   `pageSize=100`; si el catálogo supera 100 empresas el filtro queda incompleto.

*(El máximo real de `pageSize` y la ausencia de `?search` se verifican contra el backend al
implementar T7, por la norma de tipos.)*

---

## 7. Verificación (Definition of Done por tarea)

- **Tipos**: cada `types/*` tocado verificado contra el DTO/Controller real antes de consumirlo.
- **Componentes/hooks**: Vitest + RTL para el hook nuevo (mock del `api`) y el render de cada estado
  (carga/error/vacío/datos).
- **E2E Playwright**: los flujos críticos existentes (login, crear gasto, vender holding) siguen
  verdes; añadir cobertura donde la tarea introduzca un flujo crítico nuevo (p. ej. registrar con
  moneda, crear empresa/valoración).
- **Diseño**: tokens semánticos, modal centrado para altas/acciones, breadcrumb `← Lista` en
  detalles, responsive mobile-first.
- **Honestidad**: nada de funciones prometidas que no existan; las deudas del §6 se dejan como
  costuras documentadas, no como botones muertos.

---

## 8. Orden de ejecución (tareas → PRs)

| Orden | Tarea | Módulo | PRs |
|-------|-------|--------|-----|
| 1 | T1 — Contactos + Emails | Notifications | 1 |
| 2 | T2 — 2 gráficas de Finance | Finance | 1 |
| 3 | T3a — Moneda en registro · T3b — Perfil | Auth | 2 |
| 4 | T4 — Paginación de listados | Transversal | 1 |
| 5 | T5 — Renombrar/borrar cartera | Investments | 1 |
| 6 | T6 — Holdings `*Original` + summary global | Investments | 1 |
| 7 | T7 — Pantallas Companies ("Mercado") | Investments | 1 |
| 8 | T8 — Valuations + serie | Investments | 1 |

El detalle de pasos por tarea se desarrolla en el plan de implementación correspondiente
(`docs/superpowers/plans/`).
