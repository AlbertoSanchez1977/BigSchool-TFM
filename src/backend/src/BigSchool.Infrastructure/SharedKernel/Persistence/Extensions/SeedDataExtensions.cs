using BigSchool.Domain.Finanzas.Enums;
using BigSchool.Domain.Investments.Enums;
using BigSchool.Domain.SharedKernel.Enums;
using Microsoft.EntityFrameworkCore;

namespace BigSchool.Infrastructure.SharedKernel.Persistence.Extensions;

public static class SeedDataExtensions
{
    private static readonly DateTime SeedDate = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static ModelBuilder SeedSubCategories(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Domain.Finanzas.Entities.SubCategory>().HasData(
            // Gastos Necesarios
            new { IdSubCategory = 1, IdMainCategory = MainCategory.EssentialExpenses, Name = "Supermercado", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdSubCategory = 2, IdMainCategory = MainCategory.EssentialExpenses, Name = "Farmacia", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdSubCategory = 3, IdMainCategory = MainCategory.EssentialExpenses, Name = "Facturas", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdSubCategory = 4, IdMainCategory = MainCategory.EssentialExpenses, Name = "Seguros", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdSubCategory = 5, IdMainCategory = MainCategory.EssentialExpenses, Name = "Transporte", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            // Inversión
            new { IdSubCategory = 6, IdMainCategory = MainCategory.Investment, Name = "Bolsa", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdSubCategory = 7, IdMainCategory = MainCategory.Investment, Name = "Fondos", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdSubCategory = 8, IdMainCategory = MainCategory.Investment, Name = "Crypto", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            // Ahorro
            new { IdSubCategory = 9, IdMainCategory = MainCategory.Savings, Name = "Cuenta ahorro", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdSubCategory = 10, IdMainCategory = MainCategory.Savings, Name = "Depósitos", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            // Lujos
            new { IdSubCategory = 11, IdMainCategory = MainCategory.Luxuries, Name = "Restaurantes", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdSubCategory = 12, IdMainCategory = MainCategory.Luxuries, Name = "Ocio", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdSubCategory = 13, IdMainCategory = MainCategory.Luxuries, Name = "Viajes", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdSubCategory = 14, IdMainCategory = MainCategory.Luxuries, Name = "Ropa", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdSubCategory = 15, IdMainCategory = MainCategory.Luxuries, Name = "Tecnología", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            // Educación
            new { IdSubCategory = 16, IdMainCategory = MainCategory.Education, Name = "Cursos", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdSubCategory = 17, IdMainCategory = MainCategory.Education, Name = "Libros", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdSubCategory = 18, IdMainCategory = MainCategory.Education, Name = "Máster", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            // Donaciones
            new { IdSubCategory = 19, IdMainCategory = MainCategory.Donations, Name = "ONG", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            // Amortizaciones
            new { IdSubCategory = 20, IdMainCategory = MainCategory.Amortizations, Name = "Hipoteca", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdSubCategory = 21, IdMainCategory = MainCategory.Amortizations, Name = "Préstamo", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            // Nómina
            new { IdSubCategory = 22, IdMainCategory = MainCategory.Salary, Name = "Empresa principal", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            // Alquileres
            new { IdSubCategory = 23, IdMainCategory = MainCategory.Rentals, Name = "Vivienda", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdSubCategory = 24, IdMainCategory = MainCategory.Rentals, Name = "Local", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdSubCategory = 25, IdMainCategory = MainCategory.Rentals, Name = "Garaje", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            // Dividendos
            new { IdSubCategory = 26, IdMainCategory = MainCategory.Dividends, Name = "Acciones nacionales", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdSubCategory = 27, IdMainCategory = MainCategory.Dividends, Name = "Acciones internacionales", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            // Otros
            new { IdSubCategory = 28, IdMainCategory = MainCategory.Other, Name = "Otros ingresos", IdUser = (int?)null, IsDefault = true, IdStatus = EntityStatus.Active, CreatedAt = SeedDate }
        );

        return modelBuilder;
    }

    public static ModelBuilder SeedExchangeRates(this ModelBuilder modelBuilder)
    {
        // Tipos fijos para demo determinista (sin red). RateDate = 2026-01-01. Source = "seed".
        modelBuilder.Entity<Domain.SharedKernel.Entities.ExchangeRate>().HasData(
            new { IdExchangeRate = 1, FromCurrency = Currency.USD, ToCurrency = Currency.EUR, Rate = 0.920000m, RateDate = new DateOnly(2026, 1, 1), Source = (string?)"seed", FetchedAt = SeedDate },
            new { IdExchangeRate = 2, FromCurrency = Currency.GBP, ToCurrency = Currency.EUR, Rate = 1.170000m, RateDate = new DateOnly(2026, 1, 1), Source = (string?)"seed", FetchedAt = SeedDate },
            new { IdExchangeRate = 3, FromCurrency = Currency.CHF, ToCurrency = Currency.EUR, Rate = 1.060000m, RateDate = new DateOnly(2026, 1, 1), Source = (string?)"seed", FetchedAt = SeedDate },
            new { IdExchangeRate = 4, FromCurrency = Currency.JPY, ToCurrency = Currency.EUR, Rate = 0.006100m, RateDate = new DateOnly(2026, 1, 1), Source = (string?)"seed", FetchedAt = SeedDate }
        );

        return modelBuilder;
    }

    public static ModelBuilder SeedCompanies(this ModelBuilder modelBuilder)
    {
        // Catálogo global de demo (4 empresas en 3 monedas). IDs fijos 1-4 → el fixture de tests
        // los preserva y limpia solo las creadas por tests (IdCompany > 4).
        modelBuilder.Entity<Domain.Investments.Entities.Company>().HasData(
            new { IdCompany = 1, Name = "Apple Inc.", Ticker = "AAPL", Sector = (Sector?)Sector.Technology, Market = (Market?)Market.NASDAQ, Currency = Currency.USD, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdCompany = 2, Name = "Microsoft Corp.", Ticker = "MSFT", Sector = (Sector?)Sector.Technology, Market = (Market?)Market.NASDAQ, Currency = Currency.USD, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdCompany = 3, Name = "Banco Santander", Ticker = "SAN", Sector = (Sector?)Sector.Financials, Market = (Market?)Market.BME, Currency = Currency.EUR, IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdCompany = 4, Name = "Shell plc", Ticker = "SHEL", Sector = (Sector?)Sector.Energy, Market = (Market?)Market.LSE, Currency = Currency.GBP, IdStatus = EntityStatus.Active, CreatedAt = SeedDate }
        );

        // Valuations: fila base (FK shadow IdCompany incluida en el objeto anónimo).
        modelBuilder.Entity<Domain.Investments.Entities.Valuation>().HasData(
            new { IdValuation = 1, IdCompany = 1, Date = new DateOnly(2026, 1, 2), Source = (string?)"seed", IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdValuation = 2, IdCompany = 1, Date = new DateOnly(2026, 3, 2), Source = (string?)"seed", IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdValuation = 3, IdCompany = 2, Date = new DateOnly(2026, 1, 2), Source = (string?)"seed", IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdValuation = 4, IdCompany = 2, Date = new DateOnly(2026, 3, 2), Source = (string?)"seed", IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdValuation = 5, IdCompany = 3, Date = new DateOnly(2026, 1, 2), Source = (string?)"seed", IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdValuation = 6, IdCompany = 3, Date = new DateOnly(2026, 3, 2), Source = (string?)"seed", IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdValuation = 7, IdCompany = 4, Date = new DateOnly(2026, 1, 2), Source = (string?)"seed", IdStatus = EntityStatus.Active, CreatedAt = SeedDate },
            new { IdValuation = 8, IdCompany = 4, Date = new DateOnly(2026, 3, 2), Source = (string?)"seed", IdStatus = EntityStatus.Active, CreatedAt = SeedDate }
        );

        // Owned type Price (Money): FK shadow del owned = "{Owner}{OwnerPk}" = "ValuationIdValuation".
        modelBuilder.Entity<Domain.Investments.Entities.Valuation>().OwnsOne(v => v.Price).HasData(
            new { ValuationIdValuation = 1, Amount = 195.0000m, Currency = Currency.USD },
            new { ValuationIdValuation = 2, Amount = 210.0000m, Currency = Currency.USD },
            new { ValuationIdValuation = 3, Amount = 420.0000m, Currency = Currency.USD },
            new { ValuationIdValuation = 4, Amount = 440.0000m, Currency = Currency.USD },
            new { ValuationIdValuation = 5, Amount = 4.5000m, Currency = Currency.EUR },
            new { ValuationIdValuation = 6, Amount = 4.8000m, Currency = Currency.EUR },
            new { ValuationIdValuation = 7, Amount = 28.0000m, Currency = Currency.GBP },
            new { ValuationIdValuation = 8, Amount = 30.0000m, Currency = Currency.GBP }
        );

        return modelBuilder;
    }
}
