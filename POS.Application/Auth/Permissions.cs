using POS.Domain.Enums;

namespace POS.Application.Auth;

/// <summary>
/// Matriz de permisos por rol (decisión Fase 1B, 15-ago-2026). Cada permiso es
/// una constante; la UI los usa en CanExecute y el sidebar decide qué se muestra.
/// Centralizar aquí evita "if rol == Admin" sueltos por el código.
/// </summary>
public static class Permissions
{
    // ─── Permisos ───
    public const string Sell = "Sell";                     // Vender / cobrar
    public const string ViewCosts = "ViewCosts";           // Ver costos
    public const string CloseCash = "CloseCash";           // Cerrar caja
    public const string ManageCatalog = "ManageCatalog";   // Productos + categorías
    public const string AdjustStock = "AdjustStock";       // Ajustar stock
    public const string ManageUsers = "ManageUsers";       // Gestionar usuarios
    public const string ViewAudit = "ViewAudit";           // Ver auditoría (vista Fase 1D)
    public const string ManageSettings = "ManageSettings"; // Configurar impresora/datos del negocio

    private static readonly Dictionary<UserRole, HashSet<string>> _matrix = new()
    {
        [UserRole.Admin] = new()
        {
            Sell, ViewCosts, CloseCash, ManageCatalog, AdjustStock, ManageUsers, ViewAudit, ManageSettings
        },
        [UserRole.Supervisor] = new()
        {
            Sell, CloseCash, ManageCatalog, AdjustStock, ManageSettings
        },
        [UserRole.Cajero] = new()
        {
            Sell
        },
    };

    public static bool Has(UserRole role, string permission)
        => _matrix.TryGetValue(role, out var perms) && perms.Contains(permission);
}