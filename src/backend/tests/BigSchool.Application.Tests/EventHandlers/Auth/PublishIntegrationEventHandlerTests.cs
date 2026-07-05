using BigSchool.Application.Auth.EventHandlers;
using BigSchool.Application.SharedKernel.Events;
using BigSchool.Application.SharedKernel.IntegrationEvents;
using BigSchool.Application.SharedKernel.IntegrationEvents.Contracts;
using BigSchool.Domain.Auth.Entities;
using BigSchool.Domain.Auth.Events;
using Moq;
using Xunit;

namespace BigSchool.Application.Tests.EventHandlers.Auth;

public class PublishIntegrationEventHandlerTests
{
    [Fact]
    public async Task Handle_UserRegistered_EnqueuesUserRegisteredIntegrationEventWithUserData()
    {
        var outbox = new Mock<IIntegrationEventOutbox>();
        var handler = new PublishIntegrationEventHandler(outbox.Object);
        var user = User.Create("ada@example.com", "hash", "salt", "Ada");

        await handler.Handle(new DomainEventNotification<UserRegisteredDomainEvent>(
            new UserRegisteredDomainEvent(user)), CancellationToken.None);

        outbox.Verify(o => o.Add(It.Is<UserRegisteredIntegrationEvent>(
            e => e.Email == "ada@example.com" && e.FullName == "Ada")), Times.Once);
    }
}
