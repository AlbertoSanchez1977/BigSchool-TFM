using BigSchool.Domain.Notifications.Events;
using BigSchool.Domain.SharedKernel.Entities;
using BigSchool.Domain.SharedKernel.Enums;

namespace BigSchool.Domain.Notifications.Entities;

public class Contact : BaseEntity, IAggregateRoot
{
    public int IdContact { get; private set; }
    public string FullName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public EntityStatus IdStatus { get; private set; }
    public DateTime CreatedAt { get; private set; }

    protected Contact() { } // EF Core

    private Contact(string fullName, string email, string message, EntityStatus idStatus, DateTime createdAt)
    {
        FullName = fullName;
        Email = email;
        Message = message;
        IdStatus = idStatus;
        CreatedAt = createdAt;
    }

    public static Contact Create(string fullName, string email, string message)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full name is required.", nameof(fullName));
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new ArgumentException("Valid email is required.", nameof(email));
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message is required.", nameof(message));

        var contact = new Contact(
            fullName.Trim(), email.Trim().ToLowerInvariant(), message.Trim(),
            EntityStatus.Active, DateTime.UtcNow);
        contact.RaiseDomainEvent(new ContactSubmittedDomainEvent(contact));
        return contact;
    }
}
