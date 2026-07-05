using BigSchool.Application.Auth.Interfaces.Repositories;
using BigSchool.Application.SharedKernel.Interfaces.Services;
using BigSchool.Domain.SharedKernel.Enums;

namespace BigSchool.Infrastructure.Auth.Services;

/// <summary>
/// Implementación válida mientras Auth vive en el mismo proceso/BD que sus consumidores
/// (monolito modular): consulta el repositorio de Auth directamente, sin red de por medio.
///
/// Si Finance/Investments se extraen algún día a microservicios separados, esta clase deja
/// de tener sentido TAL CUAL en el consumidor (no se puede inyectar por DI una implementación
/// que vive en el código de otro servicio). El contrato <see cref="IUserBaseCurrencyProvider"/>
/// sobrevive; lo que cambia es dónde y cómo se implementa en el lado consumidor — ver el
/// comentario de la interfaz para las dos opciones (RPC síncrono vs. proyección local vía
/// IIntegrationEventBus + Outbox, que es lo recomendado).
/// </summary>
public sealed class UserBaseCurrencyProvider : IUserBaseCurrencyProvider
{
    private readonly IUserRepository _userRepository;

    public UserBaseCurrencyProvider(IUserRepository userRepository) => _userRepository = userRepository;

    public async Task<Currency?> GetBaseCurrencyAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        return user?.BaseCurrency;
    }
}
