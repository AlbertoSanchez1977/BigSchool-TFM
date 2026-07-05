using BigSchool.Domain.SharedKernel.Enums;

namespace BigSchool.Application.SharedKernel.Interfaces.Services;

/// <summary>
/// Contrato de SharedKernel que expone la moneda base de un usuario sin que los módulos
/// de negocio (Finance, Investments) dependan del repositorio/entidad de Auth.
/// Implementado por Auth, publicado vía SharedKernel — así se respeta la frontera de módulo.
///
/// Evolución a microservicio real: esta interfaz sobrevive sin cambios; solo cambia la
/// implementación en el consumidor. Dos caminos:
///   A) RPC síncrono (REST/gRPC) a Auth en tiempo de request — más simple, pero acopla la
///      disponibilidad/latencia de Finance/Investments a la de Auth (requiere retry/circuit
///      breaker/timeout/fallback).
///   B) Proyección local eventualmente consistente (recomendado) — Finance/Investments
///      mantienen su propia copia mínima (IdUser, BaseCurrency) alimentada por un
///      IIntegrationEventHandler que escucha el evento de Auth (p. ej. UserRegistered /
///      UserBaseCurrencyChanged) vía el mismo IIntegrationEventBus + Outbox ya construidos
///      (Tareas 6-7 del plan 018). La implementación del provider en el consumidor pasa a
///      leer esa proyección local en vez de llamar a Auth por red; Finance sigue funcionando
///      aunque Auth esté caído, al precio de consistencia eventual.
/// </summary>
public interface IUserBaseCurrencyProvider
{
    /// <summary>Devuelve la moneda base del usuario, o null si el usuario no existe.</summary>
    Task<Currency?> GetBaseCurrencyAsync(int userId, CancellationToken cancellationToken = default);
}
