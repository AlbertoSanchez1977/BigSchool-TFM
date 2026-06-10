using System.Security.Cryptography;
using System.Text;
using BigSchool.Application.Interfaces.Services;
using Isopoh.Cryptography.Argon2;

namespace BigSchool.Infrastructure.Services;

public class Argon2PasswordHasher : IPasswordHasher
{
    private const int PARALLELISM = 2;
    private const int MEMORY_COST = 65536;
    private const int ITERATIONS = 3;
    private const int HASH_LENGTH = 16;
    private const int SALT_LENGTH = 32;

    public (string Hash, string Salt) HashPassword(string password)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(SALT_LENGTH);
        var salt = Convert.ToBase64String(saltBytes);

        var config = new Argon2Config
        {
            Type = Argon2Type.HybridAddressing,
            Version = Argon2Version.Nineteen,
            Password = Encoding.UTF8.GetBytes(password),
            Salt = saltBytes,
            Threads = PARALLELISM,
            MemoryCost = MEMORY_COST,
            TimeCost = ITERATIONS,
            HashLength = HASH_LENGTH
        };

        using var argon2 = new Argon2(config);
        using var hashResult = argon2.Hash();
        var hash = Convert.ToBase64String(hashResult.Buffer);

        return (hash, salt);
    }

    public bool VerifyPassword(string password, string hash, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);

        var config = new Argon2Config
        {
            Type = Argon2Type.HybridAddressing,
            Version = Argon2Version.Nineteen,
            Password = Encoding.UTF8.GetBytes(password),
            Salt = saltBytes,
            Threads = PARALLELISM,
            MemoryCost = MEMORY_COST,
            TimeCost = ITERATIONS,
            HashLength = HASH_LENGTH
        };

        using var argon2 = new Argon2(config);
        using var hashResult = argon2.Hash();
        var computedHash = Convert.ToBase64String(hashResult.Buffer);

        return string.Equals(hash, computedHash, StringComparison.Ordinal);
    }
}
