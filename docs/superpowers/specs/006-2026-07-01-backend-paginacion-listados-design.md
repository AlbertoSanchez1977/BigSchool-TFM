# Diseño: Backend — Paginación de listados (Spec 00, fundacional)

**Fecha**: 2026-07-01
**Autor**: Alberto Sánchez
**Estado**: Diseño en revisión
**Módulo**: backend (transversal; Investments + SharedKernel)
**Backlog**: `docs/04-backend-tech-debt.md` (Spec 00). Se ejecuta **sobre la estructura de la Spec 0**.

---

## 1. Contexto y motivación

`AGENTS.md` (§API REST) ya fija como convención **"Paginación en listados: `?page=1&pageSize=20`"**,
y **Transactions ya la cumple**: `GET /transactions` devuelve `PagedResult<T>` y el controller
expone `meta { page, pageSize, totalCount }`. Pero el resto de listados **no**: `GET /companies`,
`GET /portfolios` y `GET /companies/{id}/valuations` devuelven la colección **entera** sin `page`,
`pageSize` ni `totalCount`. A medida que crezcan catálogo, carteras e histórico de cotizaciones,
esos endpoints escalan mal y el frontend no puede paginar de forma consistente.

Esta spec **generaliza el contrato de paginación ya existente** al resto de listados, reutilizando
las piezas que ya hay (`PagedResult<T>`, `MetaData`, `NormalizePage/PageSize`) y consolidando el
helper de normalización (hoy duplicado dentro de `GetTransactionsQuery`) en el **SharedKernel** que
introduce la Spec 0.

## 2. Objetivo y no-objetivos

**Objetivo**
- `page` + `pageSize` (query params) y `meta.totalCount` en los listados tabulares:
  **Companies, Portfolios, Valuations**.
- Contrato **idéntico** al ya vigente en Transactions (misma forma de request/response/meta).
- Consolidar `NormalizePage`/`NormalizePageSize` en SharedKernel (dedup con Transactions).

**No-objetivos**
- **Holdings queda fuera** de esta spec: es colección hija acotada del AR `Portfolio` y se mantiene
  **anidada y sin paginar** en el detalle (excepción consciente documentada en §5).
- **No** se paginan **series ni summaries** (charts por periodo #9, `/summary`, `/monthly-chart`,
  `/performance`): son agregados/series completas por diseño (§7).
- **No** se cambian los DTOs de item (`CompanyListItemDto`, `PortfolioListItemDto`,
  `ValuationListItemDto`) ni la lógica de negocio; solo la **forma del listado** (query + handler +
  controller). `PortfolioDetailDto` **no** cambia (sigue embebiendo holdings).
- **No** se introduce cursor-pagination ni `keyset`; se mantiene `LIMIT/OFFSET` (coherente con
  Transactions). Se documenta keyset como evolución si algún listado creciera mucho.

## 3. Contrato de paginación (referencia ya existente)

Piezas reutilizadas tal cual:

```csharp
// Application/Common (→ SharedKernel tras Spec 0)
public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

// Envelope (ya existe)
public record MetaData { int? Page; int? PageSize; int? TotalCount; int? TotalPages /*calculado*/; }
```

- **Request**: `?page=1&pageSize=20` (query string). Defaults: `page=1`, `pageSize=20`. Tope
  `pageSize ≤ 100`; `page < 1 → 1`. (Valores ya consolidados en Transactions.)
- **Response**: `data = Items[]`, `meta = { page, pageSize, totalCount, totalPages }`.
- **Determinismo**: todo listado paginado ordena por una clave **con desempate único** (…, `Id`)
  para que el paginado sea estable entre páginas.

## 4. Endpoints afectados

| Endpoint | Hoy | Cambio | `ORDER BY` (con desempate) |
|----------|-----|--------|-----------------------------|
| `GET /companies` | `IReadOnlyList<CompanyListItemDto>` (todo) | `page`/`pageSize` + `meta`; devuelve `PagedResult` | `c.Name, c.IdCompany` |
| `GET /portfolios` | `IReadOnlyList<PortfolioListItemDto>` (todo) | idem | `p.IdPortfolio` (ya único) |
| `GET /companies/{id}/valuations` | `IReadOnlyList<ValuationListItemDto>` (todo) | idem | `Date DESC, IdValuation DESC` (ya único) |

`GET /transactions` **no se toca** (ya cumple; es la referencia). `GET /portfolios/{id}` (detalle
con holdings) **no se toca** (§5).

## 5. Holdings — excepción consciente (no se pagina)

Los holdings **no son un listado propio**: viajan **anidados** dentro de `PortfolioDetailDto`
(`GET /portfolios/{id}`), como **entidades hijas del AR `Portfolio`**. Se decide **dejarlos fuera**
de la paginación:

- **Razón (DDD)**: son hijos de un agregado **acotado** — una cartera tiene decenas de holdings, no
  miles. Se cargan **con** el agregado (`GetPortfolioById`) y no tiene sentido paginarlos por
  separado; hacerlo rompería la cohesión "detalle de cartera = cartera + sus posiciones".
- **Implicación**: `PortfolioDetailDto` **no cambia** y el frontend de detalle de cartera sigue
  leyendo `detail.holdings` igual que hoy. Cero churn.

Si en el futuro una cartera pudiera acumular cientos de holdings, se reconsideraría con un endpoint
dedicado `GET /portfolios/{id}/holdings?page&pageSize`; hoy no aplica.

## 6. Patrón de implementación (por listado)

Espejo exacto de `GetTransactions` (COUNT + página en un solo round-trip con `QueryMultipleAsync`):

1. **Query record**: añadir `int Page, int PageSize` y devolver `PagedResult<TItem>` en vez de
   `IReadOnlyList<TItem>`.
2. **Handler (Dapper)**: normaliza page/pageSize; arma `DynamicParameters` con `@PageSize` y
   `@Offset = (page-1)*pageSize`; ejecuta **dos sentencias** en un `QueryMultipleAsync`:
   `SELECT COUNT(*) …` (con el **mismo WHERE** pero **sin** joins de enriquecimiento) + `SELECT …
   LIMIT @PageSize OFFSET @Offset`. Devuelve `new PagedResult<TItem>(items, page, pageSize, total)`.
   - **Companies**: el `COUNT(*)` va sobre `Companies` con los filtros `Sector/Market`+status (sin
     el `LEFT JOIN` de última valoración). La página conserva el join + `ORDER BY c.Name, c.IdCompany`.
   - **Portfolios**: el `COUNT(*)` cuenta **carteras** (`Portfolios` con `IdUser`+status), **no**
     filas tras `GROUP BY`. La página conserva `GROUP BY` + `HOLDING_VALUATION` + `LIMIT/OFFSET`.
   - **Valuations**: `COUNT(*)` sobre `Valuations` con `IdCompany`+status; página con el `ORDER BY`
     actual.
3. **Controller**: añadir `[FromQuery] int page = 1, [FromQuery] int pageSize = 20`; construir
   `MetaData { Page, PageSize, TotalCount }` y devolver
   `ApiResponse<IReadOnlyList<TItem>>.Success(result.Items, meta)` — idéntico a `TransactionsController.Get`.

SQL siguiendo `AGENTS.md`: `private const … _QUERY` en UPPERCASE, `DynamicParameters`, enums como
parámetro (`@StatusDeleted = EntityStatus.Deleted`), nunca números mágicos.

## 7. Consolidación del helper de normalización (SharedKernel)

`NormalizePage`/`NormalizePageSize` viven hoy **dentro** de `GetTransactionsQuery` (20 por defecto,
tope 100). Se extraen a un helper compartido en **SharedKernel** (creado por la Spec 0), p. ej.
`Pagination.NormalizePage(int)` / `Pagination.NormalizePageSize(int)` con las constantes
`DEFAULT_PAGE_SIZE = 20` y `MAX_PAGE_SIZE = 100`. `GetTransactionsQuery` pasa a usarlo (dedup); los
nuevos listados lo reutilizan. Un único sitio para la política de paginación.

## 8. Qué NO se pagina (aclaración de diseño)

Se paginan **listados tabulares**. **No** se paginan:
- **Holdings** (§5): colección hija acotada del AR `Portfolio`.
- **Series por periodo** (charts de valuations #9, `/transactions/monthly-chart`): quieres el
  periodo entero para pintar la gráfica.
- **Summaries/agregados** (`/transactions/summary`, `/transactions/by-category` #6,
  `/portfolios/summary` #E, `/portfolios/{id}/performance`): son totales/colecciones acotadas por
  naturaleza.

Estas specs (007–010) definirán esos endpoints como no paginados de forma explícita.

## 9. Fuera de alcance

- Paginación de Holdings (§5) — reconsiderable en el futuro, no ahora.
- Cursor/keyset pagination (evolución si algún listado creciera mucho).
- Ordenación configurable por el cliente (`?sortBy`): cada listado mantiene su `ORDER BY` fijo.
- Cambios en DTOs de item o en la lógica de negocio.

## 10. Verificación

- **Unit (Application)**: por handler, que `page/pageSize` fuera de rango se normalizan (0/negativo
  → 1; `>100` → 100; vacío → 20) y que `TotalCount` refleja el total **sin** el `LIMIT`.
- **Integración E2E** (`WebApplicationFactory` + MySQL `bigschool_test`): sembrar N > pageSize
  filas y verificar: primera página con `pageSize` ítems, `meta.totalCount = N`,
  `meta.totalPages` correcto, segunda página con el resto y **sin solape** (paginado determinista);
  `pageSize` fuera de rango capado; listado vacío → `data: []`, `totalCount: 0`.
- **Portfolios**: verificar que `totalCount` cuenta **carteras** (no filas del `GROUP BY`).
- **Frontend**: los services de companies/portfolios/valuations pasan `page/pageSize` y leen
  `meta.totalCount`; los E2E Playwright existentes siguen verdes. (Detalle de cartera y sus holdings
  no cambian, §5.)

---

## Referencias
- Backlog y orden de specs: `docs/04-backend-tech-debt.md`.
- Patrón de referencia ya implementado: `GetTransactionsQuery(Handler)` + `TransactionsController.Get`.
- Convenciones (paginación, Dapper): `src/backend/AGENTS.md`.
- Estructura modular sobre la que se ejecuta: `005-2026-07-01-backend-modular-monolith-design.md`.
