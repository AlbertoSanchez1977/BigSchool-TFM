namespace BigSchool.Domain.Investments.Enums;

/// <summary>Ventana de la serie de cotización. El valor subyacente ES el nº de meses (evita magic values; compartido con el frontend).</summary>
public enum ValuationPeriod
{
    ThreeMonths = 3,
    SixMonths = 6,
    OneYear = 12,
    ThreeYears = 36,
    FiveYears = 60
}
