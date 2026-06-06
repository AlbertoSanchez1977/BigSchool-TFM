-- BigSchool-TFM: Datos de demostración (Seed)
-- 2-3 meses de datos fake para que el profesor pueda evaluar

-- ========== Usuario de demo ==========
-- Contraseña: Demo2026! (hash Argon2 placeholder - se regenerará desde la app)
INSERT INTO Users (Email, PasswordHash, PasswordSalt, FullName, LastLoginDate, IdStatus) VALUES
    ('demo@bigschool.com', '$argon2id$v=19$m=65536,t=3,p=4$placeholder_hash', 'placeholder_salt', 'Usuario Demo', NOW(), 2),
    ('profesor@bigschool.com', '$argon2id$v=19$m=65536,t=3,p=4$placeholder_hash', 'placeholder_salt', 'Profesor TFM', NULL, 2);

-- ========== Empresas ==========
INSERT INTO Companies (Name, Ticker, Sector, Market, Currency) VALUES
    ('Apple Inc.', 'AAPL', 'Tecnología', 'NASDAQ', 'USD'),
    ('Microsoft Corporation', 'MSFT', 'Tecnología', 'NASDAQ', 'USD'),
    ('Inditex', 'ITX', 'Textil', 'BME', 'EUR'),
    ('Iberdrola', 'IBE', 'Energía', 'BME', 'EUR'),
    ('NVIDIA Corporation', 'NVDA', 'Tecnología', 'NASDAQ', 'USD'),
    ('Banco Santander', 'SAN', 'Banca', 'BME', 'EUR'),
    ('Amazon.com Inc.', 'AMZN', 'Tecnología', 'NASDAQ', 'USD'),
    ('Tesla Inc.', 'TSLA', 'Automoción', 'NASDAQ', 'USD');

-- ========== Portfolio del usuario demo ==========
INSERT INTO Portfolios (IdUser, Name) VALUES
    (1, 'Mi cartera principal');

-- ========== Holdings (posiciones abiertas) ==========
INSERT INTO Holdings (IdPortfolio, IdCompany, Shares, AvgBuyPrice, BuyDate, Notes) VALUES
    (1, 1, 10.0000, 178.5000, '2026-02-15', 'Compra inicial Apple'),
    (1, 2, 5.0000, 415.2000, '2026-03-01', 'Microsoft a buen precio'),
    (1, 3, 25.0000, 38.4500, '2026-01-20', 'Inditex pre-resultados'),
    (1, 4, 50.0000, 12.8000, '2026-02-10', 'Iberdrola dividendo'),
    (1, 5, 3.0000, 890.0000, '2026-04-05', 'NVIDIA IA');

-- ========== Valoraciones históricas (marzo - junio 2026) ==========
-- Apple
INSERT INTO Valuations (IdCompany, Price, Date, Source) VALUES
    (1, 175.20, '2026-03-01', 'Yahoo Finance'),
    (1, 180.50, '2026-03-15', 'Yahoo Finance'),
    (1, 182.30, '2026-04-01', 'Yahoo Finance'),
    (1, 188.75, '2026-04-15', 'Yahoo Finance'),
    (1, 191.00, '2026-05-01', 'Yahoo Finance'),
    (1, 195.40, '2026-05-15', 'Yahoo Finance'),
    (1, 193.20, '2026-06-01', 'Yahoo Finance');

-- Microsoft
INSERT INTO Valuations (IdCompany, Price, Date, Source) VALUES
    (2, 410.00, '2026-03-01', 'Yahoo Finance'),
    (2, 418.50, '2026-03-15', 'Yahoo Finance'),
    (2, 425.00, '2026-04-01', 'Yahoo Finance'),
    (2, 430.20, '2026-04-15', 'Yahoo Finance'),
    (2, 435.80, '2026-05-01', 'Yahoo Finance'),
    (2, 442.10, '2026-05-15', 'Yahoo Finance'),
    (2, 438.50, '2026-06-01', 'Yahoo Finance');

-- Inditex
INSERT INTO Valuations (IdCompany, Price, Date, Source) VALUES
    (3, 37.80, '2026-03-01', 'BME'),
    (3, 39.10, '2026-03-15', 'BME'),
    (3, 40.25, '2026-04-01', 'BME'),
    (3, 41.50, '2026-04-15', 'BME'),
    (3, 42.00, '2026-05-01', 'BME'),
    (3, 43.20, '2026-05-15', 'BME'),
    (3, 42.80, '2026-06-01', 'BME');

-- Iberdrola
INSERT INTO Valuations (IdCompany, Price, Date, Source) VALUES
    (4, 12.50, '2026-03-01', 'BME'),
    (4, 12.75, '2026-03-15', 'BME'),
    (4, 13.00, '2026-04-01', 'BME'),
    (4, 13.20, '2026-04-15', 'BME'),
    (4, 13.50, '2026-05-01', 'BME'),
    (4, 13.80, '2026-05-15', 'BME'),
    (4, 14.00, '2026-06-01', 'BME');

-- NVIDIA
INSERT INTO Valuations (IdCompany, Price, Date, Source) VALUES
    (5, 880.00, '2026-03-01', 'Yahoo Finance'),
    (5, 895.00, '2026-03-15', 'Yahoo Finance'),
    (5, 910.50, '2026-04-01', 'Yahoo Finance'),
    (5, 925.00, '2026-04-15', 'Yahoo Finance'),
    (5, 940.00, '2026-05-01', 'Yahoo Finance'),
    (5, 960.50, '2026-05-15', 'Yahoo Finance'),
    (5, 955.00, '2026-06-01', 'Yahoo Finance');

-- ========== Transacciones: GASTOS (marzo - mayo 2026) ==========

-- Marzo 2026
INSERT INTO Transactions (IdUser, Type, IdMainCategory, IdSubCategory, Amount, Description, Date, IsRecurrent, RecurrencePeriod) VALUES
    (1, 'EXPENSE', 1, 1, 185.50, 'Compra semanal Mercadona', '2026-03-02', FALSE, NULL),
    (1, 'EXPENSE', 1, 1, 92.30, 'Compra semanal Mercadona', '2026-03-09', FALSE, NULL),
    (1, 'EXPENSE', 1, 3, 120.00, 'Factura luz marzo', '2026-03-05', TRUE, 'MONTHLY'),
    (1, 'EXPENSE', 1, 3, 45.00, 'Factura agua', '2026-03-10', TRUE, 'QUARTERLY'),
    (1, 'EXPENSE', 1, 3, 55.00, 'Factura internet', '2026-03-08', TRUE, 'MONTHLY'),
    (1, 'EXPENSE', 1, 4, 280.00, 'Seguro coche', '2026-03-15', TRUE, 'YEARLY'),
    (1, 'EXPENSE', 1, 5, 50.00, 'Gasolina', '2026-03-12', FALSE, NULL),
    (1, 'EXPENSE', 5, 13, 85.00, 'Cena restaurante', '2026-03-14', FALSE, NULL),
    (1, 'EXPENSE', 5, 14, 35.00, 'Cine + palomitas', '2026-03-16', FALSE, NULL),
    (1, 'EXPENSE', 5, 16, 120.00, 'Zapatillas running', '2026-03-20', FALSE, NULL),
    (1, 'EXPENSE', 6, 19, 29.99, 'Curso Udemy', '2026-03-22', FALSE, NULL),
    (1, 'EXPENSE', 7, 21, 650.00, 'Hipoteca marzo', '2026-03-01', TRUE, 'MONTHLY'),
    (1, 'EXPENSE', 1, 2, 18.50, 'Farmacia', '2026-03-25', FALSE, NULL);

-- Abril 2026
INSERT INTO Transactions (IdUser, Type, IdMainCategory, IdSubCategory, Amount, Description, Date, IsRecurrent, RecurrencePeriod) VALUES
    (1, 'EXPENSE', 1, 1, 175.20, 'Compra semanal', '2026-04-01', FALSE, NULL),
    (1, 'EXPENSE', 1, 1, 88.90, 'Compra semanal', '2026-04-08', FALSE, NULL),
    (1, 'EXPENSE', 1, 1, 165.00, 'Compra semanal', '2026-04-15', FALSE, NULL),
    (1, 'EXPENSE', 1, 1, 110.50, 'Compra semanal', '2026-04-22', FALSE, NULL),
    (1, 'EXPENSE', 1, 3, 115.00, 'Factura luz abril', '2026-04-05', TRUE, 'MONTHLY'),
    (1, 'EXPENSE', 1, 3, 55.00, 'Factura internet', '2026-04-08', TRUE, 'MONTHLY'),
    (1, 'EXPENSE', 1, 5, 55.00, 'Gasolina', '2026-04-10', FALSE, NULL),
    (1, 'EXPENSE', 1, 5, 48.00, 'Gasolina', '2026-04-24', FALSE, NULL),
    (1, 'EXPENSE', 5, 13, 62.00, 'Comida fuera', '2026-04-12', FALSE, NULL),
    (1, 'EXPENSE', 5, 15, 450.00, 'Escapada fin de semana', '2026-04-18', FALSE, NULL),
    (1, 'EXPENSE', 5, 17, 89.99, 'Auriculares bluetooth', '2026-04-20', FALSE, NULL),
    (1, 'EXPENSE', 6, 20, 25.00, 'Libro técnico', '2026-04-14', FALSE, NULL),
    (1, 'EXPENSE', 7, 21, 650.00, 'Hipoteca abril', '2026-04-01', TRUE, 'MONTHLY'),
    (1, 'EXPENSE', 4, 11, 30.00, 'Donación Cruz Roja', '2026-04-25', FALSE, NULL);

-- Mayo 2026
INSERT INTO Transactions (IdUser, Type, IdMainCategory, IdSubCategory, Amount, Description, Date, IsRecurrent, RecurrencePeriod) VALUES
    (1, 'EXPENSE', 1, 1, 195.00, 'Compra semanal', '2026-05-01', FALSE, NULL),
    (1, 'EXPENSE', 1, 1, 105.30, 'Compra semanal', '2026-05-08', FALSE, NULL),
    (1, 'EXPENSE', 1, 1, 142.80, 'Compra semanal', '2026-05-15', FALSE, NULL),
    (1, 'EXPENSE', 1, 1, 98.50, 'Compra semanal', '2026-05-22', FALSE, NULL),
    (1, 'EXPENSE', 1, 3, 125.00, 'Factura luz mayo', '2026-05-05', TRUE, 'MONTHLY'),
    (1, 'EXPENSE', 1, 3, 55.00, 'Factura internet', '2026-05-08', TRUE, 'MONTHLY'),
    (1, 'EXPENSE', 1, 3, 42.00, 'Factura móvil', '2026-05-10', TRUE, 'MONTHLY'),
    (1, 'EXPENSE', 1, 5, 52.00, 'Gasolina', '2026-05-06', FALSE, NULL),
    (1, 'EXPENSE', 5, 13, 95.00, 'Cena cumpleaños amigo', '2026-05-12', FALSE, NULL),
    (1, 'EXPENSE', 5, 14, 28.00, 'Netflix + Spotify', '2026-05-01', TRUE, 'MONTHLY'),
    (1, 'EXPENSE', 5, 16, 65.00, 'Camisetas verano', '2026-05-18', FALSE, NULL),
    (1, 'EXPENSE', 6, 18, 150.00, 'Matrícula curso online', '2026-05-20', FALSE, NULL),
    (1, 'EXPENSE', 7, 21, 650.00, 'Hipoteca mayo', '2026-05-01', TRUE, 'MONTHLY'),
    (1, 'EXPENSE', 1, 2, 12.80, 'Ibuprofeno', '2026-05-14', FALSE, NULL),
    (1, 'EXPENSE', 2, 6, 500.00, 'Aportación fondo indexado', '2026-05-15', TRUE, 'MONTHLY');

-- ========== Transacciones: INGRESOS (marzo - mayo 2026) ==========

-- Marzo 2026
INSERT INTO Transactions (IdUser, Type, IdMainCategory, IdSubCategory, Amount, Description, Date, IsRecurrent, RecurrencePeriod) VALUES
    (1, 'INCOME', 10, 23, 2850.00, 'Nómina marzo', '2026-03-28', TRUE, 'MONTHLY'),
    (1, 'INCOME', 13, 31, 350.00, 'Proyecto freelance web', '2026-03-15', FALSE, NULL);

-- Abril 2026
INSERT INTO Transactions (IdUser, Type, IdMainCategory, IdSubCategory, Amount, Description, Date, IsRecurrent, RecurrencePeriod) VALUES
    (1, 'INCOME', 10, 23, 2850.00, 'Nómina abril', '2026-04-28', TRUE, 'MONTHLY'),
    (1, 'INCOME', 12, 28, 125.00, 'Dividendo Iberdrola', '2026-04-10', FALSE, NULL),
    (1, 'INCOME', 11, 25, 600.00, 'Alquiler garaje', '2026-04-05', TRUE, 'MONTHLY');

-- Mayo 2026
INSERT INTO Transactions (IdUser, Type, IdMainCategory, IdSubCategory, Amount, Description, Date, IsRecurrent, RecurrencePeriod) VALUES
    (1, 'INCOME', 10, 23, 2850.00, 'Nómina mayo', '2026-05-28', TRUE, 'MONTHLY'),
    (1, 'INCOME', 11, 25, 600.00, 'Alquiler garaje', '2026-05-05', TRUE, 'MONTHLY'),
    (1, 'INCOME', 13, 31, 200.00, 'Venta mueble Wallapop', '2026-05-20', FALSE, NULL),
    (1, 'INCOME', 12, 29, 85.00, 'Dividendo Apple', '2026-05-15', FALSE, NULL);
