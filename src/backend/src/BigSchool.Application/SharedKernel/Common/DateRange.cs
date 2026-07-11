namespace BigSchool.Application.SharedKernel.Common;

public static class DateRange
{
    /// <summary>Válido si falta uno de los dos; si vienen ambos, from ≤ to y span ≤ 4 años.</summary>
    public static bool IsValid(DateOnly? from, DateOnly? to)
        => from is null || to is null || (from.Value <= to.Value && to.Value <= from.Value.AddYears(4));
}
