using BigSchool.Application.Notifications.Commands.CreateWelcomeEmail;
using BigSchool.Application.Notifications.EventHandlers;
using BigSchool.Application.SharedKernel.IntegrationEvents.Contracts;
using MediatR;
using Moq;
using Xunit;

namespace BigSchool.Application.Tests.EventHandlers.Notifications;

public class CreateWelcomeEmailTriggerTests
{
    [Fact]
    public async Task HandleAsync_UserRegisteredIntegrationEvent_SendsCreateWelcomeEmailCommand()
    {
        var mediator = new Mock<IMediator>();
        var handler = new CreateWelcomeEmailOnUserRegisteredHandler(mediator.Object);

        await handler.HandleAsync(new UserRegisteredIntegrationEvent(
            Guid.NewGuid(), DateTime.UtcNow, 7, "ada@example.com", "Ada"), CancellationToken.None);

        mediator.Verify(m => m.Send(
            It.Is<CreateWelcomeEmailCommand>(c => c.IdUser == 7 && c.Recipient == "ada@example.com"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
