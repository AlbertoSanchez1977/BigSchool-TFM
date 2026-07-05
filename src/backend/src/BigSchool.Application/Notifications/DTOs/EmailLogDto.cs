namespace BigSchool.Application.Notifications.DTOs;

public record EmailLogDto(int IdEmailLog, int? IdUser, string Recipient, string Subject, string Body, short Type, DateTime SentAt);
