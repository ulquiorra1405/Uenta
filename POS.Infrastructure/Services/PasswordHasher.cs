using System.Security.Cryptography;

namespace POS.Infrastructure.Services;

/// <summary>
/// Hash de contraseñas PBKDF2 (P2.1a) — Rfc2898DeriveBytes, sin dependencias.
/// Formato del hash guardado: "iteraciones.saltBase64.hashBase64" (versión implícita
/// en iteraciones, permite subirlas en el futuro sin romper hashes viejos).
/// 100k iteraciones SHA-256, salt 16 bytes, hash 32 bytes.
/// </summary>
public class PasswordHasher : POS.Application.Abstractions.IPasswordHasher
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string storedHash)
    {
        try
        {
            var parts = storedHash.Split('.');
            if (parts.Length != 3) return false;

            var iterations = int.Parse(parts[0]);
            var salt = Convert.FromBase64String(parts[1]);
            var expected = Convert.FromBase64String(parts[2]);

            var actual = Rfc2898DeriveBytes.Pbkdf2(
                password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch
        {
            return false; // hash corrupto → nunca valida
        }
    }
}