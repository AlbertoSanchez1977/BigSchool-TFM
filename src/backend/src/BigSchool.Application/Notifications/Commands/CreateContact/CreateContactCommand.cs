using MediatR;

namespace BigSchool.Application.Notifications.Commands.CreateContact;

public record CreateContactCommand(string FullName, string Email, string Message) : IRequest<int>;
