# Normalización infra/docker/mysql + HTTP Test Stack — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Alinear `init.sql` y `seed.sql` con el schema real de EF Core (Plans 2A + 2B completos) y crear `docs/swagger/bigschool-api.http` con las peticiones pre-rellenadas para verificación E2E manual.

**Architecture:** `init.sql` contiene el schema SQL completo en estado final + pre-popula `__EFMigrationsHistory` para que `dotnet run` no entre en conflicto con EF Core. `seed.sql` es un script manual que se ejecuta UNA VEZ después de registrar el usuario demo vía API. `.http` cubre Auth + Transactions + Categories en formato VS Code REST Client.

**Tech Stack:** MySQL 8, SQL DDL/DML puro, VS Code REST Client (extensión `humao.rest-client`).

---

## Contexto y decisiones de diseño

### Por qué init.sql estaba roto

La tabla `Transactions` en el viejo `init.sql` usaba el schema anterior:
- `Type ENUM('INCOME','EXPENSE')` → el código C# espera `SMALLINT` (`TransactionType.Income=0, Expense=1`)
- Columna `Amount` → ahora son 6 columnas de conversión multimoneda (`OriginalAmount`, `OriginalCurrency`, `ExchangeRate`, `BaseAmount`, `BaseCurrency`, `RateDate`)
- Columna `Date` → ahora `TransactionDate`
- Columnas `IsRecurrent`, `RecurrencePeriod` → eliminadas del dominio

Al arrancar `dotnet run` con el viejo `init.sql`, EF Core no encontraba `__EFMigrationsHistory` y ejecutaba `InitialCreate` (`CREATE TABLE Users`) sobre una tabla que ya existía → error.

### Solución adoptada

`init.sql` crea el schema completo en estado final + inserta las 4 filas en `__EFMigrationsHistory`. EF Core detecta que no hay migraciones pendientes y arranca sin tocar nada.

`seed.sql` no puede vivir en `docker-entrypoint-initdb.d/` porque inserta transacciones con `IdUser=1` que todavía no existe (el usuario se crea vía API). Es un script manual.

### Mapeo SubCategory IDs (viejo init.sql → EF Core InitialCreate migration)

La migration `InitialCreate` usa IDs explícitos 1-28 con orden diferente al viejo init.sql:

| ID nuevo (EF) | Nombre | ID viejo |
|:---:|---|:---:|
| 11 | Restaurantes | 13 |
| 12 | Ocio | 14 |
| 13 | Viajes | 15 |
| 14 | Ropa | 16 |
| 15 | Tecnología | 17 |
| 16 | Cursos | 18 |
| 17 | Libros | 19 |
| 18 | Máster | 20 |
| 19 | ONG | 11 |
| 20 | Hipoteca | 21 |
| 21 | Préstamo | 22 |
| 22 | Empresa principal | 23 |
| 23 | Vivienda | 25 |
| 24 | Local | 26 |
| 25 | Garaje | 27 |
| 26 | Acciones nacionales | 28 |
| 27 | Acciones internacionales | 29 |
| 28 | Otros ingresos | 30/31 (consolidados) |

IDs eliminados (sin equivalente en EF Core): Particulares(12), Empresa secundaria(24), Freelance(30), Ventas(31).

### Workflow de verificación E2E tras completar el plan

```
1. docker-compose -f infra/docker-compose.yml -f infra/docker-compose.override.yml up -d mysql
2. dotnet run --project src/backend/src/BigSchool.WebApi/BigSchool.WebApi.csproj
   → EF Core ve __EFMigrationsHistory con 4 entradas → no ejecuta ninguna migración
   → app en http://localhost:5285
3. VS Code → abrir docs/swagger/bigschool-api.http
4. Ejecutar "01 Registro" → crea demo@bigschool.com (IdUser=1)
5. Ejecutar "02 Login" → copiar accessToken → pegar en @token del .http
6. MySQL Workbench / CLI → ejecutar infra/docker/mysql/seed.sql
   → inserta 51 transacciones + Companies + Holdings + Valuations para IdUser=1
7. Probar el resto de endpoints del .http
```

---

## Archivos

| Acción | Archivo |
|---|---|
| Modificar | `infra/docker/mysql/init.sql` |
| Modificar | `infra/docker/mysql/seed.sql` |
| Crear | `docs/swagger/bigschool-api.http` |

---

### Task 1: Reescribir `init.sql`

**Files:**
- Modify: `infra/docker/mysql/init.sql`

- [ ] **Step 1: Reemplazar el contenido completo de `infra/docker/mysql/init.sql`**

```sql
-- BigSchool-TFM: Inicialización de Base de Datos
-- ESTRATEGIA: Este script crea el schema COMPLETO en estado final (Plans 2A + 2B) y registra
-- las 4 migraciones EF Core en __EFMigrationsHistory para que dotnet run arranque sin conflictos.
--
-- En docker-compose la BD 'bigschool' la crea MySQL via MYSQL_DATABASE.
-- Ejecución manual: descomenta la siguiente línea.
-- CREATE DATABASE IF NOT EXISTS `bigschool` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

USE `bigschool`;

-- ============================================================
-- EF Core migrations history
-- Permite que dotnet run / MigrateAsync() no entre en conflicto con las tablas ya creadas.
-- ============================================================
CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId`    VARCHAR(150) CHARACTER SET utf8mb4 NOT NULL,
    `ProductVersion` VARCHAR(32)  CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK___EFMigrationsHistory` PRIMARY KEY (`MigrationId`)
) CHARACTER SET utf8mb4;

INSERT IGNORE INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`) VALUES
    ('20260610163310_InitialCreate',             '8.0.11'),
    ('20260619182857_AlterUsersAddBaseCurrency', '8.0.11'),
    ('20260620054007_CreateExchangeRates',        '8.0.11'),
    ('20260620085001_CreateTransactions',         '8.0.11');

-- ============================================================
-- Tabla: Users
-- EF Core: InitialCreate + AlterUsersAddBaseCurrency
-- BaseCurrency CHAR(3) DEFAULT 'EUR' — Currency enum almacenado como string ISO 4217
-- ============================================================
CREATE TABLE IF NOT EXISTS `Users` (
    `IdUser`        INT          AUTO_INCREMENT,
    `Email`         VARCHAR(255) NOT NULL,
    `PasswordHash`  VARCHAR(512) NOT NULL,
    `PasswordSalt`  VARCHAR(256) NOT NULL,
    `FullName`      VARCHAR(200) NOT NULL,
    `BaseCurrency`  CHAR(3)      NOT NULL DEFAULT 'EUR',
    `LastLoginDate` DATETIME(6)  NULL,
    `IdStatus`      SMALLINT     NOT NULL DEFAULT 2,
    `CreatedAt`     DATETIME(6)  NOT NULL,
    `UpdatedAt`     DATETIME(6)  NULL,
    PRIMARY KEY (`IdUser`),
    UNIQUE KEY `IX_Users_Email` (`Email`)
) CHARACTER SET utf8mb4;

-- ============================================================
-- Tabla: SubCategories
-- EF Core: InitialCreate + HasData (28 subcategorías globales, IDs 1-28 explícitos)
-- ATENCIÓN: El orden de IDs difiere del viejo init.sql (Lujos empiezan en 11, ONG en 19).
-- ============================================================
CREATE TABLE IF NOT EXISTS `SubCategories` (
    `IdSubCategory`  INT          AUTO_INCREMENT,
    `IdMainCategory` INT          NOT NULL,
    `Name`           VARCHAR(100) NOT NULL,
    `IsDefault`      TINYINT(1)   NOT NULL DEFAULT 0,
    `IdStatus`       SMALLINT     NOT NULL DEFAULT 2,
    `CreatedAt`      DATETIME(6)  NOT NULL,
    `IdUser`         INT          NULL,
    PRIMARY KEY (`IdSubCategory`),
    KEY `IX_SubCategories_IdUser` (`IdUser`),
    CONSTRAINT `FK_SubCategories_Users_IdUser`
        FOREIGN KEY (`IdUser`) REFERENCES `Users` (`IdUser`) ON DELETE CASCADE
) CHARACTER SET utf8mb4;

-- Espejo exacto de EF Core HasData (InitialCreate migration, timestamp 2026-01-01 UTC)
INSERT IGNORE INTO `SubCategories`
    (`IdSubCategory`, `IdMainCategory`, `Name`, `IsDefault`, `IdStatus`, `CreatedAt`, `IdUser`) VALUES
    ( 1,  1, 'Supermercado',             1, 2, '2026-01-01 00:00:00', NULL),
    ( 2,  1, 'Farmacia',                 1, 2, '2026-01-01 00:00:00', NULL),
    ( 3,  1, 'Facturas',                 1, 2, '2026-01-01 00:00:00', NULL),
    ( 4,  1, 'Seguros',                  1, 2, '2026-01-01 00:00:00', NULL),
    ( 5,  1, 'Transporte',               1, 2, '2026-01-01 00:00:00', NULL),
    ( 6,  2, 'Bolsa',                    1, 2, '2026-01-01 00:00:00', NULL),
    ( 7,  2, 'Fondos',                   1, 2, '2026-01-01 00:00:00', NULL),
    ( 8,  2, 'Crypto',                   1, 2, '2026-01-01 00:00:00', NULL),
    ( 9,  3, 'Cuenta ahorro',            1, 2, '2026-01-01 00:00:00', NULL),
    (10,  3, 'Depósitos',                1, 2, '2026-01-01 00:00:00', NULL),
    (11,  5, 'Restaurantes',             1, 2, '2026-01-01 00:00:00', NULL),
    (12,  5, 'Ocio',                     1, 2, '2026-01-01 00:00:00', NULL),
    (13,  5, 'Viajes',                   1, 2, '2026-01-01 00:00:00', NULL),
    (14,  5, 'Ropa',                     1, 2, '2026-01-01 00:00:00', NULL),
    (15,  5, 'Tecnología',               1, 2, '2026-01-01 00:00:00', NULL),
    (16,  6, 'Cursos',                   1, 2, '2026-01-01 00:00:00', NULL),
    (17,  6, 'Libros',                   1, 2, '2026-01-01 00:00:00', NULL),
    (18,  6, 'Máster',                   1, 2, '2026-01-01 00:00:00', NULL),
    (19,  4, 'ONG',                      1, 2, '2026-01-01 00:00:00', NULL),
    (20,  7, 'Hipoteca',                 1, 2, '2026-01-01 00:00:00', NULL),
    (21,  7, 'Préstamo',                 1, 2, '2026-01-01 00:00:00', NULL),
    (22, 10, 'Empresa principal',        1, 2, '2026-01-01 00:00:00', NULL),
    (23, 11, 'Vivienda',                 1, 2, '2026-01-01 00:00:00', NULL),
    (24, 11, 'Local',                    1, 2, '2026-01-01 00:00:00', NULL),
    (25, 11, 'Garaje',                   1, 2, '2026-01-01 00:00:00', NULL),
    (26, 12, 'Acciones nacionales',      1, 2, '2026-01-01 00:00:00', NULL),
    (27, 12, 'Acciones internacionales', 1, 2, '2026-01-01 00:00:00', NULL),
    (28, 13, 'Otros ingresos',           1, 2, '2026-01-01 00:00:00', NULL);

ALTER TABLE `SubCategories` AUTO_INCREMENT = 29;

-- ============================================================
-- Tabla: ExchangeRates
-- EF Core: CreateExchangeRates + HasData (4 tasas con Source='seed')
-- Source='seed' está protegido en ResetAsync() del test fixture.
-- ============================================================
CREATE TABLE IF NOT EXISTS `ExchangeRates` (
    `IdExchangeRate` INT           AUTO_INCREMENT,
    `FromCurrency`   CHAR(3)       NOT NULL,
    `ToCurrency`     CHAR(3)       NOT NULL,
    `Rate`           DECIMAL(18,6) NOT NULL,
    `RateDate`       DATE          NOT NULL,
    `Source`         VARCHAR(100)  NULL,
    `FetchedAt`      DATETIME(6)   NOT NULL,
    PRIMARY KEY (`IdExchangeRate`),
    UNIQUE KEY `IX_ExchangeRates_FromCurrency_ToCurrency_RateDate`
        (`FromCurrency`, `ToCurrency`, `RateDate`)
) CHARACTER SET utf8mb4;

-- Espejo exacto de EF Core HasData (CreateExchangeRates migration)
INSERT IGNORE INTO `ExchangeRates`
    (`IdExchangeRate`, `FromCurrency`, `ToCurrency`, `Rate`, `RateDate`, `Source`, `FetchedAt`) VALUES
    (1, 'USD', 'EUR', 0.920000, '2026-01-01', 'seed', '2026-01-01 00:00:00'),
    (2, 'GBP', 'EUR', 1.170000, '2026-01-01', 'seed', '2026-01-01 00:00:00'),
    (3, 'CHF', 'EUR', 1.060000, '2026-01-01', 'seed', '2026-01-01 00:00:00'),
    (4, 'JPY', 'EUR', 0.006100, '2026-01-01', 'seed', '2026-01-01 00:00:00');

ALTER TABLE `ExchangeRates` AUTO_INCREMENT = 5;

-- ============================================================
-- Tabla: Transactions
-- EF Core: CreateTransactions (Plan 2B)
-- Cambios vs viejo init.sql:
--   · Type SMALLINT (0=Income, 1=Expense) en lugar de ENUM('INCOME','EXPENSE')
--   · TransactionDate en lugar de Date
--   · 6 columnas MoneyConversion en lugar de Amount
--   · Eliminadas: IsRecurrent, RecurrencePeriod
--   · FK IdSubCategory ON DELETE SET NULL; FK IdUser ON DELETE CASCADE
-- ============================================================
CREATE TABLE IF NOT EXISTS `Transactions` (
    `IdTransaction`    INT           AUTO_INCREMENT,
    `IdUser`           INT           NOT NULL,
    `Type`             SMALLINT      NOT NULL,
    `IdMainCategory`   INT           NOT NULL,
    `IdSubCategory`    INT           NULL,
    `Description`      VARCHAR(255)  NULL,
    `TransactionDate`  DATE          NOT NULL,
    `OriginalAmount`   DECIMAL(18,2) NOT NULL,
    `OriginalCurrency` CHAR(3)       NOT NULL,
    `ExchangeRate`     DECIMAL(18,6) NOT NULL,
    `BaseAmount`       DECIMAL(18,2) NOT NULL,
    `BaseCurrency`     CHAR(3)       NOT NULL,
    `RateDate`         DATE          NOT NULL,
    `IdStatus`         SMALLINT      NOT NULL DEFAULT 2,
    `CreatedAt`        DATETIME(6)   NOT NULL,
    `UpdatedAt`        DATETIME(6)   NULL,
    PRIMARY KEY (`IdTransaction`),
    KEY `IX_Transactions_IdUser_TransactionDate` (`IdUser`, `TransactionDate`),
    KEY `IX_Transactions_IdSubCategory`          (`IdSubCategory`),
    CONSTRAINT `FK_Transactions_Users_IdUser`
        FOREIGN KEY (`IdUser`) REFERENCES `Users` (`IdUser`) ON DELETE CASCADE,
    CONSTRAINT `FK_Transactions_SubCategories_IdSubCategory`
        FOREIGN KEY (`IdSubCategory`) REFERENCES `SubCategories` (`IdSubCategory`) ON DELETE SET NULL
) CHARACTER SET utf8mb4;

-- ============================================================
-- Tablas no gestionadas por EF Core (Plan 3: BC Inversiones; Plan RAG)
-- ============================================================
CREATE TABLE IF NOT EXISTS `Companies` (
    `IdCompany` INT          AUTO_INCREMENT,
    `Name`      VARCHAR(200) NOT NULL,
    `Ticker`    VARCHAR(10)  NOT NULL,
    `Sector`    VARCHAR(100) NULL,
    `Market`    VARCHAR(50)  NULL,
    `Currency`  CHAR(3)      NOT NULL DEFAULT 'EUR',
    `IdStatus`  SMALLINT     NOT NULL DEFAULT 2,
    `CreatedAt` DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt` DATETIME     NULL,
    PRIMARY KEY (`IdCompany`),
    UNIQUE KEY `UQ_Companies_Ticker` (`Ticker`)
) CHARACTER SET utf8mb4;

CREATE TABLE IF NOT EXISTS `Portfolios` (
    `IdPortfolio` INT          AUTO_INCREMENT,
    `IdUser`      INT          NOT NULL,
    `Name`        VARCHAR(100) NOT NULL,
    `IdStatus`    SMALLINT     NOT NULL DEFAULT 2,
    `CreatedAt`   DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`   DATETIME     NULL,
    PRIMARY KEY (`IdPortfolio`),
    CONSTRAINT `FK_Portfolios_Users`
        FOREIGN KEY (`IdUser`) REFERENCES `Users` (`IdUser`) ON DELETE CASCADE
) CHARACTER SET utf8mb4;

CREATE TABLE IF NOT EXISTS `Holdings` (
    `IdHolding`   INT           AUTO_INCREMENT,
    `IdPortfolio` INT           NOT NULL,
    `IdCompany`   INT           NOT NULL,
    `Shares`      DECIMAL(18,4) NOT NULL,
    `AvgBuyPrice` DECIMAL(18,4) NOT NULL,
    `BuyDate`     DATE          NOT NULL,
    `Notes`       VARCHAR(500)  NULL,
    `IdStatus`    SMALLINT      NOT NULL DEFAULT 2,
    `CreatedAt`   DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`   DATETIME      NULL,
    PRIMARY KEY (`IdHolding`),
    CONSTRAINT `FK_Holdings_Portfolios`
        FOREIGN KEY (`IdPortfolio`) REFERENCES `Portfolios` (`IdPortfolio`) ON DELETE CASCADE,
    CONSTRAINT `FK_Holdings_Companies`
        FOREIGN KEY (`IdCompany`) REFERENCES `Companies` (`IdCompany`),
    CONSTRAINT `CHK_Holdings_Shares` CHECK (`Shares` > 0)
) CHARACTER SET utf8mb4;

CREATE TABLE IF NOT EXISTS `Valuations` (
    `IdValuation` INT           AUTO_INCREMENT,
    `IdCompany`   INT           NOT NULL,
    `Price`       DECIMAL(18,4) NOT NULL,
    `Date`        DATE          NOT NULL,
    `Source`      VARCHAR(100)  NULL,
    `IdStatus`    SMALLINT      NOT NULL DEFAULT 2,
    `CreatedAt`   DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`   DATETIME      NULL,
    PRIMARY KEY (`IdValuation`),
    CONSTRAINT `FK_Valuations_Companies`
        FOREIGN KEY (`IdCompany`) REFERENCES `Companies` (`IdCompany`),
    UNIQUE KEY `UQ_Valuations_Company_Date` (`IdCompany`, `Date`)
) CHARACTER SET utf8mb4;

CREATE TABLE IF NOT EXISTS `RagDocuments` (
    `IdRagDocument`      INT          AUTO_INCREMENT,
    `IdUser`             INT          NOT NULL,
    `FileName`           VARCHAR(255) NOT NULL,
    `FileType`           VARCHAR(10)  NOT NULL,
    `FileSize`           INT          NOT NULL,
    `ChunkCount`         INT          NOT NULL DEFAULT 0,
    `IdStatus`           SMALLINT     NOT NULL DEFAULT 1,
    `QdrantCollectionId` VARCHAR(100) NULL,
    `UploadedAt`         DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `IndexedAt`          DATETIME     NULL,
    PRIMARY KEY (`IdRagDocument`),
    CONSTRAINT `FK_RagDocuments_Users`
        FOREIGN KEY (`IdUser`) REFERENCES `Users` (`IdUser`) ON DELETE CASCADE
) CHARACTER SET utf8mb4;
```

- [ ] **Step 2: Verificar que el script es correcto**

Contar las 3 secciones INSERT IGNORE:
```
grep -c "INSERT IGNORE" infra/docker/mysql/init.sql
# Esperado: 3 (__EFMigrationsHistory, SubCategories, ExchangeRates)
```

---

### Task 2: Reescribir `seed.sql`

**Files:**
- Modify: `infra/docker/mysql/seed.sql`

seed.sql no vive en `docker-entrypoint-initdb.d/` — se ejecuta manualmente una vez que el usuario demo (IdUser=1) ha sido creado vía API. Contiene: Companies, Portfolios (IdUser=1), Holdings, Valuations y 51 Transactions en el nuevo schema.

- [ ] **Step 1: Reemplazar el contenido completo de `infra/docker/mysql/seed.sql`**

```sql
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
-- Empresas (Plan 3: BC Inversiones)
-- ============================================================
INSERT INTO `Companies` (`IdCompany`, `Name`, `Ticker`, `Sector`, `Market`, `Currency`) VALUES
    (1, 'Apple Inc.',            'AAPL', 'Tecnología', 'NASDAQ', 'USD'),
    (2, 'Microsoft Corporation', 'MSFT', 'Tecnología', 'NASDAQ', 'USD'),
    (3, 'Inditex',               'ITX',  'Textil',     'BME',    'EUR'),
    (4, 'Iberdrola',             'IBE',  'Energía',    'BME',    'EUR'),
    (5, 'NVIDIA Corporation',    'NVDA', 'Tecnología', 'NASDAQ', 'USD'),
    (6, 'Banco Santander',       'SAN',  'Banca',      'BME',    'EUR'),
    (7, 'Amazon.com Inc.',       'AMZN', 'Tecnología', 'NASDAQ', 'USD'),
    (8, 'Tesla Inc.',            'TSLA', 'Automoción', 'NASDAQ', 'USD');

-- ============================================================
-- Portfolio del usuario demo (IdUser=1 = primer usuario registrado vía API)
-- ============================================================
INSERT INTO `Portfolios` (`IdPortfolio`, `IdUser`, `Name`) VALUES
    (1, 1, 'Mi cartera principal');

-- ============================================================
-- Posiciones abiertas
-- ============================================================
INSERT INTO `Holdings` (`IdPortfolio`, `IdCompany`, `Shares`, `AvgBuyPrice`, `BuyDate`, `Notes`) VALUES
    (1, 1, 10.0000, 178.5000, '2026-02-15', 'Compra inicial Apple'),
    (1, 2,  5.0000, 415.2000, '2026-03-01', 'Microsoft a buen precio'),
    (1, 3, 25.0000,  38.4500, '2026-01-20', 'Inditex pre-resultados'),
    (1, 4, 50.0000,  12.8000, '2026-02-10', 'Iberdrola dividendo'),
    (1, 5,  3.0000, 890.0000, '2026-04-05', 'NVIDIA IA');

-- ============================================================
-- Valoraciones históricas (marzo-junio 2026)
-- ============================================================
INSERT INTO `Valuations` (`IdCompany`, `Price`, `Date`, `Source`) VALUES
    (1, 175.2000, '2026-03-01', 'Yahoo Finance'), (1, 180.5000, '2026-03-15', 'Yahoo Finance'),
    (1, 182.3000, '2026-04-01', 'Yahoo Finance'), (1, 188.7500, '2026-04-15', 'Yahoo Finance'),
    (1, 191.0000, '2026-05-01', 'Yahoo Finance'), (1, 195.4000, '2026-05-15', 'Yahoo Finance'),
    (1, 193.2000, '2026-06-01', 'Yahoo Finance'),
    (2, 410.0000, '2026-03-01', 'Yahoo Finance'), (2, 418.5000, '2026-03-15', 'Yahoo Finance'),
    (2, 425.0000, '2026-04-01', 'Yahoo Finance'), (2, 430.2000, '2026-04-15', 'Yahoo Finance'),
    (2, 435.8000, '2026-05-01', 'Yahoo Finance'), (2, 442.1000, '2026-05-15', 'Yahoo Finance'),
    (2, 438.5000, '2026-06-01', 'Yahoo Finance'),
    (3,  37.8000, '2026-03-01', 'BME'), (3,  39.1000, '2026-03-15', 'BME'),
    (3,  40.2500, '2026-04-01', 'BME'), (3,  41.5000, '2026-04-15', 'BME'),
    (3,  42.0000, '2026-05-01', 'BME'), (3,  43.2000, '2026-05-15', 'BME'),
    (3,  42.8000, '2026-06-01', 'BME'),
    (4,  12.5000, '2026-03-01', 'BME'), (4,  12.7500, '2026-03-15', 'BME'),
    (4,  13.0000, '2026-04-01', 'BME'), (4,  13.2000, '2026-04-15', 'BME'),
    (4,  13.5000, '2026-05-01', 'BME'), (4,  13.8000, '2026-05-15', 'BME'),
    (4,  14.0000, '2026-06-01', 'BME'),
    (5, 880.0000, '2026-03-01', 'Yahoo Finance'), (5, 895.0000, '2026-03-15', 'Yahoo Finance'),
    (5, 910.5000, '2026-04-01', 'Yahoo Finance'), (5, 925.0000, '2026-04-15', 'Yahoo Finance'),
    (5, 940.0000, '2026-05-01', 'Yahoo Finance'), (5, 960.5000, '2026-05-15', 'Yahoo Finance'),
    (5, 955.0000, '2026-06-01', 'Yahoo Finance');

-- ============================================================
-- Transacciones: 3 meses de datos (marzo-mayo 2026)
-- Schema nuevo vs viejo init.sql:
--   · Type SMALLINT: 0=Income (TransactionType.Income), 1=Expense (TransactionType.Expense)
--   · TransactionDate en lugar de Date
--   · OriginalAmount/OriginalCurrency/ExchangeRate/BaseAmount/BaseCurrency/RateDate
--     en lugar de Amount. Demo 100% EUR → rate=1.000000, BaseAmount=OriginalAmount.
--   · Sin IsRecurrent, sin RecurrencePeriod
--   · IDs SubCategory según EF Core InitialCreate migration (ver tabla de mapeo en plan)
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
```

- [ ] **Step 2: Verificar el recuento**

```
grep -c "(1, [01]," infra/docker/mysql/seed.sql
# Esperado: 51
```

---

### Task 3: Crear stack HTTP para pruebas manuales

**Files:**
- Create: `docs/swagger/bigschool-api.http`

- [ ] **Step 1: Crear el fichero `docs/swagger/bigschool-api.http`**

```http
# BigSchool-TFM — API REST Test Stack
# Formato: VS Code REST Client (extensión humao.rest-client)
# Instalar: code --install-extension humao.rest-client
#
# WORKFLOW:
#   1. docker-compose up -d mysql
#   2. dotnet run --project src/backend/src/BigSchool.WebApi/BigSchool.WebApi.csproj
#   3. Ejecutar "01 Registro" → crea demo@bigschool.com (IdUser=1)
#   4. Ejecutar "02 Login"    → copiar data.accessToken → pegar en @token (abajo)
#   5. Ejecutar seed.sql en Workbench: mysql -u bigschool -p bigschool < infra/docker/mysql/seed.sql
#   6. Probar el resto de endpoints
# ─────────────────────────────────────────────────────────────────────────────
@baseUrl = http://localhost:5285

# ⬇ Pegar aquí el accessToken devuelto por "02 Login"
@token = PEGAR_ACCESS_TOKEN_AQUI


# ═════════════════════════════════════════════════════════════════════════════
# AUTH
# ═════════════════════════════════════════════════════════════════════════════

### 01 Registro — crear cuenta demo (ejecutar UNA sola vez)
POST {{baseUrl}}/api/v1/auth/register
Content-Type: application/json

{
  "email": "demo@bigschool.com",
  "password": "Demo2026!",
  "fullName": "Usuario Demo"
}

###

### 02 Login — obtener token JWT (copiar data.accessToken → @token)
POST {{baseUrl}}/api/v1/auth/login
Content-Type: application/json

{
  "email": "demo@bigschool.com",
  "password": "Demo2026!"
}

###

### 03 Refresh — renovar token expirado (sin body, usa el Bearer actual)
POST {{baseUrl}}/api/v1/auth/refresh
Authorization: Bearer {{token}}

###


# ═════════════════════════════════════════════════════════════════════════════
# CATEGORIES
# ═════════════════════════════════════════════════════════════════════════════

### 04 Listar categorías (MainCategories + SubCategories globales y propias)
GET {{baseUrl}}/api/v1/categories
Authorization: Bearer {{token}}

###


# ═════════════════════════════════════════════════════════════════════════════
# TRANSACTIONS — CRUD
# ═════════════════════════════════════════════════════════════════════════════

### 05 Crear gasto en EUR (sin currency → usa BaseCurrency=EUR, rate=1, sin llamada externa)
# Type: 0=Income | 1=Expense
# IdMainCategory: 1=EssentialExpenses, 2=Investment, 3=Savings, 4=Donations, 5=Luxuries,
#                 6=Education, 7=Amortizations, 10=Salary, 11=Rentals, 12=Dividends, 13=Other
POST {{baseUrl}}/api/v1/transactions
Content-Type: application/json
Authorization: Bearer {{token}}

{
  "type": 1,
  "idMainCategory": 1,
  "idSubCategory": 1,
  "description": "Compra test Mercadona",
  "transactionDate": "2026-06-20",
  "amount": 75.50
}

###

### 06 Crear ingreso con moneda explícita USD → llama a Frankfurter o usa caché ExchangeRates
POST {{baseUrl}}/api/v1/transactions
Content-Type: application/json
Authorization: Bearer {{token}}

{
  "type": 0,
  "idMainCategory": 13,
  "idSubCategory": 28,
  "description": "Pago plataforma online USD",
  "transactionDate": "2026-06-15",
  "amount": 100.00,
  "currency": "USD"
}

###

### 07 Obtener transacción por ID (cambiar 1 por el ID real devuelto en 05/06)
GET {{baseUrl}}/api/v1/transactions/1
Authorization: Bearer {{token}}

###

### 08 Listar transacciones — sin filtros, página 1
GET {{baseUrl}}/api/v1/transactions?page=1&pageSize=20
Authorization: Bearer {{token}}

###

### 09 Listar transacciones — filtros: solo gastos de marzo-mayo 2026
GET {{baseUrl}}/api/v1/transactions?type=1&from=2026-03-01&to=2026-05-31&page=1&pageSize=50
Authorization: Bearer {{token}}

###

### 10 Listar transacciones — solo ingresos, categoría Salary (10)
GET {{baseUrl}}/api/v1/transactions?type=0&category=10&page=1&pageSize=10
Authorization: Bearer {{token}}

###

### 11 Actualizar transacción (reemplazar {id} por un ID existente)
PUT {{baseUrl}}/api/v1/transactions/1
Content-Type: application/json
Authorization: Bearer {{token}}

{
  "type": 1,
  "idMainCategory": 1,
  "idSubCategory": 3,
  "description": "Factura luz actualizada",
  "transactionDate": "2026-06-20",
  "amount": 118.00
}

###

### 12 Eliminar transacción — soft-delete (IdStatus=Deleted, Global Query Filter la oculta)
DELETE {{baseUrl}}/api/v1/transactions/1
Authorization: Bearer {{token}}

###


# ═════════════════════════════════════════════════════════════════════════════
# TRANSACTIONS — QUERIES AGREGADAS
# ═════════════════════════════════════════════════════════════════════════════

### 13 Resumen total — todos los datos del usuario (sin rango de fechas)
# Respuesta: { totalIncome, totalExpense, balance, baseCurrency }
GET {{baseUrl}}/api/v1/transactions/summary
Authorization: Bearer {{token}}

###

### 14 Resumen — rango marzo-mayo 2026 (los 3 meses del seed)
GET {{baseUrl}}/api/v1/transactions/summary?from=2026-03-01&to=2026-05-31
Authorization: Bearer {{token}}

###

### 15 Gráfica mensual — año 2026
# Respuesta: lista de { year, month, totalIncome, totalExpense } para graficar barras
GET {{baseUrl}}/api/v1/transactions/monthly-chart?year=2026
Authorization: Bearer {{token}}

###
```

- [ ] **Step 2: Verificar en VS Code**

Abrir `docs/swagger/bigschool-api.http`. Con `humao.rest-client` instalado, cada `###` muestra el botón `Send Request`. Confirmar que aparecen 15 peticiones numeradas.

---

## Auto-revisión

| Requisito | Task |
|---|---|
| `init.sql` ejecutable sin conflicto EF Core | Task 1 (`__EFMigrationsHistory` + schema final) |
| SubCategory IDs alineados con EF Core migration | Task 1 seed + Task 2 mapeo |
| `seed.sql` con schema nuevo Transactions | Task 2 (51 rows, SMALLINT, TransactionDate, 6 cols MoneyConversion) |
| Stack pre-rellenado para 10 endpoints | Task 3 (15 peticiones: Auth×3 + Categories×1 + CRUD×6 + Agregadas×3 + bonus filtros×2) |
| `currency` opcional documentado | Task 3 — request 05 sin currency, request 06 con currency |
