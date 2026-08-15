namespace POS.Domain.Enums;

/// <summary>
/// Rol de usuario (P2.1). Los permisos por rol se resuelven en
/// <c>POS.Application.Auth.Permissions</c> — el enum solo nombra el rol.
/// </summary>
public enum UserRole
{
    Admin = 1,
    Supervisor = 2,
    Cajero = 3
}