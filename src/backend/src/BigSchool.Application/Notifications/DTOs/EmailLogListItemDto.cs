namespace BigSchool.Application.Notifications.DTOs;

public record EmailLogListItemDto(int IdEmailLog, int? IdUser, string Recipient, string Subject, short Type, DateTime SentAt);
