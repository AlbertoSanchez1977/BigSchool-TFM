# Bloque 2 — Seed demo enriquecido (dos usuarios) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Un script generador determinista (`infra/docker/mysql/generate_seed.py`) que **reemplaza** `infra/docker/mysql/seed.sql` por un dataset de demo rico: dos usuarios (EUR/USD), transacciones (2022→hoy para el EUR, 2026→hoy para el USD), 2 carteras, 18 holdings, tipos de cambio en las fechas de compra y valoraciones mensuales por empresa.

**Architecture:** Un único script Python de stdlib, determinista (`random.seed` fijo, `Decimal` con redondeo banker's). Emite SQL en orden de FKs: Users → Companies → ExchangeRates → Portfolios → Holdings → Valuations → Transactions. La verificación carga `init.sql` + el `seed.sql` generado en una BD desechable del MySQL dev y comprueba integridad (recuentos, FKs, y que la valoración multimoneda de la cartera USD da `MarketValue > 0`).

**Tech Stack:** Python 3 (stdlib: `random`, `decimal`, `datetime`), MySQL 8 (contenedor dev `bigschool-mysql`), `init.sql` existente como esquema + catálogo base.

**Prerrequisitos (ya en `develop`):**
- `infra/docker/mysql/init.sql`: esquema + catálogo base EF (SubCategories 1-28, Companies 1-4 AAPL/MSFT/SAN/SHEL, Valuations 1-8, ExchangeRates 'seed' 1-4 incluyendo `USD→EUR`@2026-01-01 y `GBP→EUR`@2026-01-01).
- MySQL dev levantable desde `infra/` (`cd infra && docker compose --env-file .env up -d mysql`), contenedor `bigschool-mysql`, puerto host `3306`, password root en `infra/.env` (`MYSQL_ROOT_PASSWORD`).

**Contexto verificado del repo (no requiere relectura):**
- **Enums persistidos como NOMBRE (string)** vía `HasConversion<string>()` en `CompanyConfiguration.cs`:
  - `Market` (sin valores explícitos): `NYSE, NASDAQ, TSX, CSE, TSXV, LSE, AquisExchange, CboeUK, Frankfurt, Xetra, BorseStuttgart, BorseMunchen, EuronextParis, BME`. **No hay** Ámsterdam ni Milán.
  - `Sector`: `Technology, Financials, Energy, Retail, Automotive, Healthcare, RealEstate, Utilities, ConsumerGoods, Industrials, Other`.
  - `Currency` (CHAR(3), nombre = ISO): `EUR, USD, GBP, CHF, JPY`.
- `MainCategory` es un **enum** (no tabla): Expense `EssentialExpenses=1, Investment=2, Savings=3, Donations=4, Luxuries=5, Education=6, Amortizations=7`; Income `Salary=10, Rentals=11, Dividends=12, Other=13`. `Transactions.IdMainCategory` guarda el valor numérico; `Type` SMALLINT `0=Income, 1=Expense`.
- `SubCategories` (IDs→IdMainCategory): 1-5→1; 6-8→2; 9-10→3; 19→4; 11-15→5; 16-18→6; 20-21→7; 22→10; 23-25→11; 26-27→12; 28→13.
- **Valoración de cartera** (`PortfolioSqlFragments.HOLDING_VALUATION`): `Rate = 1` si `Company.Currency == BaseCurrency`; si no, **la última `ExchangeRates.Rate` con `FromCurrency=Company.Currency, ToCurrency=BaseCurrency, RateDate ≤ (última fecha de valoración)`**, con `COALESCE(...,0)` (sin tasa → `MarketValue=0`). `LastPrice` = valoración más reciente por `Date`.
- `Holdings`: `BuyOriginalAmount` = precio **por acción** en moneda de la empresa; `BuyExchangeRate` = tasa snapshot; `BuyBaseAmount = round(BuyOriginalAmount × BuyExchangeRate, 2, banker's)`; `BuyBaseCurrency` = base del usuario; `CostBasis = OpenShares × BuyBaseAmount`.
- `Portfolio.RealizedPnLCurrency` = moneda base del usuario dueño. `AddHolding` exige base == moneda de la cartera (por eso **un usuario por moneda**).
- Hash/salt del usuario demo (password `Demo2026!`): `PasswordHash='IgvUQiiIH1/MUfsvc6nUYA=='`, `PasswordSalt='DfXPG5w8vbmmheoGkMhwYO2zTAWMkFbg/zIZ94728do='`. Reutilizable para el segundo usuario (el hash depende de password+salt, no del email).

**Desviaciones respecto al spec (justificadas, a confirmar en revisión):**
1. **ASML → LVMH (MC)** y **ENEL → Airbus (AIR)**, ambos `EuronextParis`/EUR, porque el enum `Market` no tiene Ámsterdam ni Milán. Mantiene "P2 = 10 empresas, 3 en EUR".
2. **ExchangeRates solo en fechas de adquisición** (no mensuales): la query usa "última tasa ≤ fecha" con fallback, así que una tasa por par-fecha basta para snapshot y display. Fiel al spec.

---

## File Structure

| Fichero | Acción | Responsabilidad |
|---------|--------|-----------------|
| `infra/docker/mysql/generate_seed.py` | Crear | Generador determinista que emite `seed.sql` |
| `infra/docker/mysql/seed.sql` | Reemplazar | Salida del generador (se commitea) |

> **Nota de "test":** el generador no tiene lógica de negocio que merezca unit tests aislados; su corrección se valida **cargando** `init.sql` + el `seed.sql` generado en una BD desechable y ejecutando consultas de integridad (Task 2). Comandos con la herramienta Bash (Git Bash). Requiere Python 3 y el MySQL dev en marcha.

---

## Task 1: Escribir el generador y regenerar `seed.sql`

**Files:**
- Create: `infra/docker/mysql/generate_seed.py`
- Replace: `infra/docker/mysql/seed.sql`

- [x] **Step 1: Crear `infra/docker/mysql/generate_seed.py`**

```python
#!/usr/bin/env python3
"""
Generador determinista del seed de demo (Bloque 2).

Emite infra/docker/mysql/seed.sql con:
  - 2 usuarios: demo@bigschool.com (EUR, IdUser=1) y demo.usd@bigschool.com (USD, IdUser=2)
  - Transacciones: 2022-01..ancla (usuario EUR) y 2026-01..ancla (usuario USD)
  - Companies 5-18 (1-4 ya en init.sql)
  - 2 carteras: P1 EUR (user 1, 8 holdings, compras 2024) y P2 USD (user 2, 10 holdings, compras 2025)
  - ExchangeRates en las fechas de compra que cruzan moneda
  - Valuations: día-1 de cada mes posterior a la adquisición hasta la ancla, por empresa

Uso:
    python infra/docker/mysql/generate_seed.py [YYYY-MM-DD]
El argumento opcional fija la "actualidad" (ancla); por defecto, hoy.
"""
from __future__ import annotations

import random
import sys
from datetime import date
from decimal import Decimal, ROUND_HALF_EVEN

SEED = 20260629
OUT_PATH = "infra/docker/mysql/seed.sql"

# Hash/salt del usuario demo (password Demo2026!), reutilizado para ambos usuarios.
PWD_HASH = "IgvUQiiIH1/MUfsvc6nUYA=="
PWD_SALT = "DfXPG5w8vbmmheoGkMhwYO2zTAWMkFbg/zIZ94728do="

# --- Empresas nuevas (5-18); 1-4 ya existen en init.sql ---
# (id, name, ticker, sector, market, currency)
COMPANIES = [
    (5,  "NVIDIA Corporation",   "NVDA", "Technology",    "NASDAQ",        "USD"),
    (6,  "Inditex",              "ITX",  "Retail",        "BME",           "EUR"),
    (7,  "Iberdrola",            "IBE",  "Utilities",     "BME",           "EUR"),
    (8,  "Amazon.com Inc",       "AMZN", "Retail",        "NASDAQ",        "USD"),
    (9,  "Tesla Inc",            "TSLA", "Automotive",    "NASDAQ",        "USD"),
    (10, "BBVA",                 "BBVA", "Financials",    "BME",           "EUR"),
    (11, "Telefonica",           "TEF",  "Other",         "BME",           "EUR"),
    (12, "Alphabet Inc",         "GOOGL","Technology",    "NASDAQ",        "USD"),
    (13, "Meta Platforms",       "META", "Technology",    "NASDAQ",        "USD"),
    (14, "Netflix Inc",          "NFLX", "Technology",    "NASDAQ",        "USD"),
    (15, "Oracle Corporation",   "ORCL", "Technology",    "NYSE",          "USD"),
    (16, "SAP SE",               "SAP",  "Technology",    "Xetra",         "EUR"),
    (17, "LVMH",                 "MC",   "ConsumerGoods", "EuronextParis", "EUR"),
    (18, "Airbus SE",            "AIR",  "Industrials",   "EuronextParis", "EUR"),
]
# Moneda por empresa (incluye 1-4 de init.sql)
COMPANY_CCY = {1: "USD", 2: "USD", 3: "EUR", 4: "GBP"}
for cid, _n, _t, _s, _m, ccy in COMPANIES:
    COMPANY_CCY[cid] = ccy

# --- Holdings: (company_id, shares, precio_por_accion_en_moneda_empresa, fecha_compra) ---
# P1: cartera EUR (user 1), compras a lo largo de 2024 (día-1, meses distintos)
P1_HOLDINGS = [
    (6,  Decimal("25"),  Decimal("38.40"),  date(2024, 2, 1)),   # ITX  EUR
    (3,  Decimal("50"),  Decimal("4.20"),   date(2024, 3, 1)),   # SAN  EUR
    (1,  Decimal("10"),  Decimal("170.00"), date(2024, 4, 1)),   # AAPL USD -> USD->EUR
    (7,  Decimal("80"),  Decimal("11.50"),  date(2024, 5, 1)),   # IBE  EUR
    (10, Decimal("60"),  Decimal("9.20"),   date(2024, 6, 1)),   # BBVA EUR
    (2,  Decimal("6"),   Decimal("410.00"), date(2024, 8, 1)),   # MSFT USD -> USD->EUR
    (11, Decimal("120"), Decimal("4.05"),   date(2024, 9, 1)),   # TEF  EUR
    (4,  Decimal("15"),  Decimal("27.50"),  date(2024, 10, 1)),  # SHEL GBP -> GBP->EUR
]
# P2: cartera USD (user 2), compras a lo largo de 2025
P2_HOLDINGS = [
    (5,  Decimal("8"),   Decimal("480.00"), date(2025, 1, 1)),   # NVDA  USD
    (8,  Decimal("12"),  Decimal("180.00"), date(2025, 2, 1)),   # AMZN  USD
    (9,  Decimal("10"),  Decimal("250.00"), date(2025, 3, 1)),   # TSLA  USD
    (12, Decimal("9"),   Decimal("155.00"), date(2025, 4, 1)),   # GOOGL USD
    (16, Decimal("20"),  Decimal("170.00"), date(2025, 5, 1)),   # SAP   EUR -> EUR->USD
    (13, Decimal("7"),   Decimal("520.00"), date(2025, 6, 1)),   # META  USD
    (14, Decimal("6"),   Decimal("620.00"), date(2025, 7, 1)),   # NFLX  USD
    (17, Decimal("5"),   Decimal("720.00"), date(2025, 8, 1)),   # LVMH  EUR -> EUR->USD
    (15, Decimal("30"),  Decimal("140.00"), date(2025, 9, 1)),   # ORCL  USD
    (18, Decimal("18"),  Decimal("155.00"), date(2025, 10, 1)),  # AIR   EUR -> EUR->USD
]
# Tasas snapshot en fecha de compra (from, to, fecha) -> rate (cruzan moneda)
BUY_RATES = {
    ("USD", "EUR", date(2024, 4, 1)):  Decimal("0.921000"),
    ("USD", "EUR", date(2024, 8, 1)):  Decimal("0.917000"),
    ("GBP", "EUR", date(2024, 10, 1)): Decimal("1.172000"),
    ("EUR", "USD", date(2025, 5, 1)):  Decimal("1.085000"),
    ("EUR", "USD", date(2025, 8, 1)):  Decimal("1.092000"),
    ("EUR", "USD", date(2025, 10, 1)): Decimal("1.078000"),
}

# --- Categorías de gasto/ingreso: (IdMainCategory, IdSubCategory, descripcion, importe_min, importe_max) ---
EXPENSES_BASE = [  # se incluyen (casi) todos los meses
    (7, 20, "Hipoteca", Decimal("650"), Decimal("650")),
    (1, 1, "Compra supermercado", Decimal("70"), Decimal("190")),
    (1, 1, "Compra supermercado", Decimal("60"), Decimal("140")),
    (1, 3, "Facturas hogar", Decimal("45"), Decimal("130")),
]
EXPENSES_EXTRA = [  # se añaden aleatoriamente para llegar a 8-12/mes
    (1, 2, "Farmacia", Decimal("8"), Decimal("40")),
    (1, 5, "Transporte", Decimal("20"), Decimal("70")),
    (5, 11, "Restaurante", Decimal("25"), Decimal("110")),
    (5, 12, "Ocio", Decimal("10"), Decimal("60")),
    (5, 14, "Ropa", Decimal("30"), Decimal("150")),
    (5, 15, "Tecnologia", Decimal("40"), Decimal("400")),
    (5, 13, "Viaje", Decimal("120"), Decimal("600")),
    (6, 16, "Curso", Decimal("15"), Decimal("200")),
    (6, 17, "Libro", Decimal("10"), Decimal("40")),
    (4, 19, "Donacion ONG", Decimal("10"), Decimal("40")),
]
INCOMES_EXTRA = [
    (11, 23, "Alquiler vivienda", Decimal("600"), Decimal("600")),
    (12, 26, "Dividendo nacional", Decimal("40"), Decimal("150")),
    (13, 28, "Otros ingresos", Decimal("50"), Decimal("300")),
]


def q2(x: Decimal) -> Decimal:
    return x.quantize(Decimal("0.01"), rounding=ROUND_HALF_EVEN)


def q4(x: Decimal) -> Decimal:
    return x.quantize(Decimal("0.0001"), rounding=ROUND_HALF_EVEN)


def add_month(d: date) -> date:
    return date(d.year + (d.month // 12), (d.month % 12) + 1, 1)


def first_of_months(start: date, end: date):
    cur = date(start.year, start.month, 1)
    while cur <= end:
        yield cur
        cur = add_month(cur)


def rnd_amount(lo: Decimal, hi: Decimal) -> Decimal:
    if lo == hi:
        return q2(lo)
    cents = random.randint(int(lo * 100), int(hi * 100))
    return q2(Decimal(cents) / Decimal(100))


def s(text: str) -> str:
    """Literal SQL string (los datos no contienen comillas simples)."""
    return "'" + text + "'"


def gen_transactions(lines, id_user, ccy, start, anchor):
    """Emite transacciones mensuales para un usuario. ccy: 'EUR' o 'USD'. Todo a rate=1."""
    rows = []
    salary_lo, salary_hi = (Decimal("2800"), Decimal("3050")) if ccy == "EUR" else (Decimal("4000"), Decimal("4300"))
    base = EXPENSES_BASE if ccy == "EUR" else [
        (7, 20, "Mortgage", Decimal("1200"), Decimal("1200")),
        (1, 1, "Groceries", Decimal("90"), Decimal("220")),
        (1, 3, "Utilities", Decimal("60"), Decimal("160")),
    ]
    for m in first_of_months(start, anchor):
        month_rows = []
        # Nomina (ingreso) el día 28 (o último disponible)
        pay_day = date(m.year, m.month, 28)
        month_rows.append((id_user, 0, 10, 22, "Nomina" if ccy == "USD" else "Nomina mensual",
                           pay_day, rnd_amount(salary_lo, salary_hi)))
        # Gastos base
        for mc, sc, desc, lo, hi in base:
            day = random.randint(1, 27)
            month_rows.append((id_user, 1, mc, sc, desc, date(m.year, m.month, day), rnd_amount(lo, hi)))
        if ccy == "EUR":
            # Extras hasta un total de 8-12 transacciones
            target = random.randint(8, 12)
            pool = EXPENSES_EXTRA[:]
            random.shuffle(pool)
            i = 0
            while len(month_rows) < target and i < len(pool):
                mc, sc, desc, lo, hi = pool[i]
                day = random.randint(1, 27)
                month_rows.append((id_user, 1, mc, sc, desc, date(m.year, m.month, day), rnd_amount(lo, hi)))
                i += 1
            # Ingreso extra ocasional (~40% de los meses)
            if random.random() < 0.4:
                mc, sc, desc, lo, hi = random.choice(INCOMES_EXTRA)
                day = random.randint(1, 27)
                month_rows.append((id_user, 0, mc, sc, desc, date(m.year, m.month, day), rnd_amount(lo, hi)))
        else:
            # USD: ligero, 4-6/mes
            target = random.randint(4, 6)
            extra = [(1, 1, "Groceries", Decimal("70"), Decimal("160")),
                     (5, 11, "Restaurant", Decimal("25"), Decimal("120")),
                     (5, 12, "Leisure", Decimal("15"), Decimal("80"))]
            random.shuffle(extra)
            i = 0
            while len(month_rows) < target and i < len(extra):
                mc, sc, desc, lo, hi = extra[i]
                day = random.randint(1, 27)
                month_rows.append((id_user, 1, mc, sc, desc, date(m.year, m.month, day), rnd_amount(lo, hi)))
                i += 1
        rows.extend(month_rows)

    lines.append("-- ============================================================")
    lines.append(f"-- Transacciones usuario {id_user} ({ccy}) — {len(rows)} filas")
    lines.append("-- ============================================================")
    lines.append("INSERT INTO `Transactions`")
    lines.append("    (`IdUser`, `Type`, `IdMainCategory`, `IdSubCategory`, `Description`,")
    lines.append("     `TransactionDate`, `OriginalAmount`, `OriginalCurrency`, `ExchangeRate`,")
    lines.append("     `BaseAmount`, `BaseCurrency`, `RateDate`, `CreatedAt`) VALUES")
    vals = []
    for (iu, ty, mc, sc, desc, d, amt) in rows:
        ds = d.isoformat()
        vals.append(
            f"({iu}, {ty}, {mc}, {sc}, {s(desc)}, {s(ds)}, {amt}, {s(ccy)}, 1.000000, "
            f"{amt}, {s(ccy)}, {s(ds)}, {s(ds + ' 09:00:00')})"
        )
    lines.append(",\n".join(vals) + ";")
    lines.append("")
    return len(rows)


def gen_valuations(lines, anchor):
    """Valoraciones día-1 mensuales desde el mes siguiente a la adquisición hasta la ancla."""
    acq = {}  # company_id -> (buy_date, buy_price, ccy)
    for cid, _sh, price, d in P1_HOLDINGS + P2_HOLDINGS:
        acq[cid] = (d, price, COMPANY_CCY[cid])

    vals = []
    for cid in sorted(acq.keys()):
        buy_date, buy_price, ccy = acq[cid]
        price = buy_price
        for vd in first_of_months(add_month(buy_date), anchor):
            factor = Decimal(str(random.uniform(0.96, 1.06)))
            price = q4(price * factor)
            if price <= 0:
                price = q4(buy_price)
            vals.append((cid, price, ccy, vd))

    lines.append("-- ============================================================")
    lines.append(f"-- Valoraciones mensuales (día-1) — {len(vals)} filas")
    lines.append("-- ============================================================")
    lines.append("INSERT INTO `Valuations`")
    lines.append("    (`IdCompany`, `Price`, `PriceCurrency`, `Date`, `Source`, `IdStatus`, `CreatedAt`) VALUES")
    rows = []
    for (cid, price, ccy, vd) in vals:
        ds = vd.isoformat()
        rows.append(f"({cid}, {price}, {s(ccy)}, {s(ds)}, 'seed-demo', 2, {s(ds + ' 00:00:00')})")
    lines.append(",\n".join(rows) + ";")
    lines.append("")
    return len(vals)


def gen_holdings_and_rates(lines, anchor):
    """Emite ExchangeRates (compras cruzadas), Portfolios y Holdings."""
    # 1. ExchangeRates de compras que cruzan moneda
    er_rows = []
    holdings_out = []  # (id_portfolio, cid, shares, buy_orig, orig_ccy, rate, base_amt, base_ccy, buy_date)

    def process(holdings, id_portfolio, base_ccy):
        for cid, shares, price, d in holdings:
            orig_ccy = COMPANY_CCY[cid]
            if orig_ccy == base_ccy:
                rate = Decimal("1.000000")
            else:
                rate = BUY_RATES[(orig_ccy, base_ccy, d)]
                er_rows.append((orig_ccy, base_ccy, rate, d))
            base_amt = q2(price * rate)
            holdings_out.append((id_portfolio, cid, shares, price, orig_ccy, rate, base_amt, base_ccy, d))

    process(P1_HOLDINGS, 1, "EUR")
    process(P2_HOLDINGS, 2, "USD")

    lines.append("-- ============================================================")
    lines.append(f"-- Tipos de cambio en fechas de compra (Source='seed-demo') — {len(er_rows)} filas")
    lines.append("-- ============================================================")
    lines.append("INSERT INTO `ExchangeRates`")
    lines.append("    (`FromCurrency`, `ToCurrency`, `Rate`, `RateDate`, `Source`, `FetchedAt`) VALUES")
    er_vals = []
    for (frm, to, rate, d) in er_rows:
        ds = d.isoformat()
        er_vals.append(f"({s(frm)}, {s(to)}, {rate}, {s(ds)}, 'seed-demo', {s(ds + ' 00:00:00')})")
    lines.append(",\n".join(er_vals) + ";")
    lines.append("")

    lines.append("-- ============================================================")
    lines.append("-- Portfolios: P1 EUR (user 1) creado 2024, P2 USD (user 2) creado 2025")
    lines.append("-- ============================================================")
    lines.append("INSERT INTO `Portfolios`")
    lines.append("    (`IdPortfolio`, `IdUser`, `Name`, `RealizedPnL`, `RealizedPnLCurrency`, `IdStatus`, `CreatedAt`) VALUES")
    lines.append("    (1, 1, 'Cartera Europa', 0.00, 'EUR', 2, '2024-01-15 10:00:00'),")
    lines.append("    (2, 2, 'US Tech Portfolio', 0.00, 'USD', 2, '2025-01-05 10:00:00');")
    lines.append("")

    lines.append("-- ============================================================")
    lines.append(f"-- Holdings — {len(holdings_out)} filas (8 en P1 EUR + 10 en P2 USD)")
    lines.append("-- ============================================================")
    lines.append("INSERT INTO `Holdings`")
    lines.append("    (`IdPortfolio`, `IdCompany`, `Shares`,")
    lines.append("     `BuyOriginalAmount`, `BuyOriginalCurrency`, `BuyExchangeRate`, `BuyBaseAmount`, `BuyBaseCurrency`, `BuyRateDate`,")
    lines.append("     `BuyDate`, `Notes`, `IdStatus`, `CreatedAt`) VALUES")
    h_vals = []
    for (idp, cid, shares, price, orig_ccy, rate, base_amt, base_ccy, d) in holdings_out:
        ds = d.isoformat()
        h_vals.append(
            f"({idp}, {cid}, {shares}, {price}, {s(orig_ccy)}, {rate}, {base_amt}, {s(base_ccy)}, {s(ds)}, "
            f"{s(ds)}, NULL, 2, {s(ds + ' 10:00:00')})"
        )
    lines.append(",\n".join(h_vals) + ";")
    lines.append("")
    return len(holdings_out), len(er_rows)


def main():
    anchor = date.fromisoformat(sys.argv[1]) if len(sys.argv) > 1 else date.today()
    anchor = date(anchor.year, anchor.month, 1)
    random.seed(SEED)

    lines = []
    lines.append("-- BigSchool-TFM: Datos de demostración (Seed) — GENERADO por generate_seed.py")
    lines.append(f"-- Ancla (actualidad): {anchor.isoformat()}. NO EDITAR A MANO: re-ejecutar el generador.")
    lines.append("-- Carga tras init.sql. Dos usuarios: demo@bigschool.com (EUR), demo.usd@bigschool.com (USD).")
    lines.append("")
    lines.append("USE `bigschool`;")
    lines.append("")

    # Usuarios (1 EUR, 2 USD); password Demo2026!
    lines.append("-- Usuarios demo (password Demo2026!)")
    lines.append("INSERT IGNORE INTO `Users`")
    lines.append("    (`IdUser`, `Email`, `PasswordHash`, `PasswordSalt`, `FullName`, `BaseCurrency`, `IdStatus`, `CreatedAt`) VALUES")
    lines.append(f"    (1, 'demo@bigschool.com', '{PWD_HASH}', '{PWD_SALT}', 'Usuario Demo EUR', 'EUR', 2, '2021-12-01 08:00:00'),")
    lines.append(f"    (2, 'demo.usd@bigschool.com', '{PWD_HASH}', '{PWD_SALT}', 'Usuario Demo USD', 'USD', 2, '2025-01-01 08:00:00');")
    lines.append("")

    # Companies 5-18
    lines.append("-- Empresas 5-18 (1-4 ya en init.sql)")
    lines.append("INSERT IGNORE INTO `Companies`")
    lines.append("    (`IdCompany`, `Name`, `Ticker`, `Sector`, `Market`, `Currency`, `IdStatus`, `CreatedAt`) VALUES")
    c_vals = []
    for (cid, name, ticker, sector, market, ccy) in COMPANIES:
        c_vals.append(f"    ({cid}, {s(name)}, {s(ticker)}, {s(sector)}, {s(market)}, {s(ccy)}, 2, '2024-01-01 00:00:00')")
    lines.append(",\n".join(c_vals) + ";")
    lines.append("")

    n_holdings, n_rates = gen_holdings_and_rates(lines, anchor)
    n_vals = gen_valuations(lines, anchor)
    n_tx_eur = gen_transactions(lines, 1, "EUR", date(2022, 1, 1), anchor)
    n_tx_usd = gen_transactions(lines, 2, "USD", date(2026, 1, 1), anchor)

    with open(OUT_PATH, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")

    print(f"seed.sql generado: usuarios=2 companies={len(COMPANIES)+4} holdings={n_holdings} "
          f"rates={n_rates} valuations={n_vals} tx_eur={n_tx_eur} tx_usd={n_tx_usd}")


if __name__ == "__main__":
    main()
```

- [x] **Step 2: Ejecutar el generador (regenera `seed.sql`)**

Run:
```bash
python infra/docker/mysql/generate_seed.py 2026-06-01
```
Expected: imprime una línea tipo `seed.sql generado: usuarios=2 companies=18 holdings=18 rates=6 valuations=... tx_eur=... tx_usd=...` y sobrescribe `infra/docker/mysql/seed.sql`.

- [x] **Step 3: Sanity check del SQL generado**

Run:
```bash
grep -c "INSERT INTO \`Transactions\`\|INSERT IGNORE INTO \`Users\`\|INSERT IGNORE INTO \`Companies\`\|INSERT INTO \`Holdings\`\|INSERT INTO \`Valuations\`\|INSERT INTO \`ExchangeRates\`\|INSERT INTO \`Portfolios\`" infra/docker/mysql/seed.sql
head -3 infra/docker/mysql/seed.sql
```
Expected: el `grep -c` devuelve `7` (las 7 sentencias INSERT); la cabecera muestra el comentario "GENERADO por generate_seed.py".

- [x] **Step 4: Commit**

```bash
git add infra/docker/mysql/generate_seed.py infra/docker/mysql/seed.sql
git commit -m "infra: generador determinista de seed demo (2 usuarios EUR/USD, 18 holdings)"
```

---

## Task 2: Verificación de integridad cargando en una BD desechable

**Files:** (ninguno — verificación)

- [x] **Step 1: Asegurar el MySQL dev levantado**

Run:
```bash
cd infra && docker compose --env-file .env up -d mysql && cd -
docker inspect -f "{{.State.Running}}" bigschool-mysql
```
Expected: imprime `true`.

- [x] **Step 2: Cargar `init.sql` + `seed.sql` en una BD desechable `bigschool_seedtest`**

Run:
```bash
PW="$(grep '^MYSQL_ROOT_PASSWORD=' infra/.env | cut -d= -f2-)"
{
  echo "DROP DATABASE IF EXISTS \`bigschool_seedtest\`;"
  echo "CREATE DATABASE \`bigschool_seedtest\` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"
  sed 's/USE `bigschool`/USE `bigschool_seedtest`/' infra/docker/mysql/init.sql
  sed 's/USE `bigschool`/USE `bigschool_seedtest`/' infra/docker/mysql/seed.sql
} | docker exec -i -e MYSQL_PWD="$PW" bigschool-mysql mysql -uroot && echo "CARGA OK"
```
Expected: imprime `CARGA OK` sin errores (ni FK, ni claves duplicadas, ni `Data too long`/parseo de enums).

- [x] **Step 3: Consultas de integridad**

Run:
```bash
PW="$(grep '^MYSQL_ROOT_PASSWORD=' infra/.env | cut -d= -f2-)"
docker exec -i -e MYSQL_PWD="$PW" bigschool-mysql mysql -uroot bigschool_seedtest -t <<'SQL'
SELECT (SELECT COUNT(*) FROM Users)        AS users,
       (SELECT COUNT(*) FROM Companies)    AS companies,
       (SELECT COUNT(*) FROM Portfolios)   AS portfolios,
       (SELECT COUNT(*) FROM Holdings)     AS holdings,
       (SELECT COUNT(*) FROM Transactions WHERE IdUser=1) AS tx_eur,
       (SELECT COUNT(*) FROM Transactions WHERE IdUser=2) AS tx_usd,
       (SELECT COUNT(*) FROM ExchangeRates WHERE FromCurrency='EUR' AND ToCurrency='USD') AS eur_usd_rates;
-- Sin holdings huérfanos (empresa inexistente)
SELECT COUNT(*) AS holdings_huerfanos
FROM Holdings h LEFT JOIN Companies c ON c.IdCompany=h.IdCompany WHERE c.IdCompany IS NULL;
-- Valoración de la cartera USD (id=2): cada holding debe tener Rate>0 y MarketValue>0
SELECT h.IdCompany, co.Currency,
       (SELECT v.Price FROM Valuations v WHERE v.IdCompany=h.IdCompany ORDER BY v.Date DESC, v.IdValuation DESC LIMIT 1) AS LastPrice,
       CASE WHEN co.Currency='USD' THEN 1
            ELSE COALESCE((SELECT er.Rate FROM ExchangeRates er
                           WHERE er.FromCurrency=co.Currency AND er.ToCurrency='USD'
                             AND er.RateDate <= (SELECT v.Date FROM Valuations v WHERE v.IdCompany=h.IdCompany ORDER BY v.Date DESC LIMIT 1)
                           ORDER BY er.RateDate DESC LIMIT 1), 0) END AS Rate
FROM Holdings h JOIN Companies co ON co.IdCompany=h.IdCompany
WHERE h.IdPortfolio=2
ORDER BY h.IdCompany;
SQL
```
Expected:
- Fila de recuentos: `users=2`, `companies=18`, `portfolios=2`, `holdings=18`, `tx_eur` ≥ 400, `tx_usd` ≥ 20, `eur_usd_rates=3`.
- `holdings_huerfanos=0`.
- En la última tabla, **todas** las filas tienen `LastPrice` no nulo y `Rate > 0` (las EUR usan la tasa `EUR→USD` sembrada; las USD tienen `Rate=1`).

- [x] **Step 4: Limpiar la BD desechable**

Run:
```bash
PW="$(grep '^MYSQL_ROOT_PASSWORD=' infra/.env | cut -d= -f2-)"
docker exec -i -e MYSQL_PWD="$PW" bigschool-mysql mysql -uroot -e "DROP DATABASE IF EXISTS \`bigschool_seedtest\`;"
```
Expected: sin salida (BD eliminada). No se ha tocado `bigschool` ni `bigschool_test`.

- [x] **Step 5: Commit (solo si la verificación obligó a retocar el generador)**

Si algún expected falló y corregiste `generate_seed.py`, regenera `seed.sql` (Task 1 Step 2) y commitea:
```bash
git add infra/docker/mysql/generate_seed.py infra/docker/mysql/seed.sql
git commit -m "infra: ajustes en el generador de seed tras verificación de integridad"
```
Si todo pasó a la primera, omitir este commit.

---

## Self-Review (verificación del autor del plan)

- **Cobertura de la spec (Bloque 2):** generador determinista que **emite** `seed.sql` (Task 1) ✓; dos usuarios EUR/USD con el modelo `RealizedPnLCurrency` = base del usuario ✓; transacciones 2022→ancla (EUR) y 2026→ancla (USD), ~8-12/mes y ~4-6/mes ✓; 18 companies (1-4 init.sql + 5-18 generadas), valores de enum válidos ✓; P1 EUR (8 holdings, compras 2024) y P2 USD (10 holdings, compras 2025) con día-1 ✓; ExchangeRates en cada compra que cruza moneda ✓; valuations día-1 mensuales desde la adquisición hasta la ancla ✓; sin Disposals ✓. Verificación de carga + integridad + valoración multimoneda no-cero (Task 2) ✓.
- **Sin placeholders:** el generador se da completo y ejecutable; los comandos de verificación son concretos con resultados esperados.
- **Consistencia de tipos/nombres:** enums `Market`/`Sector`/`Currency` usan nombres válidos verificados; IDs de SubCategory mapeados a su MainCategory correcto; `BuyBaseAmount` con redondeo banker's (`ROUND_HALF_EVEN`, espejo de `Money.Create`); `RealizedPnLCurrency`/`BuyBaseCurrency` coherentes con la base de cada usuario (EUR/USD); pares `EUR→USD` sembrados garantizan `Rate>0` en la valoración de P2 (acorde a `HOLDING_VALUATION`).
- **Desviaciones flagueadas** (LVMH/Airbus por enum `Market`; tasas solo en fechas de compra) documentadas arriba para confirmación en revisión.

## Notas para bloques siguientes
- El Bloque 3 (compose completo) montará `bigschool` con `init.sql` + este `seed.sql` (el `docker-compose.override.yml` ya monta `seed.sql` como `02-seed.sql`); su verificación reutiliza estas consultas de integridad sobre la BD real `bigschool`.
- La ancla es parametrizable: para refrescar "la actualidad" basta re-ejecutar `generate_seed.py` y commitear el `seed.sql` regenerado.
