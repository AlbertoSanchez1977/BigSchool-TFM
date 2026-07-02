using BigSchool.Domain.SharedKernel.Enums;

namespace BigSchool.Application.Interfaces.Services;

public interface IExchangeRateProvider
{
    /// <summary>
    /// Devuelve el tipo de cambio from -> to para la fecha dada.
    /// Atajo: from == to devuelve 1 sin llamada. Resuelve cache (Dapper) -> API externa -> cache.
    /// </summary>
    Task<decimal> GetRateAsync(Currency from, Currency to, DateOnly date, CancellationToken ct = default);
}
