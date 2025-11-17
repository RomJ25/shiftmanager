using System.Security.Cryptography;

namespace ShiftManager.Models;

public static class PasswordHasher
{
    public static (byte[] hash, byte[] salt) CreateHash(string password)
    {
        using var rng = RandomNumberGenerator.Create();
        byte[] salt = new byte[16];
        rng.GetBytes(salt);
        using var derive = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
        return (derive.GetBytes(32), salt);
    }

    public static bool Verify(string password, byte[] hash, byte[] salt)
    {
        using var derive = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
        return CryptographicOperations.FixedTimeEquals(hash, derive.GetBytes(32));
    }
}
