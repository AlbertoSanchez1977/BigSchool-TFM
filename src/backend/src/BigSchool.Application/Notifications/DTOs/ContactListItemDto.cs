namespace BigSchool.Application.Notifications.DTOs;

public record ContactListItemDto(int IdContact, string FullName, string Email, string Message, DateTime CreatedAt);
