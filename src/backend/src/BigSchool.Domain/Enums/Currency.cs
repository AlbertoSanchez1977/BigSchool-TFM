namespace BigSchool.Domain.Enums;

/// <summary>
/// Monedas soportadas. El NOMBRE del enum es el código ISO 4217 alpha-3 (se persiste como CHAR(3)).
/// El valor numérico es el código ISO 4217 numérico (informativo). Set reducido para la demo; ampliable.
/// </summary>
public enum Currency : short
{
    EUR = 978,
    USD = 840,
    GBP = 826,
    CHF = 756,
    JPY = 392
}
