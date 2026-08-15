namespace POS.Domain.Enums;

/// <summary>
/// Acción registrada en el <see cref="POS.Domain.Entities.AuditLog"/> (P2.1f).
/// Se guarda como cadena en Detail para no romper el log si una acción cambia.
/// </summary>
public enum AuditAction
{
    /// <summary>Inicio de sesión correcto.</summary>
    Login = 1,

    /// <summary>Intento de inicio de sesión con credenciales inválidas.</summary>
    LoginFailed = 2,

    /// <summary>Venta creada (Detail: número de recibo + total).</summary>
    SaleCreated = 3,

    /// <summary>Ajuste de stock (Detail: producto, tipo, cantidad, motivo).</summary>
    StockAdjusted = 4,

    /// <summary>Cambio de precio de un producto (Detail: anterior → nuevo).</summary>
    PriceChanged = 5,

    /// <summary>Apertura de caja (Detail: efectivo inicial).</summary>
    CashOpened = 6,

    /// <summary>Retiro de caja (Detail: monto + motivo).</summary>
    CashWithdrawn = 7,

    /// <summary>Cierre de caja (Detail: conteo, esperado, diferencia).</summary>
    CashClosed = 8
}