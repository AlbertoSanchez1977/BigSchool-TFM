using BigSchool.Application.Notifications.Commands.CreateWelcomeEmail;
using BigSchool.Application.Notifications.Interfaces.Repositories;
using BigSchool.Domain.Notifications.Entities;
using BigSchool.Domain.Notifications.Enums;
using BigSchool.Domain.SharedKernel.Interfaces;
using Moq;
using Xunit;

namespace BigSchool.Application.Tests.Commands.Notifications;

public class CreateWelcomeEmailCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidCommand_CreatesWelcomeEmailLogAndSaves()
    {
        var uow = new Mock<IUnitOfWork>();
        var repo = new Mock<IEmailLogRepository>();
        repo.SetupGet(r => r.UnitOfWork).Returns(uow.Object);
        var handler = new CreateWelcomeEmailCommandHandler(repo.Object);

        await handler.Handle(new CreateWelcomeEmailCommand(7, "ada@example.com", "Ada"), CancellationToken.None);

        repo.Verify(r => r.AddAsync(It.Is<EmailLog>(e => e.Type == EmailType.Welcome && e.IdUser == 7),
            It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(true), Times.Once);
    }
}
