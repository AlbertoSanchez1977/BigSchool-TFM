# Diseño: Backend — Inversiones: valuations por periodo, holdings Original, portfolio rename/delete + summary (Spec 4)

**Fecha**: 2026-07-04
**Autor**: Alberto Sánchez
**Estado**: Diseño en revisión
**Módulo**: backend (módulo **Investments**)
**Backlog**: `docs/04-backend-tech-debt.md` (#9 valuations por periodo, #10 holdings `*Original`,
#11/D portfolio rename/delete, #11/E summary global). Última feature del bloque 1–4.

---

## 1. Contexto y motivación

Cuatro huecos de Inversiones:
- **#9**: `GET /companies/{id}/valuations` (ya paginado, Spec 00) da el histórico plano; falta una
  **serie de precio por periodo** (3m/6m/1y/3y/5y) + summary para la gráfica de cotización.
- **#10**: `HoldingPerformanceDto` solo trae valores en **moneda base**; el frontend tiene el contrato
  pendiente en `portfolios.ts:106-122` (`buyOriginalCurrency`, `costBasisOriginal`,
  `marketValueOriginal`, `unrealizedPnLOriginal`) con `TODO (deuda técnica)`.
- **#11/D**: la cartera solo se **crea**; faltan renombrar y borrar (spec 003 §4.2 hueco #2).
- **#11/E**: `performance` es **por cartera**; el dashboard necesita un **agregado de todas** las
  carteras del usuario.

## 2. Objetivo y no-objetivos

**Objetivo**
- `GET /companies/{id}/valuations/series?period=` (#9), serie de precio (moneda de la empresa) + summary.
- Campos `*Original` en `HoldingPerformanceDto` (#10), calculados server-side.
- `PUT /portfolios/{id}` (rename) y `DELETE /portfolios/{id}` (soft-delete con reglas) (#11/D).
- `GET /portfolios/summary` (#11/E), agregado de inversiones del usuario en moneda base.

**No-objetivos**
- **No** se paginan series/summary (Spec 00 §8).
- Los `*Original` son **por holding** (no se agregan a nivel cartera: monedas distintas).
- **Frontend fuera de alcance** (§8 apunte): backend puro.

## 3. Decisiones (cerradas en revisión)

| Tema | Decisión |
|------|----------|
| #9 ancla del periodo | La ventana **termina en la última valoración** disponible de la empresa y va N meses atrás (robusto: siempre muestra datos). |
| #9 moneda | Serie en **moneda de la empresa** (`PriceCurrency`); catálogo global, sin conversión por usuario. |
| #10 alcance | `*Original` **solo por holding**; el `PortfolioPerformanceDto` mantiene solo totales en base. |
| #11/D delete | **Guard**: si hay holdings con `OpenShares > 0` → no se borra. Si todo cerrado, **no** se borra dentro de los **5 años fiscales de gracia** desde la última venta (prescripción fiscal ES), con **excepción de dominio específica**. Rename siempre permitido. |

## 4. #10 — Holdings: campos `*Original` en performance

El fragmento `PortfolioSqlFragments.HOLDING_VALUATION` **ya** trae por lote `CompanyCurrency`,
`BuyOriginalAmount`, `OpenShares` y `LastPrice` (en moneda de la empresa). Basta **exponer** en su
SELECT externo los valores sin aplicar `Rate`:
```sql
x.BuyOriginalCurrency,
ROUND(x.OpenShares * x.BuyOriginalAmount, 2)                         AS CostBasisOriginal,
ROUND(x.OpenShares * COALESCE(x.LastPrice, 0), 2)                    AS MarketValueOriginal,
ROUND(x.OpenShares * COALESCE(x.LastPrice, 0) - x.OpenShares * x.BuyOriginalAmount, 2) AS UnrealizedPnLOriginal
```
- `GetPortfolioPerformanceQueryHandler` (`OPEN_HOLDINGS_QUERY`) selecciona los 4 campos nuevos.
- `HoldingPerformanceDto` añade `string BuyOriginalCurrency, decimal CostBasisOriginal,
  decimal MarketValueOriginal, decimal UnrealizedPnLOriginal` → cierra el `TODO` de `portfolios.ts`
  (los campos ya son opcionales allí; el frontend solo tendrá que retirarlos del `TODO`).
- **Aditivo y seguro**: añadir columnas al fragmento **no** afecta a `GetPortfolios` (que solo
  `SUM(hv.MarketValue/CostBasis/UnrealizedPnL)` — ignora las nuevas).

## 5. #9 — `GET /companies/{id}/valuations/series?period=` (Dapper)

- Param `period` (string): uno de `3m|6m|1y|3y|5y` (default `1y`); validador rechaza otros (400).
  Mapa a meses: 3/6/12/36/60.
- Anclaje: se resuelve la **última** `Date` de valoración de la empresa (`LIMIT 1 ORDER BY Date DESC`);
  `from = ultima.AddMonths(-N)`. Serie = valoraciones en `[from, ultima]` `ORDER BY Date ASC`.
- **DTO**: `ValuationSeriesDto(string Currency, IReadOnlyList<ValuationPointDto> Points,
  ValuationSeriesSummaryDto Summary)`; `ValuationPointDto(DateOnly Date, decimal Price)`;
  `ValuationSeriesSummaryDto(decimal First, decimal Last, decimal Min, decimal Max, decimal ChangePct)`
  (`ChangePct = (Last - First) / First * 100`, 0 si `First = 0` o serie vacía).
- Empresa sin valoraciones → serie vacía + summary en cero (no 404 si la empresa existe).
- Carpeta `Application/Investments/Queries/GetCompanyValuationSeries/`. Endpoint en `CompaniesController`
  (`[Authorize]`, como el resto).

## 6. #11/D — Rename / Delete de cartera

### 6.1 Dominio (`Portfolio`)
```csharp
public void Rename(string name)          // valida no vacío; set Name + UpdatedAt
public void Delete(DateOnly today)       // aplica los guards y hace soft-delete
```
`Delete(today)` (la fecha la pasa Application → dominio puro, sin reloj):
1. **Posiciones abiertas**: si algún holding activo tiene `OpenShares > 0` →
   `PortfolioHasOpenPositionsDomainException(IdPortfolio)`.
2. **Gracia fiscal**: sea `ultimaVenta` la mayor `SellDate` de los `Disposal` activos; si existe y
   `ultimaVenta > today.AddYears(-5)` → `PortfolioWithinFiscalGracePeriodDomainException(IdPortfolio,
   ultimaVenta)`. (Retención fiscal ES: no se puede borrar el registro dentro de 5 años desde la
   última venta.)
3. Si pasa ambos → `IdStatus = Deleted` + `UpdatedAt`.

> Requiere cargar el agregado con holdings **y** sus disposals (`IPortfolioRepository.GetByIdAsync`
> con includes; `SellShares` ya opera sobre `_holdings`, y `Holding` debe exponer sus `Disposal`
> —o su `SellDate` máxima— para la regla).
>
> **Excepciones nuevas** (`Domain/Investments/Exceptions/`) mapeadas en `ExceptionHandlingMiddleware`
> a **409** con `Code` propio (`PORTFOLIO_HAS_OPEN_POSITIONS`, `PORTFOLIO_WITHIN_FISCAL_GRACE`) para
> que el frontend muestre el aviso adecuado; la de gracia incluye `ultimaVenta` en el mensaje/campo.

### 6.2 Application + Controller
- `RenamePortfolioCommand(int IdPortfolio, int IdUser, string Name)` (validator `Name` requerido,
  `MaxLength(100)`); handler carga, `Rename`, guarda; 404 si no es del usuario.
- `DeletePortfolioCommand(int IdPortfolio, int IdUser)`; handler carga el agregado, llama
  `portfolio.Delete(DateOnly.FromDateTime(DateTime.UtcNow))`, guarda; 404 si no existe/no es suyo.
- `PortfoliosController`: `PUT /portfolios/{id}` (body `{name}`) y `DELETE /portfolios/{id}`.

## 7. #11/E — `GET /portfolios/summary` (Dapper)

Agregado de **todas** las carteras activas del usuario, en moneda base. Reutiliza
`HOLDING_VALUATION` uniéndolo a las carteras del usuario:
```sql
SELECT COALESCE(SUM(hv.MarketValue),0)   AS MarketValue,
       COALESCE(SUM(hv.CostBasis),0)     AS CostBasis,
       COALESCE(SUM(hv.UnrealizedPnL),0) AS UnrealizedPnL
FROM (<HOLDING_VALUATION>) hv
JOIN Portfolios p ON p.IdPortfolio = hv.IdPortfolio
WHERE p.IdUser = @IdUser AND p.IdStatus <> @StatusDeleted AND hv.OpenShares > 0;
-- + SELECT COALESCE(SUM(RealizedPnL),0), COUNT(*) FROM Portfolios WHERE IdUser=@IdUser AND IdStatus<>@StatusDeleted;
```
- **DTO** `InvestmentsSummaryDto(string BaseCurrency, decimal MarketValue, decimal CostBasis,
  decimal UnrealizedPnL, decimal RealizedPnL, decimal TotalPnL, decimal ReturnPct, int PortfolioCount)`
  (`TotalPnL = Realized + Unrealized`; `ReturnPct = Unrealized / CostBasis * 100` si `CostBasis > 0`).
- Base currency vía `IUserBaseCurrencyProvider` (como `GetPortfolioPerformance`).
- Endpoint `GET /portfolios/summary` en `PortfoliosController` (`[Authorize]`). **No** paginado.

## 8. Frontend (solo apunte — FUERA DE ALCANCE)

No se implementa aquí. Futuro: gráfica de cotización con selector de periodo (`/valuations/series`);
retirar los `TODO (deuda técnica)` de `HoldingPerformance` y pintar los `*Original`; acciones de
renombrar/borrar cartera (mostrando el aviso de las excepciones de dominio); KPIs del dashboard desde
`/portfolios/summary`.

## 9. Fuera de alcance

- Conversión de la serie de #9 a moneda base del usuario (catálogo es global).
- Agregar `*Original` a nivel cartera (monedas mixtas).
- Editar holdings/ventas o tocar la lógica FIFO existente.
- Frontend (§8).

## 10. Verificación

- **Unit (Domain)**: `Portfolio.Rename` (valida); `Portfolio.Delete` — 3 caminos:
  con `OpenShares>0` → `PortfolioHasOpenPositions…`; cerrada con última venta < 5 años →
  `PortfolioWithinFiscalGracePeriod…`; cerrada con última venta ≥ 5 años (o sin ventas) → soft-delete OK.
- **Unit (Application)**: validador de `RenamePortfolioCommand`; validador de `period` (#9);
  `ChangePct`/summary con serie vacía.
- **Integración E2E** (`WebApplicationFactory` + MySQL `bigschool_test`):
  - `GET /performance` → holdings con `*Original` correctos (coste/valor/PnL en moneda de la empresa)
    y `buyOriginalCurrency`; los totales base **no cambian**.
  - `GET /companies/{id}/valuations/series?period=1y` → puntos dentro de la ventana anclada a la última
    valoración, ordenados, summary coherente; `period` inválido → 400.
  - `PUT /portfolios/{id}` renombra; `DELETE` con posición abierta → 409 `PORTFOLIO_HAS_OPEN_POSITIONS`;
    cerrada dentro de 5 años (sembrar Disposal reciente) → 409 `PORTFOLIO_WITHIN_FISCAL_GRACE`;
    cerrada con venta antigua (o sin ventas) → 200 y desaparece de los listados (soft-delete).
  - `GET /portfolios/summary` → agregado sobre varias carteras coincide con la suma de sus `performance`.
  - Los E2E existentes de `performance`/`GetPortfolios`/`valuations` siguen verdes.

---

## Referencias
- Backlog: `docs/04-backend-tech-debt.md`. Contrato pendiente frontend: `portfolios.ts:106-122`.
- Reutiliza: `PortfolioSqlFragments.HOLDING_VALUATION`, `GetPortfolioPerformanceQueryHandler`,
  `IUserBaseCurrencyProvider`. Convenciones: `src/backend/AGENTS.md`.
