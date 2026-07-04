using BigSchool.Application.Notifications.Commands.CreateContactAckEmail;
using BigSchool.Application.SharedKernel.Events;
using BigSchool.Domain.Notifications.Events;
using MediatR;

namespace BigSchool.Application.Notifications.EventHandlers;

/// <summary>Disparador: traduce el hecho de dominio en el command que realiza la acción. No manipula estado.</summary>
public sealed class SendContactAckEmailOnContactSubmittedHandler
    : INotificationHandler<DomainEventNotification<ContactSubmittedDomainEvent>>
{
    private readonly IMediator _mediator;

    public SendContactAckEmailOnContactSubmittedHandler(IMediator mediator) => _mediator = mediator;

    public async Task Handle(DomainEventNotification<ContactSubmittedDomainEvent> notification, CancellationToken cancellationToken)
    {
        var c = notification.DomainEvent.Contact;
        await _mediator.Send(new CreateContactAckEmailCommand(c.Email, c.FullName), cancellationToken);
    }
}
