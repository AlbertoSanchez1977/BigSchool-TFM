using BigSchool.Application.SharedKernel.Events;
using BigSchool.Application.SharedKernel.IntegrationEvents;
using BigSchool.Application.SharedKernel.IntegrationEvents.Contracts;
using BigSchool.Domain.Auth.Events;
using MediatR;

namespace BigSchool.Application.Auth.EventHandlers;

public sealed class PublishIntegrationEventHandler
    : INotificationHandler<DomainEventNotification<UserRegisteredDomainEvent>>
{
    private readonly IIntegrationEventOutbox _outbox;

    public PublishIntegrationEventHandler(IIntegrationEventOutbox outbox) => _outbox = outbox;

    public Task Handle(DomainEventNotification<UserRegisteredDomainEvent> notification, CancellationToken cancellationToken)
    {
        // EXCEPCIÓN CONSCIENTE a la norma "EventHandler → Send(Command)": aquí solo ENCOLAMOS la fila
        // de outbox y NO llamamos a SaveChangesAsync. El save-3 de la UoW componible la persiste en la
        // MISMA transacción que el User (atomicidad: no se "notifica" un registro que luego hace rollback).
        var u = notification.DomainEvent.User; // IdUser ya asignado (dispatch tras save-1)
        _outbox.Add(new UserRegisteredIntegrationEvent(
            Guid.NewGuid(), DateTime.UtcNow, u.IdUser, u.Email, u.FullName));
        return Task.CompletedTask;
    }
}
