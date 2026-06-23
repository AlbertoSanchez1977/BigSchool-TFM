-- BigSchool-TFM: Datos de demostración (Seed)
-- 3 meses de datos fake (marzo-mayo 2026) para que el profesor pueda evaluar.
--
-- ============================================================
-- EJECUTAR DESPUÉS DE:
--   1. docker-compose up -d mysql               (init.sql crea el schema)
--   2. dotnet run --project src/backend/...     (app arranca; EF Core no toca nada)
--   3. POST /api/v1/auth/register               → crea demo@bigschool.com (IdUser=1)
--
-- Desde MySQL Workbench o CLI:
--   mysql -u bigschool -p bigschool < infra/docker/mysql/seed.sql
-- ============================================================

USE `bigschool`;

-- ============================================================
-- Empresas adicionales (Plan 3A: BC Inversiones)
-- EF Core ya siembra IdCompany 1-4 vía migración CreateCompanies:
--   1=AAPL (USD/NASDAQ)  2=MSFT (USD/NASDAQ)
--   3=SAN  (EUR/BME)     4=SHEL (GBP/LSE)
-- ============================================================
INSERT IGNORE INTO `Companies` (`IdCompany`, `Name`, `Ticker`, `Sector`, `Market`, `Currency`) VALUES
    (5, 'NVIDIA Corporation', 'NVDA', 'Technology', 'NASDAQ', 'USD'),
    (6, 'Inditex',            'ITX',  'Retail',      'BME',   'EUR'),
    (7, 'Iberdrola',          'IBE',  'Energy',       'BME',  'EUR'),
    (8, 'Amazon.com Inc.',    'AMZN', 'Technology',  'NASDAQ', 'USD'),
    (9, 'Tesla Inc.',         'TSLA', 'Automotive',  'NASDAQ', 'USD');

-- ============================================================
-- Tipos de cambio para las fechas de compra multimoneda (USD→EUR)
-- Se usan al calcular BuyBaseAmount de los Holdings en USD.
-- Source='seed-demo' (≠ 'seed') → ResetAsync los elimina entre tests; aquí son datos de demo.
-- ============================================================
INSERT IGNORE INTO `ExchangeRates`
    (`FromCurrency`, `ToCurrency`, `Rate`, `RateDate`, `Source`, `FetchedAt`) VALUES
    ('USD', 'EUR', 0.918000, '2026-01-20', 'seed-demo', '2026-01-20 00:00:00'),
    ('USD', 'EUR', 0.921000, '2026-02-10', 'seed-demo', '2026-02-10 00:00:00'),
    ('USD', 'EUR', 0.918000, '2026-02-15', 'seed-demo', '2026-02-15 00:00:00'),
    ('USD', 'EUR', 0.922000, '2026-03-01', 'seed-demo', '2026-03-01 00:00:00'),
    ('USD', 'EUR', 0.915000, '2026-04-05', 'seed-demo', '2026-04-05 00:00:00'),
    ('USD', 'EUR', 0.920000, '2026-06-01', 'seed-demo', '2026-06-01 00:00:00'),
    ('GBP', 'EUR', 1.168000, '2026-01-01', 'seed-demo', '2026-01-01 00:00:00');

-- ============================================================
-- Portfolio del usuario demo (IdUser=1 = primer usuario registrado vía API)
-- RealizedPnL y RealizedPnLCurrency son obligatorios (NOT NULL, sin DEFAULT en EF).
-- Se inicializan a 0/EUR; se irán actualizando vía API cuando el profesor ejecute ventas.
-- ============================================================
INSERT INTO `Portfolios`
    (`IdPortfolio`, `IdUser`, `Name`, `RealizedPnL`, `RealizedPnLCurrency`, `IdStatus`, `CreatedAt`) VALUES
    (1, 1, 'Mi cartera principal', 0.00, 'EUR', 2, '2026-01-15 10:00:00');

-- ============================================================
-- Posiciones abiertas — Holdings con MoneyConversion snapshot (Buy* columns)
-- BuyBaseAmount = Math.Round(BuyOriginalAmount × BuyExchangeRate, 2, ToEven) [Money.Create]
--
-- Empresas:  AAPL=1 (USD)  MSFT=2 (USD)  SAN=3 (EUR)  SHEL=4 (GBP)
--            NVDA=5 (USD)  ITX=6 (EUR)   IBE=7 (EUR)
-- ============================================================
INSERT INTO `Holdings`
    (`IdPortfolio`, `IdCompany`, `Shares`,
     `BuyOriginalAmount`, `BuyOriginalCurrency`, `BuyExchangeRate`, `BuyBaseAmount`, `BuyBaseCurrency`, `BuyRateDate`,
     `BuyDate`, `Notes`, `IdStatus`, `CreatedAt`) VALUES
    --  AAPL: 10 acc @ 178.50 USD  →  178.50 × 0.918 = 163.80 EUR  (BuyDate=BuyRateDate)
    (1, 1, 10.0000, 178.5000, 'USD', 0.918000, 163.80, 'EUR', '2026-02-15', '2026-02-15', 'Compra inicial Apple',      2, '2026-02-15 10:00:00'),
    --  MSFT:  5 acc @ 415.20 USD  →  415.20 × 0.922 = 382.81 EUR
    (1, 2,  5.0000, 415.2000, 'USD', 0.922000, 382.81, 'EUR', '2026-03-01', '2026-03-01', 'Microsoft a buen precio',   2, '2026-03-01 10:00:00'),
    --  ITX:  25 acc @  38.45 EUR  →  rate=1 (misma moneda)
    (1, 6, 25.0000,  38.4500, 'EUR', 1.000000,  38.45, 'EUR', '2026-01-20', '2026-01-20', 'Inditex pre-resultados',    2, '2026-01-20 10:00:00'),
    --  IBE:  50 acc @  12.80 EUR  →  rate=1
    (1, 7, 50.0000,  12.8000, 'EUR', 1.000000,  12.80, 'EUR', '2026-02-10', '2026-02-10', 'Iberdrola dividendo',       2, '2026-02-10 10:00:00'),
    --  NVDA:  3 acc @ 890.00 USD  →  890.00 × 0.915 = 814.35 EUR
    (1, 5,  3.0000, 890.0000, 'USD', 0.915000, 814.35, 'EUR', '2026-04-05', '2026-04-05', 'NVIDIA IA',                 2, '2026-04-05 10:00:00');

-- ============================================================
-- Valoraciones históricas demo (marzo-junio 2026) — 9 empresas × 7 fechas = 63 filas
-- EF Core ya siembra IdValuation 1-8 (fechas 2026-01-02 y 2026-03-02 para empresas 1-4).
-- Las fechas de abajo no solapan con las del seed EF (único constraint: IdCompany+Date).
-- ============================================================
INSERT INTO `Valuations` (`IdCompany`, `Price`, `PriceCurrency`, `Date`, `Source`) VALUES
    -- AAPL (USD) — EF seed: 195 @ 2026-01-02, 210 @ 2026-03-02
    (1, 175.2000, 'USD', '2026-03-01', 'Yahoo Finance'), (1, 180.5000, 'USD', '2026-03-15', 'Yahoo Finance'),
    (1, 182.3000, 'USD', '2026-04-01', 'Yahoo Finance'), (1, 188.7500, 'USD', '2026-04-15', 'Yahoo Finance'),
    (1, 191.0000, 'USD', '2026-05-01', 'Yahoo Finance'), (1, 195.4000, 'USD', '2026-05-15', 'Yahoo Finance'),
    (1, 193.2000, 'USD', '2026-06-01', 'Yahoo Finance'),
    -- MSFT (USD) — EF seed: 420 @ 2026-01-02, 440 @ 2026-03-02
    (2, 410.0000, 'USD', '2026-03-01', 'Yahoo Finance'), (2, 418.5000, 'USD', '2026-03-15', 'Yahoo Finance'),
    (2, 425.0000, 'USD', '2026-04-01', 'Yahoo Finance'), (2, 430.2000, 'USD', '2026-04-15', 'Yahoo Finance'),
    (2, 435.8000, 'USD', '2026-05-01', 'Yahoo Finance'), (2, 442.1000, 'USD', '2026-05-15', 'Yahoo Finance'),
    (2, 438.5000, 'USD', '2026-06-01', 'Yahoo Finance'),
    -- SAN (EUR) — EF seed: 4.5 @ 2026-01-02, 4.8 @ 2026-03-02
    (3,   4.6000, 'EUR', '2026-03-01', 'BME'), (3,   4.7500, 'EUR', '2026-03-15', 'BME'),
    (3,   4.9000, 'EUR', '2026-04-01', 'BME'), (3,   5.0500, 'EUR', '2026-04-15', 'BME'),
    (3,   5.1000, 'EUR', '2026-05-01', 'BME'), (3,   5.2000, 'EUR', '2026-05-15', 'BME'),
    (3,   5.1500, 'EUR', '2026-06-01', 'BME'),
    -- SHEL (GBP) — EF seed: 28 @ 2026-01-02, 30 @ 2026-03-02
    (4,  28.5000, 'GBP', '2026-03-01', 'LSE'), (4,  29.0000, 'GBP', '2026-03-15', 'LSE'),
    (4,  29.5000, 'GBP', '2026-04-01', 'LSE'), (4,  30.2000, 'GBP', '2026-04-15', 'LSE'),
    (4,  30.8000, 'GBP', '2026-05-01', 'LSE'), (4,  31.5000, 'GBP', '2026-05-15', 'LSE'),
    (4,  31.0000, 'GBP', '2026-06-01', 'LSE'),
    -- NVDA (USD)
    (5, 880.0000, 'USD', '2026-03-01', 'Yahoo Finance'), (5, 895.0000, 'USD', '2026-03-15', 'Yahoo Finance'),
    (5, 910.5000, 'USD', '2026-04-01', 'Yahoo Finance'), (5, 925.0000, 'USD', '2026-04-15', 'Yahoo Finance'),
    (5, 940.0000, 'USD', '2026-05-01', 'Yahoo Finance'), (5, 960.5000, 'USD', '2026-05-15', 'Yahoo Finance'),
    (5, 955.0000, 'USD', '2026-06-01', 'Yahoo Finance'),
    -- ITX/Inditex (EUR)
    (6,  37.8000, 'EUR', '2026-03-01', 'BME'), (6,  39.1000, 'EUR', '2026-03-15', 'BME'),
    (6,  40.2500, 'EUR', '2026-04-01', 'BME'), (6,  41.5000, 'EUR', '2026-04-15', 'BME'),
    (6,  42.0000, 'EUR', '2026-05-01', 'BME'), (6,  43.2000, 'EUR', '2026-05-15', 'BME'),
    (6,  42.8000, 'EUR', '2026-06-01', 'BME'),
    -- IBE/Iberdrola (EUR)
    (7,  12.5000, 'EUR', '2026-03-01', 'BME'), (7,  12.7500, 'EUR', '2026-03-15', 'BME'),
    (7,  13.0000, 'EUR', '2026-04-01', 'BME'), (7,  13.2000, 'EUR', '2026-04-15', 'BME'),
    (7,  13.5000, 'EUR', '2026-05-01', 'BME'), (7,  13.8000, 'EUR', '2026-05-15', 'BME'),
    (7,  14.0000, 'EUR', '2026-06-01', 'BME'),
    -- AMZN (USD)
    (8, 185.0000, 'USD', '2026-03-01', 'Yahoo Finance'), (8, 192.5000, 'USD', '2026-03-15', 'Yahoo Finance'),
    (8, 198.0000, 'USD', '2026-04-01', 'Yahoo Finance'), (8, 205.0000, 'USD', '2026-04-15', 'Yahoo Finance'),
    (8, 210.5000, 'USD', '2026-05-01', 'Yahoo Finance'), (8, 215.0000, 'USD', '2026-05-15', 'Yahoo Finance'),
    (8, 212.0000, 'USD', '2026-06-01', 'Yahoo Finance'),
    -- TSLA (USD)
    (9, 285.0000, 'USD', '2026-03-01', 'Yahoo Finance'), (9, 275.0000, 'USD', '2026-03-15', 'Yahoo Finance'),
    (9, 290.0000, 'USD', '2026-04-01', 'Yahoo Finance'), (9, 305.0000, 'USD', '2026-04-15', 'Yahoo Finance'),
    (9, 320.0000, 'USD', '2026-05-01', 'Yahoo Finance'), (9, 315.0000, 'USD', '2026-05-15', 'Yahoo Finance'),
    (9, 330.0000, 'USD', '2026-06-01', 'Yahoo Finance');
-- ── Total valoraciones demo: 63 filas (9 empresas × 7 fechas) ──────────────

-- ============================================================
-- Transacciones: 3 meses de datos (marzo-mayo 2026)
-- Schema nuevo vs viejo init.sql:
--   · Type SMALLINT: 0=Income (TransactionType.Income), 1=Expense (TransactionType.Expense)
--   · TransactionDate en lugar de Date
--   · OriginalAmount/OriginalCurrency/ExchangeRate/BaseAmount/BaseCurrency/RateDate
--     en lugar de Amount. Demo 100% EUR → rate=1.000000, BaseAmount=OriginalAmount.
--   · Sin IsRecurrent, sin RecurrencePeriod
--   · IDs SubCategory según EF Core InitialCreate migration (≠ viejo init.sql):
--     ONG=19, Restaurantes=11, Ocio=12, Viajes=13, Ropa=14, Tecnología=15,
--     Cursos=16, Libros=17, Máster=18, Hipoteca=20, Empresa principal=22,
--     Vivienda=23, Acciones nac=26, Acciones int=27, Otros ingresos=28
-- ============================================================
INSERT INTO `Transactions`
    (`IdUser`, `Type`, `IdMainCategory`, `IdSubCategory`, `Description`,
     `TransactionDate`, `OriginalAmount`, `OriginalCurrency`, `ExchangeRate`,
     `BaseAmount`, `BaseCurrency`, `RateDate`, `CreatedAt`) VALUES

-- ── Marzo 2026: Gastos ──────────────────────────────────────────────────────
(1, 1,  1,  1, 'Compra semanal Mercadona', '2026-03-02', 185.50, 'EUR', 1.000000, 185.50, 'EUR', '2026-03-02', '2026-03-02 08:00:00'),
(1, 1,  1,  1, 'Compra semanal Mercadona', '2026-03-09',  92.30, 'EUR', 1.000000,  92.30, 'EUR', '2026-03-09', '2026-03-09 08:00:00'),
(1, 1,  1,  3, 'Factura luz marzo',        '2026-03-05', 120.00, 'EUR', 1.000000, 120.00, 'EUR', '2026-03-05', '2026-03-05 09:00:00'),
(1, 1,  1,  3, 'Factura agua',             '2026-03-10',  45.00, 'EUR', 1.000000,  45.00, 'EUR', '2026-03-10', '2026-03-10 09:00:00'),
(1, 1,  1,  3, 'Factura internet',         '2026-03-08',  55.00, 'EUR', 1.000000,  55.00, 'EUR', '2026-03-08', '2026-03-08 09:00:00'),
(1, 1,  1,  4, 'Seguro coche',             '2026-03-15', 280.00, 'EUR', 1.000000, 280.00, 'EUR', '2026-03-15', '2026-03-15 10:00:00'),
(1, 1,  1,  5, 'Gasolina',                 '2026-03-12',  50.00, 'EUR', 1.000000,  50.00, 'EUR', '2026-03-12', '2026-03-12 07:00:00'),
(1, 1,  1,  2, 'Farmacia',                 '2026-03-25',  18.50, 'EUR', 1.000000,  18.50, 'EUR', '2026-03-25', '2026-03-25 10:00:00'),
(1, 1,  5, 11, 'Cena restaurante',         '2026-03-14',  85.00, 'EUR', 1.000000,  85.00, 'EUR', '2026-03-14', '2026-03-14 21:00:00'),
(1, 1,  5, 12, 'Cine + palomitas',         '2026-03-16',  35.00, 'EUR', 1.000000,  35.00, 'EUR', '2026-03-16', '2026-03-16 19:00:00'),
(1, 1,  5, 14, 'Zapatillas running',       '2026-03-20', 120.00, 'EUR', 1.000000, 120.00, 'EUR', '2026-03-20', '2026-03-20 11:00:00'),
(1, 1,  6, 16, 'Curso Udemy',              '2026-03-22',  29.99, 'EUR', 1.000000,  29.99, 'EUR', '2026-03-22', '2026-03-22 15:00:00'),
(1, 1,  7, 20, 'Hipoteca marzo',           '2026-03-01', 650.00, 'EUR', 1.000000, 650.00, 'EUR', '2026-03-01', '2026-03-01 08:00:00'),

-- ── Marzo 2026: Ingresos ────────────────────────────────────────────────────
(1, 0, 10, 22, 'Nómina marzo',             '2026-03-28', 2850.00, 'EUR', 1.000000, 2850.00, 'EUR', '2026-03-28', '2026-03-28 00:00:00'),
(1, 0, 13, 28, 'Proyecto freelance web',   '2026-03-15',  350.00, 'EUR', 1.000000,  350.00, 'EUR', '2026-03-15', '2026-03-15 12:00:00'),

-- ── Abril 2026: Gastos ──────────────────────────────────────────────────────
(1, 1,  1,  1, 'Compra semanal',           '2026-04-01', 175.20, 'EUR', 1.000000, 175.20, 'EUR', '2026-04-01', '2026-04-01 09:00:00'),
(1, 1,  1,  1, 'Compra semanal',           '2026-04-08',  88.90, 'EUR', 1.000000,  88.90, 'EUR', '2026-04-08', '2026-04-08 09:00:00'),
(1, 1,  1,  1, 'Compra semanal',           '2026-04-15', 165.00, 'EUR', 1.000000, 165.00, 'EUR', '2026-04-15', '2026-04-15 09:00:00'),
(1, 1,  1,  1, 'Compra semanal',           '2026-04-22', 110.50, 'EUR', 1.000000, 110.50, 'EUR', '2026-04-22', '2026-04-22 09:00:00'),
(1, 1,  1,  3, 'Factura luz abril',        '2026-04-05', 115.00, 'EUR', 1.000000, 115.00, 'EUR', '2026-04-05', '2026-04-05 09:00:00'),
(1, 1,  1,  3, 'Factura internet',         '2026-04-08',  55.00, 'EUR', 1.000000,  55.00, 'EUR', '2026-04-08', '2026-04-08 09:00:00'),
(1, 1,  1,  5, 'Gasolina',                 '2026-04-10',  55.00, 'EUR', 1.000000,  55.00, 'EUR', '2026-04-10', '2026-04-10 07:00:00'),
(1, 1,  1,  5, 'Gasolina',                 '2026-04-24',  48.00, 'EUR', 1.000000,  48.00, 'EUR', '2026-04-24', '2026-04-24 07:00:00'),
(1, 1,  5, 11, 'Comida fuera',             '2026-04-12',  62.00, 'EUR', 1.000000,  62.00, 'EUR', '2026-04-12', '2026-04-12 14:00:00'),
(1, 1,  5, 13, 'Escapada fin de semana',   '2026-04-18', 450.00, 'EUR', 1.000000, 450.00, 'EUR', '2026-04-18', '2026-04-18 08:00:00'),
(1, 1,  5, 15, 'Auriculares bluetooth',    '2026-04-20',  89.99, 'EUR', 1.000000,  89.99, 'EUR', '2026-04-20', '2026-04-20 11:00:00'),
(1, 1,  6, 17, 'Libro técnico',            '2026-04-14',  25.00, 'EUR', 1.000000,  25.00, 'EUR', '2026-04-14', '2026-04-14 16:00:00'),
(1, 1,  7, 20, 'Hipoteca abril',           '2026-04-01', 650.00, 'EUR', 1.000000, 650.00, 'EUR', '2026-04-01', '2026-04-01 08:00:00'),
(1, 1,  4, 19, 'Donación Cruz Roja',       '2026-04-25',  30.00, 'EUR', 1.000000,  30.00, 'EUR', '2026-04-25', '2026-04-25 12:00:00'),

-- ── Abril 2026: Ingresos ────────────────────────────────────────────────────
(1, 0, 10, 22, 'Nómina abril',             '2026-04-28', 2850.00, 'EUR', 1.000000, 2850.00, 'EUR', '2026-04-28', '2026-04-28 00:00:00'),
(1, 0, 12, 26, 'Dividendo Iberdrola',      '2026-04-10',  125.00, 'EUR', 1.000000,  125.00, 'EUR', '2026-04-10', '2026-04-10 09:00:00'),
(1, 0, 11, 23, 'Alquiler garaje',          '2026-04-05',  600.00, 'EUR', 1.000000,  600.00, 'EUR', '2026-04-05', '2026-04-05 09:00:00'),

-- ── Mayo 2026: Gastos ───────────────────────────────────────────────────────
(1, 1,  1,  1, 'Compra semanal',           '2026-05-01', 195.00, 'EUR', 1.000000, 195.00, 'EUR', '2026-05-01', '2026-05-01 09:00:00'),
(1, 1,  1,  1, 'Compra semanal',           '2026-05-08', 105.30, 'EUR', 1.000000, 105.30, 'EUR', '2026-05-08', '2026-05-08 09:00:00'),
(1, 1,  1,  1, 'Compra semanal',           '2026-05-15', 142.80, 'EUR', 1.000000, 142.80, 'EUR', '2026-05-15', '2026-05-15 09:00:00'),
(1, 1,  1,  1, 'Compra semanal',           '2026-05-22',  98.50, 'EUR', 1.000000,  98.50, 'EUR', '2026-05-22', '2026-05-22 09:00:00'),
(1, 1,  1,  3, 'Factura luz mayo',         '2026-05-05', 125.00, 'EUR', 1.000000, 125.00, 'EUR', '2026-05-05', '2026-05-05 09:00:00'),
(1, 1,  1,  3, 'Factura internet',         '2026-05-08',  55.00, 'EUR', 1.000000,  55.00, 'EUR', '2026-05-08', '2026-05-08 09:00:00'),
(1, 1,  1,  3, 'Factura móvil',            '2026-05-10',  42.00, 'EUR', 1.000000,  42.00, 'EUR', '2026-05-10', '2026-05-10 09:00:00'),
(1, 1,  1,  5, 'Gasolina',                 '2026-05-06',  52.00, 'EUR', 1.000000,  52.00, 'EUR', '2026-05-06', '2026-05-06 07:00:00'),
(1, 1,  1,  2, 'Ibuprofeno',               '2026-05-14',  12.80, 'EUR', 1.000000,  12.80, 'EUR', '2026-05-14', '2026-05-14 10:00:00'),
(1, 1,  2,  6, 'Aportación fondo indexado','2026-05-15', 500.00, 'EUR', 1.000000, 500.00, 'EUR', '2026-05-15', '2026-05-15 09:00:00'),
(1, 1,  5, 11, 'Cena cumpleaños amigo',    '2026-05-12',  95.00, 'EUR', 1.000000,  95.00, 'EUR', '2026-05-12', '2026-05-12 21:00:00'),
(1, 1,  5, 12, 'Netflix + Spotify',        '2026-05-01',  28.00, 'EUR', 1.000000,  28.00, 'EUR', '2026-05-01', '2026-05-01 00:00:00'),
(1, 1,  5, 14, 'Camisetas verano',         '2026-05-18',  65.00, 'EUR', 1.000000,  65.00, 'EUR', '2026-05-18', '2026-05-18 11:00:00'),
(1, 1,  6, 16, 'Matrícula curso online',   '2026-05-20', 150.00, 'EUR', 1.000000, 150.00, 'EUR', '2026-05-20', '2026-05-20 10:00:00'),
(1, 1,  7, 20, 'Hipoteca mayo',            '2026-05-01', 650.00, 'EUR', 1.000000, 650.00, 'EUR', '2026-05-01', '2026-05-01 08:00:00'),

-- ── Mayo 2026: Ingresos ─────────────────────────────────────────────────────
(1, 0, 10, 22, 'Nómina mayo',              '2026-05-28', 2850.00, 'EUR', 1.000000, 2850.00, 'EUR', '2026-05-28', '2026-05-28 00:00:00'),
(1, 0, 11, 23, 'Alquiler garaje',          '2026-05-05',  600.00, 'EUR', 1.000000,  600.00, 'EUR', '2026-05-05', '2026-05-05 09:00:00'),
(1, 0, 12, 27, 'Dividendo Apple',          '2026-05-15',   85.00, 'EUR', 1.000000,   85.00, 'EUR', '2026-05-15', '2026-05-15 09:00:00'),
(1, 0, 13, 28, 'Venta mueble Wallapop',    '2026-05-20',  200.00, 'EUR', 1.000000,  200.00, 'EUR', '2026-05-20', '2026-05-20 16:00:00');
-- ── Total: 51 transacciones (38 gastos + 13 ingresos) ──────────────────────
