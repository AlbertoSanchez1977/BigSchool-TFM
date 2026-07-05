-- BigSchool-TFM: Inicialización de Base de Datos
-- ESTRATEGIA: Este script crea el schema COMPLETO en estado final (Plans 2A + 2B) y registra
-- las migraciones EF Core en __EFMigrationsHistory para que dotnet run arranque sin conflictos.
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
    ('20260620085001_CreateTransactions',         '8.0.11'),
    ('20260621101518_CreateCompanies',            '8.0.11'),
    ('20260621173448_AddSectorMarketEnums',       '8.0.11'),
    ('20260621184811_CreatePortfolios',           '8.0.11'),
    ('20260702164337_AddOutboxMessage',           '8.0.11'),
    ('20260704144600_AddNotificationsModule',     '8.0.11'),
    ('20260705113440_RemodelSubCategoryAggregate', '8.0.11');

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
-- RemodelSubCategoryAggregate (Spec 009 / Plan 022 Tarea 1): SubCategory pasa a ser AR
-- independiente de Finance (ya no hija de User) → se suelta la FK/índice hacia Users.
-- IdUser sigue siendo NULL = global, pero como referencia blanda (SIN FK dura), igual que
-- EmailLogs.IdUser.
-- ============================================================
CREATE TABLE IF NOT EXISTS `SubCategories` (
    `IdSubCategory`  INT          AUTO_INCREMENT,
    `IdMainCategory` INT          NOT NULL,
    `Name`           VARCHAR(100) NOT NULL,
    `IsDefault`      TINYINT(1)   NOT NULL DEFAULT 0,
    `IdStatus`       SMALLINT     NOT NULL DEFAULT 2,
    `CreatedAt`      DATETIME(6)  NOT NULL,
    `IdUser`         INT          NULL,
    PRIMARY KEY (`IdSubCategory`)
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
-- Tablas: Companies + Valuations
-- EF Core: CreateCompanies (Plan 3A) — espejo de migración 20260621101518
-- ============================================================
CREATE TABLE IF NOT EXISTS `Companies` (
    `IdCompany` INT          NOT NULL AUTO_INCREMENT,
    `Name`      VARCHAR(200) NOT NULL,
    `Ticker`    VARCHAR(10)  NOT NULL,
    `Sector`    VARCHAR(100) NULL,
    `Market`    VARCHAR(50)  NULL,
    `Currency`  CHAR(3)      NOT NULL DEFAULT 'EUR',
    `IdStatus`  SMALLINT     NOT NULL DEFAULT 2,
    `CreatedAt` DATETIME(6)  NOT NULL,
    `UpdatedAt` DATETIME(6)  NULL,
    CONSTRAINT `PK_Companies` PRIMARY KEY (`IdCompany`),
    UNIQUE KEY `IX_Companies_Ticker` (`Ticker`)
) CHARACTER SET utf8mb4;

-- Espejo exacto de EF Core HasData (CreateCompanies migration, timestamp 2026-01-01 UTC)
-- IDs 1-4 fijos: ResetAsync del test fixture los preserva junto con sus Valuations.
INSERT IGNORE INTO `Companies`
    (`IdCompany`, `Name`, `Ticker`, `Sector`, `Market`, `Currency`, `IdStatus`, `CreatedAt`) VALUES
    (1, 'Apple Inc.',      'AAPL', 'Technology', 'NASDAQ', 'USD', 2, '2026-01-01 00:00:00'),
    (2, 'Microsoft Corp.', 'MSFT', 'Technology', 'NASDAQ', 'USD', 2, '2026-01-01 00:00:00'),
    (3, 'Banco Santander', 'SAN',  'Financials',  'BME',   'EUR', 2, '2026-01-01 00:00:00'),
    (4, 'Shell plc',       'SHEL', 'Energy',       'LSE',  'GBP', 2, '2026-01-01 00:00:00');

ALTER TABLE `Companies` AUTO_INCREMENT = 5;

CREATE TABLE IF NOT EXISTS `Valuations` (
    `IdValuation`   INT           NOT NULL AUTO_INCREMENT,
    `Price`         DECIMAL(18,4) NOT NULL,
    `PriceCurrency` CHAR(3)       NOT NULL,
    `Date`          DATE          NOT NULL,
    `Source`        VARCHAR(100)  NULL,
    `IdStatus`      SMALLINT      NOT NULL DEFAULT 2,
    `CreatedAt`     DATETIME(6)   NOT NULL,
    `UpdatedAt`     DATETIME(6)   NULL,
    `IdCompany`     INT           NOT NULL,
    CONSTRAINT `PK_Valuations` PRIMARY KEY (`IdValuation`),
    CONSTRAINT `FK_Valuations_Companies_IdCompany`
        FOREIGN KEY (`IdCompany`) REFERENCES `Companies` (`IdCompany`) ON DELETE CASCADE,
    UNIQUE KEY `IX_Valuations_IdCompany_Date` (`IdCompany`, `Date`)
) CHARACTER SET utf8mb4;

-- Espejo exacto de EF Core HasData (CreateCompanies migration)
-- IDs 1-8 fijos: ResetAsync usa "DELETE FROM Valuations WHERE IdCompany > 4" para limpiar tests.
INSERT IGNORE INTO `Valuations`
    (`IdValuation`, `Price`, `PriceCurrency`, `Date`, `Source`, `IdStatus`, `CreatedAt`, `IdCompany`) VALUES
    (1, 195.0000, 'USD', '2026-01-02', 'seed', 2, '2026-01-01 00:00:00', 1),
    (2, 210.0000, 'USD', '2026-03-02', 'seed', 2, '2026-01-01 00:00:00', 1),
    (3, 420.0000, 'USD', '2026-01-02', 'seed', 2, '2026-01-01 00:00:00', 2),
    (4, 440.0000, 'USD', '2026-03-02', 'seed', 2, '2026-01-01 00:00:00', 2),
    (5,   4.5000, 'EUR', '2026-01-02', 'seed', 2, '2026-01-01 00:00:00', 3),
    (6,   4.8000, 'EUR', '2026-03-02', 'seed', 2, '2026-01-01 00:00:00', 3),
    (7,  28.0000, 'GBP', '2026-01-02', 'seed', 2, '2026-01-01 00:00:00', 4),
    (8,  30.0000, 'GBP', '2026-03-02', 'seed', 2, '2026-01-01 00:00:00', 4);

ALTER TABLE `Valuations` AUTO_INCREMENT = 9;

-- ============================================================
-- Tablas EF Core: Plan 3B — Portfolio/Holding/Disposal
-- Espejo exacto de migración 20260621184811_CreatePortfolios
-- ============================================================

-- Portfolio: AR por-usuario. RealizedPnL = plusvalía acumulada persistida por el agregado.
CREATE TABLE IF NOT EXISTS `Portfolios` (
    `IdPortfolio`         INT           NOT NULL AUTO_INCREMENT,
    `IdUser`              INT           NOT NULL,
    `Name`                VARCHAR(100)  CHARACTER SET utf8mb4 NOT NULL,
    `RealizedPnL`         DECIMAL(18,2) NOT NULL,
    `RealizedPnLCurrency` CHAR(3)       CHARACTER SET utf8mb4 NOT NULL,
    `IdStatus`            SMALLINT      NOT NULL DEFAULT 2,
    `CreatedAt`           DATETIME(6)   NOT NULL,
    `UpdatedAt`           DATETIME(6)   NULL,
    CONSTRAINT `PK_Portfolios` PRIMARY KEY (`IdPortfolio`),
    KEY `IX_Portfolios_IdUser` (`IdUser`),
    CONSTRAINT `FK_Portfolios_Users_IdUser`
        FOREIGN KEY (`IdUser`) REFERENCES `Users` (`IdUser`) ON DELETE CASCADE
) CHARACTER SET utf8mb4;

-- Holdings: lote de compra. AvgBuyPrice = MoneyConversion snapshot (Buy* columns).
-- OpenShares = Shares − Σ Disposals.Shares activos (derivado, no persistido).
-- FK IdCompany RESTRICT: no se puede borrar una empresa con lotes abiertos.
CREATE TABLE IF NOT EXISTS `Holdings` (
    `IdHolding`           INT           NOT NULL AUTO_INCREMENT,
    `IdCompany`           INT           NOT NULL,
    `Shares`              DECIMAL(18,4) NOT NULL,
    `BuyOriginalAmount`   DECIMAL(18,4) NOT NULL,
    `BuyOriginalCurrency` CHAR(3)       CHARACTER SET utf8mb4 NOT NULL,
    `BuyExchangeRate`     DECIMAL(18,6) NOT NULL,
    `BuyBaseAmount`       DECIMAL(18,2) NOT NULL,
    `BuyBaseCurrency`     CHAR(3)       CHARACTER SET utf8mb4 NOT NULL,
    `BuyRateDate`         DATE          NOT NULL,
    `BuyDate`             DATE          NOT NULL,
    `Notes`               VARCHAR(500)  CHARACTER SET utf8mb4 NULL,
    `IdStatus`            SMALLINT      NOT NULL DEFAULT 2,
    `CreatedAt`           DATETIME(6)   NOT NULL,
    `UpdatedAt`           DATETIME(6)   NULL,
    `IdPortfolio`         INT           NOT NULL,
    CONSTRAINT `PK_Holdings` PRIMARY KEY (`IdHolding`),
    KEY `IX_Holdings_IdCompany` (`IdCompany`),
    KEY `IX_Holdings_IdPortfolio_IdCompany` (`IdPortfolio`, `IdCompany`),
    CONSTRAINT `FK_Holdings_Companies_IdCompany`
        FOREIGN KEY (`IdCompany`) REFERENCES `Companies` (`IdCompany`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Holdings_Portfolios_IdPortfolio`
        FOREIGN KEY (`IdPortfolio`) REFERENCES `Portfolios` (`IdPortfolio`) ON DELETE CASCADE
) CHARACTER SET utf8mb4;

-- Disposals: venta generada por SellShares FIFO. SellPrice = MoneyConversion snapshot (Sell* columns).
-- RealizedPnL = (SellBase − BuyBase) × Shares (calculado y persistido por el agregado).
CREATE TABLE IF NOT EXISTS `Disposals` (
    `IdDisposal`           INT           NOT NULL AUTO_INCREMENT,
    `Shares`               DECIMAL(18,4) NOT NULL,
    `SellOriginalAmount`   DECIMAL(18,4) NOT NULL,
    `SellOriginalCurrency` CHAR(3)       CHARACTER SET utf8mb4 NOT NULL,
    `SellExchangeRate`     DECIMAL(18,6) NOT NULL,
    `SellBaseAmount`       DECIMAL(18,2) NOT NULL,
    `SellBaseCurrency`     CHAR(3)       CHARACTER SET utf8mb4 NOT NULL,
    `SellRateDate`         DATE          NOT NULL,
    `SellDate`             DATE          NOT NULL,
    `RealizedPnL`          DECIMAL(18,2) NOT NULL,
    `RealizedPnLCurrency`  CHAR(3)       CHARACTER SET utf8mb4 NOT NULL,
    `Notes`                VARCHAR(500)  CHARACTER SET utf8mb4 NULL,
    `IdStatus`             SMALLINT      NOT NULL DEFAULT 2,
    `CreatedAt`            DATETIME(6)   NOT NULL,
    `UpdatedAt`            DATETIME(6)   NULL,
    `IdHolding`            INT           NOT NULL,
    CONSTRAINT `PK_Disposals` PRIMARY KEY (`IdDisposal`),
    KEY `IX_Disposals_IdHolding` (`IdHolding`),
    CONSTRAINT `FK_Disposals_Holdings_IdHolding`
        FOREIGN KEY (`IdHolding`) REFERENCES `Holdings` (`IdHolding`) ON DELETE CASCADE
) CHARACTER SET utf8mb4;

-- ============================================================
-- Tabla: OutboxMessages
-- EF Core: AddOutboxMessage (Spec 0 / Plan 018 Tarea 7) — espejo exacto de migración
-- 20260702164337. Infraestructura pura (SharedKernel): outbox transaccional de
-- IntegrationEvents, drenado post-commit por OutboxDispatcher. Sin consumidores todavía
-- (se estrenan en Spec 007).
-- ============================================================
CREATE TABLE IF NOT EXISTS `OutboxMessages` (
    `IdOutboxMessage` BIGINT        NOT NULL AUTO_INCREMENT,
    `EventId`         CHAR(36)      CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
    `Type`            VARCHAR(512)  CHARACTER SET utf8mb4 NOT NULL,
    `Payload`         JSON          NOT NULL,
    `OccurredOn`      DATETIME(6)   NOT NULL,
    `ProcessedOn`     DATETIME(6)   NULL,
    `Error`           VARCHAR(2048) CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_OutboxMessages` PRIMARY KEY (`IdOutboxMessage`),
    UNIQUE KEY `IX_OutboxMessages_EventId` (`EventId`),
    KEY `IX_OutboxMessages_ProcessedOn` (`ProcessedOn`)
) CHARACTER SET utf8mb4;

-- ============================================================
-- Tablas: Contacts + EmailLogs
-- EF Core: AddNotificationsModule (Spec 007 / Plan 020 Tarea 3) — espejo exacto de
-- migración 20260704144600. EmailLogs.IdUser es una referencia "blanda" (SIN FK):
-- mantiene limpia la frontera de módulo Auth/Notifications. Visibilidad por query
-- en la Application layer: IdUser = @user OR IdUser IS NULL (welcome propio + globales).
-- ============================================================
CREATE TABLE IF NOT EXISTS `Contacts` (
    `IdContact` INT           NOT NULL AUTO_INCREMENT,
    `FullName`  VARCHAR(200)  CHARACTER SET utf8mb4 NOT NULL,
    `Email`     VARCHAR(255)  CHARACTER SET utf8mb4 NOT NULL,
    `Message`   VARCHAR(2000) CHARACTER SET utf8mb4 NOT NULL,
    `IdStatus`  SMALLINT      NOT NULL DEFAULT 2,
    `CreatedAt` DATETIME(6)   NOT NULL,
    CONSTRAINT `PK_Contacts` PRIMARY KEY (`IdContact`)
) CHARACTER SET utf8mb4;

CREATE TABLE IF NOT EXISTS `EmailLogs` (
    `IdEmailLog` INT           NOT NULL AUTO_INCREMENT,
    `IdUser`     INT           NULL,
    `Recipient`  VARCHAR(255)  CHARACTER SET utf8mb4 NOT NULL,
    `Subject`    VARCHAR(300)  CHARACTER SET utf8mb4 NOT NULL,
    `Body`       VARCHAR(4000) CHARACTER SET utf8mb4 NOT NULL,
    `Type`       SMALLINT      NOT NULL,
    `SentAt`     DATETIME(6)   NOT NULL,
    `IdStatus`   SMALLINT      NOT NULL DEFAULT 2,
    `CreatedAt`  DATETIME(6)   NOT NULL,
    CONSTRAINT `PK_EmailLogs` PRIMARY KEY (`IdEmailLog`),
    KEY `IX_EmailLogs_IdUser` (`IdUser`)
) CHARACTER SET utf8mb4;

-- ============================================================
-- Tablas no gestionadas por EF Core (Plan RAG)
-- ============================================================
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
