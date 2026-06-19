using BigSchool.Domain.Enums;

namespace BigSchool.Domain.ValueObjects;

/// <summary>
/// Value Object de importe monetario. Inmutable, igualdad por valor.
/// El dominio crea instancias vía <see cref="Create"/>; el constructor posicional lo usa EF (owned type).
/// </summary>
public sealed record Money(decimal Amount, Currency Currency)
{
    /// <summary>Escala de los importes monetarios (simplificación TFM: 2 decimales para todas las monedas).</summary>
    public const int Scale = 2;

    /// <summary>Crea un importe válido, redondeando a la escala de columna con redondeo bancario.</summary>
    public static Money Create(decimal amount, Currency currency)
    {
        var rounded = Math.Round(amount, Scale, MidpointRounding.ToEven);
        return new Money(rounded, currency);
    }
}
