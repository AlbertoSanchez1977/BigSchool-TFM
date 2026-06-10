namespace BigSchool.Application.DTOs.Auth;

public record AuthResponseDto(string AccessToken, DateTime ExpiresAt, string Email, string FullName);
