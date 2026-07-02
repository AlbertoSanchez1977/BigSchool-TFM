namespace BigSchool.Application.SharedKernel.IntegrationEvents;

/// <summary>Evento inter-módulo. Lleva solo primitivos + identidad/fecha. Nunca entidades de dominio.</summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTime OccurredOn { get; }
}
