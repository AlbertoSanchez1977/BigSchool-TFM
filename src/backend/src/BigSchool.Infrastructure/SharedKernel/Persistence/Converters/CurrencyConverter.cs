using BigSchool.Domain.SharedKernel.Enums;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BigSchool.Infrastructure.SharedKernel.Persistence.Converters;

/// <summary>
/// Conversor EF reutilizable: Currency (enum) -> CHAR(3) usando el nombre ISO 4217 alpha-3.
/// Se reutiliza en Users.BaseCurrency, ExchangeRates y el owned type Money de Transaction (Plan 2B).
/// </summary>
public static class CurrencyConverter
{
    public static readonly ValueConverter<Currency, string> CharIso = new(
        v => v.ToString(),
        v => Enum.Parse<Currency>(v));
}
