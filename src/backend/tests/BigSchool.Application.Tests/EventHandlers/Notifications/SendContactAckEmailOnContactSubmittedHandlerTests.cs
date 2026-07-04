using BigSchool.Application.Notifications.Commands.CreateContactAckEmail;
using BigSchool.Application.Notifications.EventHandlers;
using BigSchool.Application.SharedKernel.Events;
using BigSchool.Domain.Notifications.Entities;
using BigSchool.Domain.Notifications.Events;
using MediatR;
using Moq;
using Xunit;

namespace BigSchool.Application.Tests.EventHandlers.Notifications;

public class SendContactAckEmailOnContactSubmittedHandlerTests
{
    [Fact]
    public async Task Handle_ContactSubmitted_SendsCreateContactAckEmailCommandWithContactData()
    {
        var mediator = new Mock<IMediator>();
        var handler = new SendContactAckEmailOnContactSubmittedHandler(mediator.Object);
        var contact = Contact.Create("Ada", "ada@example.com", "Hola");

        await handler.Handle(new DomainEventNotification<ContactSubmittedDomainEvent>(
            new ContactSubmittedDomainEvent(contact)), CancellationToken.None);

        mediator.Verify(m => m.Send(
            It.Is<CreateContactAckEmailCommand>(c => c.Recipient == "ada@example.com" && c.FullName == "Ada"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
