using MediatR;

namespace BigSchool.Application.Notifications.Commands.CreateWelcomeEmail;

public record CreateWelcomeEmailCommand(int IdUser, string Recipient, string FullName) : IRequest;
