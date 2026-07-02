using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BigSchool.Application.SharedKernel.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using BigSchool.Application.Auth.Interfaces.Services;

namespace BigSchool.Infrastructure.Auth.Services;

public class JwtService : IJwtService
{
    private readonly JwtSettings _jwtSettings;
    private readonly IUserIdEncryptor _userIdEncryptor;

    public JwtService(IOptions<AppSettings> settings, IUserIdEncryptor userIdEncryptor)
    {
        _jwtSettings = settings.Value.Jwt;
        _userIdEncryptor = userIdEncryptor;
    }

    public JwtToken GenerateToken(int userId, string email)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes);

        var encryptedUserId = _userIdEncryptor.Encrypt(userId);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, encryptedUserId),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials
        );

        return new JwtToken(
            AccessToken: new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresAt: expiresAt
        );
    }

    public int? ExtractUserId(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        var sub = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;
        return sub is null ? null : _userIdEncryptor.Decrypt(sub);
    }
}
