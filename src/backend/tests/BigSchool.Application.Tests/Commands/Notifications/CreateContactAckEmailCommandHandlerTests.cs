using BigSchool.Application.Notifications.Commands.CreateContactAckEmail;
using BigSchool.Application.Notifications.Interfaces.Repositories;
using BigSchool.Domain.Notifications.Entities;
using BigSchool.Domain.Notifications.Enums;
using BigSchool.Domain.SharedKernel.Interfaces;
using Moq;
using Xunit;

namespace BigSchool.Application.Tests.Commands.Notifications;

public class CreateContactAckEmailCommandHandlerTests
{
    private readonly Mock<IEmailLogRepository> _emailLogs = new();
    private readonly CreateContactAckEmailCommandHandler _handler;

    public CreateContactAckEmailCommandHandlerTests()
    {
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<bool>())).ReturnsAsync(1);
        _emailLogs.Setup(r => r.UnitOfWork).Returns(uow.Object);
        _handler = new CreateContactAckEmailCommandHandler(_emailLogs.Object);
    }

    [Fact]
    public async Task Handle_ValidRequest_CreatesContactAckEmailLogAndSaves()
    {
        await _handler.Handle(new CreateContactAckEmailCommand("ada@example.com", "Ada"), CancellationToken.None);

        _emailLogs.Verify(r => r.AddAsync(
            It.Is<EmailLog>(e => e.Type == EmailType.Contact && e.IdUser == null && e.Recipient == "ada@example.com"),
            It.IsAny<CancellationToken>()), Times.Once);
        _emailLogs.Verify(r => r.UnitOfWork.SaveChangesAsync(true), Times.Once);
    }
}
