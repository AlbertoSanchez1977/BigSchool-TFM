namespace BigSchool.Application.SharedKernel.IntegrationEvents;

/// <summary>
/// Lo usa un command handler para encolar un IntegrationEvent DENTRO de su misma UoW.
/// La serialización + inserción de la fila la hace la implementación de Infrastructure;
/// el SaveChanges del agregado confirma agregado + outbox atómicamente.
/// </summary>
public interface IIntegrationEventOutbox
{
    void Add(IIntegrationEvent @event);
}
