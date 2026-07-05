using BigSchool.Application.Notifications.Commands.CreateWelcomeEmail;
using BigSchool.Application.SharedKernel.IntegrationEvents;
using BigSchool.Application.SharedKernel.IntegrationEvents.Contracts;
using MediatR;

namespace BigSchool.Application.Notifications.EventHandlers;

public sealed class CreateWelcomeEmailOnUserRegisteredHandler
    : IIntegrationEventHandler<UserRegisteredIntegrationEvent>
{
    private readonly IMediator _mediator;

    public CreateWelcomeEmailOnUserRegisteredHandler(IMediator mediator) => _mediator = mediator;

    public async Task HandleAsync(UserRegisteredIntegrationEvent evt, CancellationToken cancellationToken)
    {
        await _mediator.Send(new CreateWelcomeEmailCommand(evt.IdUser, evt.Email, evt.FullName), cancellationToken);
    }
}
