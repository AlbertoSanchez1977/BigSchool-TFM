namespace BigSchool.Application.SharedKernel.IntegrationEvents;

public interface IIntegrationEventBus
{
    Task PublishAsync(IIntegrationEvent @event, CancellationToken cancellationToken);
}
