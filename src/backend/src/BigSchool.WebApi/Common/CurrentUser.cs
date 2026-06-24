using System.Security.Claims;
using BigSchool.Application.Interfaces.Services;
using Microsoft.IdentityModel.JsonWebTokens;

namespace BigSchool.WebApi.Common;

public static class CurrentUser
{
    public static int GetId(ClaimsPrincipal principal, IUserIdEncryptor encryptor)
    {
        var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("Token sin identificador de usuario.");
        return encryptor.Decrypt(sub)
            ?? throw new UnauthorizedAccessException("Identificador de usuario inválido en el token.");
    }
}
