using BigSchool.Domain.SharedKernel.Enums;

namespace BigSchool.Domain.SharedKernel.Entities;

/// <summary>
/// Tipo de cambio cacheado (reference data). NO es Aggregate Root ni BaseEntity: sin DomainEvents,
/// sin IdStatus, sin repositorio. Solo lo lee/escribe ExchangeRateApiClient vía Dapper.
/// </summary>
public class ExchangeRate
{
    public int IdExchangeRate { get; private set; }
    public Currency FromCurrency { get; private set; }
    public Currency ToCurrency { get; private set; }
    public decimal Rate { get; private set; }
    public DateOnly RateDate { get; private set; }
    public string? Source { get; private set; }
    public DateTime FetchedAt { get; private set; }

    protected ExchangeRate() { } // EF Core / Dapper
}
