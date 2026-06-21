namespace BigSchool.Domain.Exceptions;

/// <summary>
/// Se intentó vender más shares de las disponibles (abiertas) de una Company en la cartera. → HTTP 400.
/// </summary>
public class InsufficientSharesDomainException : DomainException
{
    public InsufficientSharesDomainException(int companyId, decimal requested, decimal available)
        : base("INSUFFICIENT_SHARES",
            $"No hay shares suficientes de la empresa {companyId}: se intentó vender {requested} pero solo hay {available} abiertas.")
    { }
}
