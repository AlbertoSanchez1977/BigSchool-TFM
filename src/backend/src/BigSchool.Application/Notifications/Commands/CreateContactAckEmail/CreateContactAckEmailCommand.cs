using MediatR;

namespace BigSchool.Application.Notifications.Commands.CreateContactAckEmail;

public record CreateContactAckEmailCommand(string Recipient, string FullName) : IRequest;
