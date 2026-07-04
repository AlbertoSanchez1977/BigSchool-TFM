using BigSchool.Application.Notifications.Interfaces.Repositories;
using BigSchool.Domain.Notifications.Entities;
using MediatR;

namespace BigSchool.Application.Notifications.Commands.CreateContact;

public class CreateContactCommandHandler : IRequestHandler<CreateContactCommand, int>
{
    private readonly IContactRepository _contacts;

    public CreateContactCommandHandler(IContactRepository contacts) => _contacts = contacts;

    public async Task<int> Handle(CreateContactCommand request, CancellationToken cancellationToken)
    {
        var contact = Contact.Create(request.FullName, request.Email, request.Message);
        await _contacts.AddAsync(contact, cancellationToken);
        await _contacts.UnitOfWork.SaveChangesAsync(); // dispara ContactSubmitted → command del acuse; UoW componible = atómico
        return contact.IdContact;
    }
}
