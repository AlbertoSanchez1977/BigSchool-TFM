namespace BigSchool.Application.Auth.DTOs;

public record AuthResponseDto(string AccessToken, DateTime ExpiresAt, string Email, string FullName);
