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
    public const string Refund = "Refund";                 // Devolución CON recibo (operación de mostrador)
    public const string RefundNoReceipt = "RefundNoReceipt"; // Devolución SIN recibo (riesgo: requiere supervisor)
    public const string ViewCosts = "ViewCosts";           // Ver costos
    public const string CloseCash = "CloseCash";           // Cerrar caja
    public const string ManageCatalog = "ManageCatalog";   // Productos + categorías
    public const string AdjustStock = "AdjustStock";       // Ajustar stock
    public const string ManageUsers = "ManageUsers";       // Gestionar usuarios
    public const string ViewAudit = "ViewAudit";           // Ver auditoría (vista Fase 1D)
    public const string ManageSettings = "ManageSettings"; // Configurar impresora/datos del negocio
    public const string ManagePurchases = "ManagePurchases"; // Registrar compras a proveedores (stock + costo)
    public const string ManageSuppliers = "ManageSuppliers"; // Gestionar proveedores (datos maestros)

    private static readonly Dictionary<UserRole, HashSet<string>> _matrix = new()
    {
        [UserRole.Admin] = new()
        {
            Sell, Refund, RefundNoReceipt, ViewCosts, CloseCash, ManageCatalog, AdjustStock, ManageUsers, ViewAudit, ManageSettings, ManagePurchases, ManageSuppliers
        },
        [UserRole.Supervisor] = new()
        {
            Sell, Refund, RefundNoReceipt, CloseCash, ManageCatalog, AdjustStock, ManageSettings, ManagePurchases, ManageSuppliers
        },
        [UserRole.Cajero] = new()
        {
            Sell, Refund
        },
    };

    public static bool Has(UserRole role, string permission)
        => _matrix.TryGetValue(role, out var perms) && perms.Contains(permission);
}