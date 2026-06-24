using BigSchool.Domain.Enums;

namespace BigSchool.Domain.ValueObjects;

/// <summary>
/// Snapshot de conversión monetaria: importe original, tipo aplicado, importe convertido a la moneda
/// base del usuario y fecha del tipo. Reutilizable en Transaction (Plan 2B), Holding y Valuation (Plan 3).
/// La capa Application resuelve el rate; el dominio nunca llama a servicios externos.
/// </summary>
public sealed record MoneyConversion(Money Original, decimal Rate, Money Base, DateOnly RateDate)
{
    // EF Core no puede pasar owned navigations como parámetros de constructor;
    // este ctor vacío le permite materializar el tipo y rellenar propiedades vía init.
    private MoneyConversion() : this(null!, 0m, null!, default) { }

    public static MoneyConversion Create(Money original, Currency baseCurrency, decimal rate, DateOnly rateDate)
    {
        if (rate <= 0m)
            throw new ArgumentException("El tipo de cambio debe ser positivo.", nameof(rate));

        if (original.Currency == baseCurrency && rate != 1m)
            throw new ArgumentException(
                "Si la moneda original es la moneda base, el tipo de cambio debe ser 1.", nameof(rate));

        var baseMoney = Money.Create(original.Amount * rate, baseCurrency);
        return new MoneyConversion(original, rate, baseMoney, rateDate);
    }
}
