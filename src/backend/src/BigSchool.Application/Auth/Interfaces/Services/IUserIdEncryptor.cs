namespace BigSchool.Application.Auth.Interfaces.Services;

public interface IUserIdEncryptor
{
    string Encrypt(int userId);
    int? Decrypt(string encryptedUserId);
}
