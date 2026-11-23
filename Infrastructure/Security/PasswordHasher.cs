using System.Security.Cryptography;
using ConceptJewelleryPro.Application.Security;

namespace ConceptJewelleryPro.Infrastructure.Security;

public sealed class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100_000;

    public string HashPassword(string password, out string salt)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
        salt = Convert.ToBase64String(saltBytes);

        return ComputeHash(password, saltBytes);
    }

    public bool VerifyPassword(string password, string salt, string expectedHash)
    {
        var cleanPassword = password?.Trim() ?? string.Empty;
        var cleanExpectedHash = expectedHash?.Trim() ?? string.Empty;

        // If salt is empty or hash matches plain text (case-insensitive)
        if (string.IsNullOrWhiteSpace(salt))
        {
            return string.Equals(cleanPassword, cleanExpectedHash, StringComparison.OrdinalIgnoreCase);
        }

        try
        {
            var saltBytes = Convert.FromBase64String(salt);
            var actualHash = ComputeHash(password, saltBytes);

            if (CryptographicOperations.FixedTimeEquals(
                Convert.FromBase64String(cleanExpectedHash),
                Convert.FromBase64String(actualHash)))
            {
                return true;
            }
        }
        catch
        {
        }

        return string.Equals(cleanPassword, cleanExpectedHash, StringComparison.OrdinalIgnoreCase);
    }

    private static string ComputeHash(string password, byte[] saltBytes)
    {
        using var deriveBytes = new Rfc2898DeriveBytes(password, saltBytes, Iterations, HashAlgorithmName.SHA256);
        return Convert.ToBase64String(deriveBytes.GetBytes(HashSize));
    }
}
