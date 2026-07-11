namespace BigSchool.Application.Auth.DTOs;

public record UserProfileDto(int IdUser, string Email, string FullName, string BaseCurrency, DateTime? LastLoginDate);
