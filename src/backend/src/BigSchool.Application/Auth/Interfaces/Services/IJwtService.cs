namespace BigSchool.Application.Auth.Interfaces.Services;

public record JwtToken(string AccessToken, DateTime ExpiresAt);

public interface IJwtService
{
    JwtToken GenerateToken(int userId, string email);
    int? ExtractUserId(string token);
}
