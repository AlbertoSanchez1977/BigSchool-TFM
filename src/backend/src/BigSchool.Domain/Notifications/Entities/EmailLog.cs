using BigSchool.Domain.Notifications.Enums;
using BigSchool.Domain.SharedKernel.Entities;
using BigSchool.Domain.SharedKernel.Enums;

namespace BigSchool.Domain.Notifications.Entities;

public class EmailLog : BaseEntity, IAggregateRoot
{
    public int IdEmailLog { get; private set; }
    public int? IdUser { get; private set; }
    public string Recipient { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public EmailType Type { get; private set; }
    public DateTime SentAt { get; private set; }
    public EntityStatus IdStatus { get; private set; }
    public DateTime CreatedAt { get; private set; }

    protected EmailLog() { } // EF Core

    private EmailLog(int? idUser, string recipient, string subject, string body,
        EmailType type, DateTime sentAt, EntityStatus idStatus, DateTime createdAt)
    {
        IdUser = idUser;
        Recipient = recipient;
        Subject = subject;
        Body = body;
        Type = type;
        SentAt = sentAt;
        IdStatus = idStatus;
        CreatedAt = createdAt;
    }

    public static EmailLog CreateWelcome(int idUser, string recipient, string fullName)
    {
        var now = DateTime.UtcNow;
        return new EmailLog(idUser, recipient,
            "¡Bienvenido a BigSchool!",
            $"Hola {fullName}, gracias por registrarte en BigSchool. Tu cuenta ya está lista.",
            EmailType.Welcome, now, EntityStatus.Active, now);
    }

    public static EmailLog CreateContactAck(string recipient, string fullName)
    {
        var now = DateTime.UtcNow;
        return new EmailLog(null, recipient,
            "Hemos recibido tu mensaje",
            $"Hola {fullName}, hemos recibido tu mensaje de contacto y te responderemos pronto.",
            EmailType.Contact, now, EntityStatus.Active, now);
    }
}
