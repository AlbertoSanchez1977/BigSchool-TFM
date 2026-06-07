-- BigSchool-TFM: Inicialización de Base de Datos
-- Este script se ejecuta automáticamente al crear el contenedor MySQL
--
-- NOTA: Status y MainCategories son Enums en código C#, no tablas.
-- IdStatus: 1=Pending, 2=Active, 3=Processing, 4=Deleted
-- MainCategories EXPENSE: 1=Gastos Necesarios, 2=Inversión, 3=Ahorro, 4=Donaciones, 5=Lujos, 6=Educación, 7=Amortizaciones
-- MainCategories INCOME: 10=Nómina, 11=Alquileres, 12=Dividendos, 13=Otros

-- Tabla: Users
CREATE TABLE IF NOT EXISTS Users (
    IdUser INT AUTO_INCREMENT PRIMARY KEY,
    Email VARCHAR(255) NOT NULL UNIQUE,
    PasswordHash VARCHAR(512) NOT NULL,
    PasswordSalt VARCHAR(256) NOT NULL,
    FullName VARCHAR(200) NOT NULL,
    LastLoginDate DATETIME NULL,
    IdStatus SMALLINT NOT NULL DEFAULT 2,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NULL
);

-- Tabla: SubCategories
CREATE TABLE IF NOT EXISTS SubCategories (
    IdSubCategory INT AUTO_INCREMENT PRIMARY KEY,
    IdMainCategory INT NOT NULL,
    IdUser INT NULL,
    Name VARCHAR(100) NOT NULL,
    IsDefault BOOLEAN NOT NULL DEFAULT FALSE,
    IdStatus SMALLINT NOT NULL DEFAULT 2,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_SubCategories_User FOREIGN KEY (IdUser) REFERENCES Users(IdUser)
);

-- Subcategorías predefinidas (globales, IdUser = NULL)
INSERT INTO SubCategories (IdMainCategory, IdUser, Name, IsDefault) VALUES
    -- Gastos Necesarios (1)
    (1, NULL, 'Supermercado', TRUE),
    (1, NULL, 'Farmacia', TRUE),
    (1, NULL, 'Facturas', TRUE),
    (1, NULL, 'Seguros', TRUE),
    (1, NULL, 'Transporte', TRUE),
    -- Inversión (2)
    (2, NULL, 'Bolsa', TRUE),
    (2, NULL, 'Fondos', TRUE),
    (2, NULL, 'Crypto', TRUE),
    -- Ahorro (3)
    (3, NULL, 'Cuenta ahorro', TRUE),
    (3, NULL, 'Depósitos', TRUE),
    -- Donaciones (4)
    (4, NULL, 'ONG', TRUE),
    (4, NULL, 'Particulares', TRUE),
    -- Lujos (5)
    (5, NULL, 'Restaurantes', TRUE),
    (5, NULL, 'Ocio', TRUE),
    (5, NULL, 'Viajes', TRUE),
    (5, NULL, 'Ropa', TRUE),
    (5, NULL, 'Tecnología', TRUE),
    -- Educación (6)
    (6, NULL, 'Cursos', TRUE),
    (6, NULL, 'Libros', TRUE),
    (6, NULL, 'Máster', TRUE),
    -- Amortizaciones (7)
    (7, NULL, 'Hipoteca', TRUE),
    (7, NULL, 'Préstamo personal', TRUE),
    -- Nómina (10)
    (10, NULL, 'Empresa principal', TRUE),
    (10, NULL, 'Empresa secundaria', TRUE),
    -- Alquileres (11)
    (11, NULL, 'Vivienda', TRUE),
    (11, NULL, 'Local', TRUE),
    (11, NULL, 'Garaje', TRUE),
    -- Dividendos (12)
    (12, NULL, 'Acciones nacionales', TRUE),
    (12, NULL, 'Acciones internacionales', TRUE),
    -- Otros (13)
    (13, NULL, 'Freelance', TRUE),
    (13, NULL, 'Ventas', TRUE);

-- Tabla: Transactions
CREATE TABLE IF NOT EXISTS Transactions (
    IdTransaction INT AUTO_INCREMENT PRIMARY KEY,
    IdUser INT NOT NULL,
    Type ENUM('INCOME', 'EXPENSE') NOT NULL,
    IdMainCategory INT NOT NULL,
    IdSubCategory INT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    Description VARCHAR(500) NULL,
    Date DATE NOT NULL,
    IsRecurrent BOOLEAN NOT NULL DEFAULT FALSE,
    RecurrencePeriod ENUM('MONTHLY', 'QUARTERLY', 'YEARLY') NULL,
    IdStatus SMALLINT NOT NULL DEFAULT 2,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NULL,
    CONSTRAINT FK_Transactions_User FOREIGN KEY (IdUser) REFERENCES Users(IdUser),
    CONSTRAINT FK_Transactions_SubCategory FOREIGN KEY (IdSubCategory) REFERENCES SubCategories(IdSubCategory),
    CONSTRAINT CHK_Transactions_Amount CHECK (Amount > 0)
);

CREATE INDEX IX_Transactions_User_Date ON Transactions(IdUser, Date);
CREATE INDEX IX_Transactions_User_Type ON Transactions(IdUser, Type);

-- Tabla: Companies
CREATE TABLE IF NOT EXISTS Companies (
    IdCompany INT AUTO_INCREMENT PRIMARY KEY,
    Name VARCHAR(200) NOT NULL,
    Ticker VARCHAR(10) NOT NULL UNIQUE,
    Sector VARCHAR(100) NULL,
    Market VARCHAR(50) NULL,
    Currency CHAR(3) NOT NULL DEFAULT 'EUR',
    IdStatus SMALLINT NOT NULL DEFAULT 2,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NULL
);

-- Tabla: Portfolios
CREATE TABLE IF NOT EXISTS Portfolios (
    IdPortfolio INT AUTO_INCREMENT PRIMARY KEY,
    IdUser INT NOT NULL,
    Name VARCHAR(100) NOT NULL,
    IdStatus SMALLINT NOT NULL DEFAULT 2,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NULL,
    CONSTRAINT FK_Portfolios_User FOREIGN KEY (IdUser) REFERENCES Users(IdUser)
);

-- Tabla: Holdings
CREATE TABLE IF NOT EXISTS Holdings (
    IdHolding INT AUTO_INCREMENT PRIMARY KEY,
    IdPortfolio INT NOT NULL,
    IdCompany INT NOT NULL,
    Shares DECIMAL(18,4) NOT NULL,
    AvgBuyPrice DECIMAL(18,4) NOT NULL,
    BuyDate DATE NOT NULL,
    Notes VARCHAR(500) NULL,
    IdStatus SMALLINT NOT NULL DEFAULT 2,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NULL,
    CONSTRAINT FK_Holdings_Portfolio FOREIGN KEY (IdPortfolio) REFERENCES Portfolios(IdPortfolio),
    CONSTRAINT FK_Holdings_Company FOREIGN KEY (IdCompany) REFERENCES Companies(IdCompany),
    CONSTRAINT CHK_Holdings_Shares CHECK (Shares > 0)
);

-- Tabla: Valuations
CREATE TABLE IF NOT EXISTS Valuations (
    IdValuation INT AUTO_INCREMENT PRIMARY KEY,
    IdCompany INT NOT NULL,
    Price DECIMAL(18,4) NOT NULL,
    Date DATE NOT NULL,
    Source VARCHAR(100) NULL,
    IdStatus SMALLINT NOT NULL DEFAULT 2,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NULL,
    CONSTRAINT FK_Valuations_Company FOREIGN KEY (IdCompany) REFERENCES Companies(IdCompany),
    UNIQUE KEY UQ_Valuations_Company_Date (IdCompany, Date)
);

-- Tabla: RagDocuments
CREATE TABLE IF NOT EXISTS RagDocuments (
    IdRagDocument INT AUTO_INCREMENT PRIMARY KEY,
    IdUser INT NOT NULL,
    FileName VARCHAR(255) NOT NULL,
    FileType VARCHAR(10) NOT NULL,
    FileSize INT NOT NULL,
    ChunkCount INT NOT NULL DEFAULT 0,
    IdStatus SMALLINT NOT NULL DEFAULT 1,
    QdrantCollectionId VARCHAR(100) NULL,
    UploadedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    IndexedAt DATETIME NULL,
    CONSTRAINT FK_RagDocuments_User FOREIGN KEY (IdUser) REFERENCES Users(IdUser)
);
