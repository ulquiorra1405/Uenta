using POS.Domain.Enums;

namespace POS.Domain.Entities;

/// <summary>
/// Usuario del sistema (P2.1). Contraseña guardada como hash PBKDF2 (nunca plano);
/// la verificación la hace <c>IPasswordHasher</c>. El login se valida contra
/// <c>IsActive</c>: un usuario desactivado no puede entrar.
/// </summary>
public class User
{
    public long Id { get; set; }

    /// <summary>Nombre de usuario para el login. Único, case-insensitive.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Nombre para mostrar (recibos, auditoría, header).</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Hash PBKDF2 (formato: saltBase64:hashBase64, 100k iteraciones).</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; }

    /// <summary>Usuario inactivo no puede iniciar sesión (soft delete, P2.1e).</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
}