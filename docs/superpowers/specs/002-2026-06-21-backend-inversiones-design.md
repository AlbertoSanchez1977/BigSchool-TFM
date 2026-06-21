# Diseño: BC Inversiones — Value Investing multimoneda (Plan 3)

**Fecha**: 2026-06-21
**Autor**: Alberto Sánchez
**Estado**: Diseño aprobado (pendiente de planes de implementación 3A/3B)
**Módulo**: backend

---

## 1. Contexto y motivación

Cerrados el Plan 1 (autenticación) y el Plan 2 (BC Finanzas Personales + cimientos multimoneda:
`Currency`, `Money`, `MoneyConversion`, `ExchangeRate`, `ExchangeRateApiClient`), el siguiente
paso es el **Bounded Context de Inversiones**.

El objetivo del producto es un seguimiento de cartera con enfoque **value investing**: el usuario
registra **compras** de acciones de empresas (que cotizan en distintas monedas), las mantiene en el
tiempo, registra **ventas** (totales o parciales) que generan **plusvalías/minusvalías realizadas**, y
puede **recomprar** en el futuro. El sistema debe consolidar el patrimonio y el resultado (realizado y
no realizado) en la **moneda base del usuario**.

El modelo de datos ya estaba esbozado a alto nivel en `docs/02-backend-design.md` (`Company`,
`Valuation`, `Portfolio`, `Holding`). Esta spec lo cierra a nivel de dominio, incorporando dos
aspectos que faltaban y que son críticos para el enfoque value investing:

1. **Lado venta + plusvalía realizada** (`Disposal`, consolidación en `Portfolio`).
2. **Disposición FIFO** para valores homogéneos (misma `Company`), coherente con la fiscalidad
   (IRPF español: método FIFO obligatorio para valores homogéneos).

Los cimientos multimoneda del Plan 2 se reutilizan **sin cambios de modelo**.

---

## 2. Decisiones de diseño (acordadas)

| Decisión | Elección | Implicación |
|---|---|---|
| Catálogo de empresas | **Global compartido** (`Company`/`Valuation` sin `IdUser`, seedeado) | Una valoración no puede snapshotear a UNA base de usuario; la conversión a base se hace por-usuario en query |
| Precio de `Valuation` | **`Money` en la moneda de la empresa** (`Company.Currency`) | Sin `MoneyConversion`; la conversión a base del usuario se calcula en la query de performance |
| `Holding` = unidad | **Lote/tranche de compra** (cada compra = una línea; recompra = nueva línea) | Soporta value investing real; coste base congelado por lote |
| Coste base (`AvgBuyPrice`) | **`MoneyConversion` snapshot congelado a `BuyDate`** | Coste inmutable en moneda base; captura el FX del momento de compra |
| Valor de mercado (query) | Última `Valuation` convertida al tipo de **su fecha** | Determinista, sin red en queries (seed de tipos) |
| Lado venta | **`Disposal`** como entidad hija de `Holding` | Historial completo de cada disposición (precio/fecha/tipo/plusvalía) |
| Evento de venta multi-lote | **N `Disposal` correlacionados por fecha** (sin entidad `Sale`) | Una venta FIFO que abarca varios lotes genera un `Disposal` por lote, mismo `SellDate`/`SellPrice` |
| Disposición | **FIFO obligatorio** (no configurable) | Al vender se consumen los lotes más antiguos primero |
| Plusvalía realizada del Portfolio | **Columna persistida** (`Portfolio.RealizedPnL`, mantenida por el agregado) | Lectura instantánea; consistencia transaccional dentro del agregado |

---

## 3. Modelo de dominio — agregado de 3 niveles

Dos Aggregate Roots independientes; el catálogo (`Company`) es global y la cartera (`Portfolio`) es
por-usuario y referencia `Company` por `IdCompany`.

```
Company (AR, global)                 Portfolio (AR, por-usuario)
  └─ Valuation (hija)                  └─ Holding (hija = lote de compra)
                                            └─ Disposal (hija = una venta)
```

### 3.1 `Company` (AR, global) + `Valuation` (hija)

`Company` es un **catálogo compartido** (sin `IdUser`). Posee `Valuation` como entidad hija, igual
que `User` posee `SubCategory` (constructor `private`, factory `internal`, alta vía
`company.AddValuation(...)`).

**Company**

| Campo | Tipo | Restricciones |
|---|---|---|
| IdCompany | INT | PK, AUTO_INCREMENT |
| Name | VARCHAR(200) | NOT NULL |
| Ticker | VARCHAR(10) | UNIQUE, NOT NULL |
| Sector | VARCHAR(100) | NULL |
| Market | VARCHAR(50) | NULL (NASDAQ, BME, LSE…) |
| Currency | CHAR(3) | NOT NULL, DEFAULT 'EUR' (moneda de cotización, enum `Currency`) |
| IdStatus | SMALLINT | NOT NULL, DEFAULT 2 (`EntityStatus`) |
| CreatedAt / UpdatedAt | DATETIME | auditoría |

**Valuation** (hija de Company)

| Campo | Tipo | Restricciones |
|---|---|---|
| IdValuation | INT | PK, AUTO_INCREMENT |
| IdCompany | INT | FK → Companies, NOT NULL |
| Price | DECIMAL(18,4) + CHAR(3) | `Money` en `Company.Currency`, > 0 |
| Date | DATE | NOT NULL |
| Source | VARCHAR(100) | NULL |
| IdStatus | SMALLINT | NOT NULL, DEFAULT 2 |
| CreatedAt / UpdatedAt | DATETIME | auditoría |
| | | UNIQUE(IdCompany, Date) |

> `Valuation.Price` es un `Money` plano (importe + moneda de la empresa). **No** lleva
> `MoneyConversion`: al ser global no existe una moneda base de usuario única a la que snapshotear.

### 3.2 `Portfolio` (AR, por-usuario) + `Holding` (hija) + `Disposal` (nieta)

**Portfolio**

| Campo | Tipo | Restricciones |
|---|---|---|
| IdPortfolio | INT | PK, AUTO_INCREMENT |
| IdUser | INT | FK → Users, NOT NULL |
| Name | VARCHAR(100) | NOT NULL |
| RealizedPnL | DECIMAL(18,2) + CHAR(3) | `Money` en moneda base del usuario; **columna persistida** mantenida por el agregado; default 0 |
| IdStatus | SMALLINT | NOT NULL, DEFAULT 2 |
| CreatedAt / UpdatedAt | DATETIME | auditoría |

**Holding** (entidad hija de Portfolio = **lote de compra**)

| Campo | Tipo | Restricciones |
|---|---|---|
| IdHolding | INT | PK, AUTO_INCREMENT |
| IdPortfolio | INT | FK → Portfolios, NOT NULL |
| IdCompany | INT | FK → Companies, NOT NULL |
| Shares | DECIMAL(18,4) | shares **compradas** (inmutable), > 0 |
| AvgBuyPrice | `MoneyConversion` | snapshot congelado a `BuyDate` (Original en `Company.Currency`, Base en moneda usuario) |
| BuyDate | DATE | NOT NULL |
| Notes | VARCHAR(500) | NULL |
| IdStatus | SMALLINT | NOT NULL, DEFAULT 2 |
| CreatedAt / UpdatedAt | DATETIME | auditoría |

- Columnas del owned `AvgBuyPrice`: `BuyOriginalAmount, BuyOriginalCurrency, BuyExchangeRate, BuyBaseAmount, BuyBaseCurrency, BuyRateDate`.
- **Derivados** (no columnas): `OpenShares = Shares − Σ Disposals.Shares`; realizado del lote `= Σ Disposals.RealizedPnL`. Un lote está **cerrado** cuando `OpenShares == 0`.
- El "Avg" de `AvgBuyPrice` es vestigial (un lote = una compra); se mantiene por compatibilidad con el diseño existente.

**Disposal** (entidad hija de Holding = **una venta de ese lote**)

| Campo | Tipo | Restricciones |
|---|---|---|
| IdDisposal | INT | PK, AUTO_INCREMENT |
| IdHolding | INT | FK → Holdings, NOT NULL |
| Shares | DECIMAL(18,4) | shares vendidas en esta disposición, > 0, ≤ `OpenShares` del lote |
| SellPrice | `MoneyConversion` | snapshot congelado a `SellDate` (Original en `Company.Currency`, Base en moneda usuario) |
| SellDate | DATE | NOT NULL |
| RealizedPnL | DECIMAL(18,2) + CHAR(3) | `Money` en moneda base; **calculado** = `(SellPrice.Base − AvgBuyPrice.Base) × Shares` |
| Notes | VARCHAR(500) | NULL |
| IdStatus | SMALLINT | NOT NULL, DEFAULT 2 |
| CreatedAt / UpdatedAt | DATETIME | auditoría |

- Columnas del owned `SellPrice`: `SellOriginalAmount, SellOriginalCurrency, SellExchangeRate, SellBaseAmount, SellBaseCurrency, SellRateDate`.

---

## 4. Modelo multimoneda (núcleo)

| Concepto | Moneda | Mecanismo |
|---|---|---|
| `Valuation.Price` | Moneda de la empresa | `Money` plano (catálogo global, sin conversión) |
| `Holding.AvgBuyPrice` | Original = `Company.Currency`; Base = moneda usuario | `MoneyConversion` **snapshot congelado a `BuyDate`** (coste base inmutable) |
| `Disposal.SellPrice` | Original = `Company.Currency`; Base = moneda usuario | `MoneyConversion` **snapshot congelado a `SellDate`** |
| `Disposal.RealizedPnL` | Moneda base usuario | calculado en dominio = `(SellPrice.Base − AvgBuyPrice.Base) × Shares` |
| `Portfolio.RealizedPnL` | Moneda base usuario | Σ de los `Disposal`, persistido y mantenido por el agregado |
| Valor de mercado (query) | → moneda base usuario | última `Valuation.Price` × `OpenShares`, convertida al tipo de **la fecha de esa valoración** |

- **Coste base**: al crear el `Holding`, Application resuelve `rate = Company.Currency → user.BaseCurrency`
  a `BuyDate` (vía `IExchangeRateProvider`/`ExchangeRateApiClient`, igual que `Transaction`) y se
  congela en `AvgBuyPrice`. Si `Company.Currency == user.BaseCurrency` → `rate = 1`.
- **Precio de venta**: al registrar la venta, Application resuelve `rate` a `SellDate`; el dominio
  calcula `SellPrice.Base` y el `RealizedPnL` de cada `Disposal`. El `RealizedPnL` incluye
  implícitamente el efecto FX entre el `BuyDate` del lote y el `SellDate`.
- **Valor actual y performance** (query Dapper): la conversión de la última valoración a la base del
  usuario se hace con `LEFT JOIN ExchangeRates` por `(Company.Currency, user.BaseCurrency, Valuation.Date)`,
  con **fallback al último tipo conocido ≤ fecha**. El dominio nunca llama a servicios externos; las
  queries no llaman al proveedor (solo leen el cache seedeado).

---

## 5. Disposición FIFO (regla de negocio crítica)

La venta es a nivel **(Portfolio, Company)**, no de lote elegido a dedo:

```csharp
portfolio.SellShares(companyId, shares, sellPrice /*Money en Company.Currency*/, sellDate, rate);
```

Algoritmo en el agregado `Portfolio`:

1. Localiza todos los `Holding` **abiertos** (`OpenShares > 0`) de esa `Company`.
2. Los ordena por `BuyDate` **ascendente** (los más antiguos primero); desempate por `IdHolding`.
3. Consume FIFO: del lote más antiguo toma `min(sharesRestantes, lote.OpenShares)`, crea un
   `Disposal` en ese lote (mismo `SellPrice`/`SellDate`, coste base del lote → su propio
   `RealizedPnL`), descuenta y pasa al siguiente lote hasta cubrir `shares`.
4. **Invariante**: `shares ≤ Σ OpenShares de esa Company en el Portfolio`. Si no → excepción de
   dominio (`InsufficientSharesDomainException : DomainException` → 400).
5. Acumula `Portfolio.RealizedPnL += Σ disposals.RealizedPnL` en el **mismo `SaveChanges`**.

Una venta puede generar **N `Disposal`** (uno por lote tocado); el último lote consumido puede quedar
**parcialmente** abierto. El "evento de venta" es implícito (mismo `IdCompany` + `SellDate`); no hay
entidad `Sale`.

> **FIFO obligatorio, no configurable** — decisión de diseño coherente con IRPF español para valores
> homogéneos.

---

## 6. CQRS

### 6.1 Commands (EF Core)

| Command | Agregado | Notas |
|---|---|---|
| `CreateCompany` | Company | catálogo global; valida `Ticker` único, `Currency` válida |
| `AddValuation` | Company | `company.AddValuation(price, date, source)`; unicidad `(IdCompany, Date)` |
| `CreatePortfolio` | Portfolio | `IdUser` del claim; `RealizedPnL` inicia a 0 en base del usuario |
| `AddHolding` | Portfolio | compra = nuevo lote; resuelve `rate` a `BuyDate`; congela `AvgBuyPrice` |
| `UpdateHolding` | Portfolio | edita `Notes`/datos no monetarios del lote; ownership por `IdUser` |
| `DeleteHolding` | Portfolio | soft-delete del lote (y sus `Disposal`); ownership |
| `SellShares` | Portfolio | **FIFO** (sección 5); resuelve `rate` a `SellDate`; genera N `Disposal`; actualiza `RealizedPnL` |

Validators FluentValidation: `Shares > 0`, `Price > 0`, fechas no vacías, `Ticker` no vacío,
ownership del `Portfolio` (not-found en vez de 403, como en Transactions).

### 6.2 Queries (Dapper) — consolidan en moneda base del usuario

| Query | Salida |
|---|---|
| `GetCompanies` | lista + filtro (sector/market/ticker) |
| `GetCompanyById` | detalle + última valoración |
| `GetCompanyValuations` | histórico de valoraciones de una empresa |
| `GetPortfolios` | carteras del usuario + resumen (valor total, realizado) |
| `GetPortfolioById` | detalle + holdings (open shares, coste base, valor actual) |
| `GetPortfolioPerformance` | desglose realizado / no realizado / total (sección 7) |

SQL como `const _QUERY` UPPERCASE a nivel de clase + `DynamicParameters` (convención del proyecto).

---

## 7. Forma de la query de performance

Por **holding (lote abierto)**: `OpenShares`, coste medio base, valor de mercado base (última
`Valuation` × `OpenShares` convertida), P/L **no realizado** absoluto y %.

Por **portfolio** (consolidado en base del usuario):
- **No realizado**: Σ (valor de mercado − coste) de los lotes abiertos.
- **Realizado**: `Portfolio.RealizedPnL` (columna persistida) — equivalente a Σ `Disposal.RealizedPnL`.
- **Total**: realizado + no realizado.
- Valor de mercado total, coste total invertido (abierto), % de rentabilidad.

---

## 8. Endpoints (todos `[Authorize]`)

### Companies (catálogo global)
| Método | Ruta | Persistencia |
|---|---|---|
| GET | /api/v1/companies | Dapper |
| GET | /api/v1/companies/{id} | Dapper |
| POST | /api/v1/companies | EF Core |
| GET | /api/v1/companies/{id}/valuations | Dapper |
| POST | /api/v1/companies/{id}/valuations | EF Core |

### Portfolios (por-usuario)
| Método | Ruta | Persistencia |
|---|---|---|
| GET | /api/v1/portfolios | Dapper |
| POST | /api/v1/portfolios | EF Core |
| GET | /api/v1/portfolios/{id} | Dapper |
| POST | /api/v1/portfolios/{id}/holdings | EF Core (compra = nuevo lote) |
| PUT | /api/v1/portfolios/{id}/holdings/{hId} | EF Core |
| DELETE | /api/v1/portfolios/{id}/holdings/{hId} | EF Core (soft) |
| POST | /api/v1/portfolios/{id}/sales | EF Core (**FIFO**: body `{ companyId, shares, sellPrice, sellDate, currency? }`) |
| GET | /api/v1/portfolios/{id}/performance | Dapper |

> Se elimina del diseño previo cualquier `POST /portfolios/{id}/holdings/{hId}/sales`: la venta es a
> nivel de `Company` por la regla FIFO.

---

## 9. Seed y determinismo

Migraciones con seed para que la demo y los tests E2E sean deterministas sin red:

- **Companies** en distintas monedas: p.ej. `AAPL`/USD (NASDAQ), `MSFT`/USD, una de `BME`/EUR
  (p.ej. `SAN`), una de `LSE`/GBP.
- **Valuations** de ejemplo (varias fechas por empresa).
- **ExchangeRates** de los pares y fechas usados (USD→EUR, GBP→EUR, …) coincidentes con los
  `BuyDate`/`SellDate`/`Valuation.Date` de la demo, para que la conversión sea determinista.

---

## 10. Autorización y simplificaciones (TFM)

- `Portfolio`/`Holding`/`Disposal`: ownership por `IdUser` del `Portfolio` (not-found en vez de 403).
- `Company`/`Valuation`: catálogo **global**; cualquier usuario autenticado puede leerlas y crearlas
  (el sistema no tiene rol admin). Documentado como simplificación.
- `DECIMAL(18,4)` para `Shares` y precios de inversión; `DECIMAL(18,2)` para importes en moneda base
  (P/L, coste base). No se contemplan monedas con distinto nº de decimales (p.ej. JPY=0).
- **Sin reconversión retroactiva** si el usuario cambia su moneda base: `AvgBuyPrice`, `Disposal` y
  `Portfolio.RealizedPnL` conservan su snapshot en la base del momento.
- Tipos de cambio cacheados por día (`RateDate` granularidad DATE), no intradía.

---

## 11. Testing

- **Domain**:
  - `Company.AddValuation` (unicidad `(IdCompany, Date)`, precio > 0).
  - `Holding` factory (shares > 0, `AvgBuyPrice` snapshot, `OpenShares` derivado).
  - `Portfolio.SellShares` **FIFO**: consume lotes por antigüedad, venta multi-lote genera N
    `Disposal`, lote parcialmente consumido, `RealizedPnL` por lote correcto (incluye FX), invariante
    de shares insuficientes.
- **Application**: handlers con `IExchangeRateProvider` mockeado (rate a `BuyDate`/`SellDate`);
  cálculo de performance con rates conocidos; validators.
- **Integración E2E** (siguiendo las reglas de `src/backend/AGENTS.md`): un fichero por endpoint,
  test exhaustivo del POST con verificación física en BD, conversión multimoneda determinista
  (rates seedeados), 401/400/404, soft-delete, y un E2E de **ciclo completo value investing**:
  crear company + valuations → crear portfolio → comprar 2 lotes → vender FIFO cruzando lotes →
  verificar `Disposal` generados, `Portfolio.RealizedPnL` y performance.

---

## 12. Alcance de implementación — dos planes

Una sola spec del BC; **dos planes** de implementación, cada uno entregable y testeable de forma
independiente:

- **Plan 3A — Catálogo**: `Company` (AR) + `Valuation` (hija). Migración + seed de empresas y
  valoraciones. CQRS (`CreateCompany`, `AddValuation`, `GetCompanies`, `GetCompanyById`,
  `GetCompanyValuations`). `CompaniesController`. Tests unitarios + E2E.
- **Plan 3B — Carteras**: `Portfolio` (AR) + `Holding` (hija) + `Disposal` (nieta). FIFO
  (`SellShares`), `RealizedPnL` persistido, performance. CQRS de cartera. `PortfoliosController`.
  Tests unitarios (FIFO exhaustivo) + E2E (ciclo completo). Consume el catálogo de 3A.

---

## 13. Cambios documentales (parte de la implementación)

- **`docs/02-backend-design.md`** → actualizar `Holdings` (lote, `AvgBuyPrice` como `MoneyConversion`),
  nuevas tablas `Disposals` y `Portfolio.RealizedPnL`, `Valuation.Price` como `Money` en moneda de la
  empresa, regla FIFO, endpoints (`/portfolios/{id}/sales`).
- **`docs/01-arquitectura.md`** → ADR (BC Inversiones: catálogo global vs cartera por-usuario,
  disposición FIFO, snapshot de coste/venta, performance consolidada en base).
- **`docs/diario.md`** → entradas de Plan 3A y 3B.

---

## 14. Resumen de artefactos nuevos

| Artefacto | Capa | Tipo | Plan |
|---|---|---|---|
| `Company` | Domain | Aggregate Root | 3A |
| `Valuation` | Domain | entidad hija de Company | 3A |
| `ICompanyRepository` / `CompanyRepository` | Application/Infra | repositorio | 3A |
| `CompanyConfiguration` / `ValuationConfiguration` | Infrastructure | EF config + migración + seed | 3A |
| Commands/Queries/Validators de Companies | Application | CQRS | 3A |
| `CompaniesController` | WebApi | controller | 3A |
| `Portfolio` | Domain | Aggregate Root | 3B |
| `Holding` | Domain | entidad hija (lote) | 3B |
| `Disposal` | Domain | entidad nieta (venta) | 3B |
| `InsufficientSharesDomainException` | Domain | excepción (→400) | 3B |
| `IPortfolioRepository` / `PortfolioRepository` | Application/Infra | repositorio | 3B |
| `PortfolioConfiguration` / `HoldingConfiguration` / `DisposalConfiguration` | Infrastructure | EF config + migración | 3B |
| Commands/Queries/Validators de Portfolios (incl. `SellShares` FIFO, performance) | Application | CQRS | 3B |
| `PortfoliosController` | WebApi | controller | 3B |
| ADR BC Inversiones | docs | decisión de arquitectura | 3A/3B |
