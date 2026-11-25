
using System.Security.Cryptography;

namespace MaternityManagementSystem.Services;

public static class PasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 600_000;

    public static string HashPassword(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);

        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);

        return $"pbkdf2-sha256${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool VerifyPassword(
        string password,
        string storedHash)
    {
        if (string.IsNullOrEmpty(password) ||
            string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        string[] parts = storedHash.Split('$');

        if (parts.Length != 4 ||
            parts[0] != "pbkdf2-sha256" ||
            !int.TryParse(parts[1], out int iterations) ||
            iterations < 100_000 ||
            iterations > 2_000_000)
        {
            return false;
        }

        try
        {
            byte[] salt = Convert.FromBase64String(parts[2]);
            byte[] expectedHash = Convert.FromBase64String(parts[3]);

            if (salt.Length != SaltSize ||
                expectedHash.Length != HashSize)
            {
                return false;
            }

            byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                HashSize);

            return CryptographicOperations.FixedTimeEquals(
                actualHash,
                expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
