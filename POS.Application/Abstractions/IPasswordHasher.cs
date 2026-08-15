namespace POS.Application.Abstractions;

/// <summary>
/// Hash de contraseñas (P2.1a). Implementación PBKDF2 con salt aleatorio:
/// el hash se guarda como "saltBase64:hashBase64" para no necesitar columnas extra.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string storedHash);
}