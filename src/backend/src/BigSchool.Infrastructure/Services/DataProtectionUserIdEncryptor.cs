using Microsoft.AspNetCore.DataProtection;
using BigSchool.Application.Auth.Interfaces.Services;

namespace BigSchool.Infrastructure.Services;

public class DataProtectionUserIdEncryptor : IUserIdEncryptor
{
    private const string PURPOSE = "BigSchool.UserId.v1";
    private readonly IDataProtector _protector;

    public DataProtectionUserIdEncryptor(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector(PURPOSE);
    }

    public string Encrypt(int userId)
    {
        return _protector.Protect(userId.ToString());
    }

    public int? Decrypt(string encryptedUserId)
    {
        try
        {
            var decrypted = _protector.Unprotect(encryptedUserId);
            return int.TryParse(decrypted, out var userId) ? userId : null;
        }
        catch
        {
            return null;
        }
    }
}
