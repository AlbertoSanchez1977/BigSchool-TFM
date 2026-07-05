using BigSchool.Application.Notifications.Interfaces.Repositories;
using BigSchool.Domain.Notifications.Entities;
using MediatR;

namespace BigSchool.Application.Notifications.Commands.CreateWelcomeEmail;

public class CreateWelcomeEmailCommandHandler : IRequestHandler<CreateWelcomeEmailCommand>
{
    private readonly IEmailLogRepository _emailLogs;

    public CreateWelcomeEmailCommandHandler(IEmailLogRepository emailLogs) => _emailLogs = emailLogs;

    public async Task Handle(CreateWelcomeEmailCommand request, CancellationToken cancellationToken)
    {
        await _emailLogs.AddAsync(EmailLog.CreateWelcome(request.IdUser, request.Recipient, request.FullName), cancellationToken);
        await _emailLogs.UnitOfWork.SaveChangesAsync();
    }
}
