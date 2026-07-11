using BigSchool.Application.Notifications.Interfaces.Repositories;
using BigSchool.Domain.Notifications.Entities;
using MediatR;

namespace BigSchool.Application.Notifications.Commands.CreateContactAckEmail;

public class CreateContactAckEmailCommandHandler : IRequestHandler<CreateContactAckEmailCommand>
{
    private readonly IEmailLogRepository _emailLogs;

    public CreateContactAckEmailCommandHandler(IEmailLogRepository emailLogs) => _emailLogs = emailLogs;

    public async Task Handle(CreateContactAckEmailCommand request, CancellationToken cancellationToken)
    {
        await _emailLogs.AddAsync(EmailLog.CreateContactAck(request.Recipient, request.FullName), cancellationToken);
        await _emailLogs.UnitOfWork.SaveChangesAsync(); // UoW componible: participa en la transacción del Contact (atómico)
    }
}
