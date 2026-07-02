using BigSchool.Domain.SharedKernel.Enums;

namespace BigSchool.Application.SharedKernel.Interfaces.Services;

/// <summary>
/// Contrato de SharedKernel que expone la moneda base de un usuario sin que los módulos
/// de negocio (Finanzas, Investments) dependan del repositorio/entidad de Auth.
/// Implementado por Auth, publicado vía SharedKernel — así se respeta la frontera de módulo.
/// </summary>
public interface IUserBaseCurrencyProvider
{
    /// <summary>Devuelve la moneda base del usuario, o null si el usuario no existe.</summary>
    Task<Currency?> GetBaseCurrencyAsync(int userId, CancellationToken cancellationToken = default);
}
