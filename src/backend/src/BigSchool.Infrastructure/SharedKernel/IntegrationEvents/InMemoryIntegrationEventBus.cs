using BigSchool.Application.SharedKernel.IntegrationEvents;
using Microsoft.Extensions.DependencyInjection;

namespace BigSchool.Infrastructure.SharedKernel.IntegrationEvents;

/// <summary>
/// Bus in-process que resuelve por reflexión los IIntegrationEventHandler&lt;T&gt; del tipo concreto
/// del evento y los invoca. Interfaz idéntica a la de un bus real (RabbitMQ/SB): el día de mañana
/// solo cambia esta implementación, ni publishers ni handlers.
/// </summary>
public sealed class InMemoryIntegrationEventBus : IIntegrationEventBus
{
    private readonly IServiceProvider _serviceProvider;

    public InMemoryIntegrationEventBus(IServiceProvider serviceProvider)
        => _serviceProvider = serviceProvider;

    public async Task PublishAsync(IIntegrationEvent @event, CancellationToken cancellationToken)
    {
        var handlerType = typeof(IIntegrationEventHandler<>).MakeGenericType(@event.GetType());
        var method = handlerType.GetMethod(nameof(IIntegrationEventHandler<IIntegrationEvent>.HandleAsync))!;

        foreach (var handler in _serviceProvider.GetServices(handlerType))
        {
            await (Task)method.Invoke(handler, new object[] { @event, cancellationToken })!;
        }
    }
}
