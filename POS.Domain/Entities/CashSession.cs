using POS.Domain.Enums;

namespace POS.Domain.Entities;

/// <summary>
/// Sesión de caja (P2.2): apertura con efectivo inicial, retiros y cierre con
/// conteo. Regla: UNA caja abierta por usuario a la vez. La venta se asocia a la
/// caja abierta del cajero (<see cref="Sale.CashSessionId"/>).
/// </summary>
public class CashSession
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public User? User { get; set; }

    public DateTimeOffset OpenedAt { get; set; }

    /// <summary>Efectivo con el que se abre la caja (fondo).</summary>
    public decimal InitialCash { get; set; }

    public CashSessionStatus Status { get; set; } = CashSessionStatus.Open;

    public DateTimeOffset? ClosedAt { get; set; }

    /// <summary>Efectivo contado por el cajero al cerrar.</summary>
    public decimal? FinalCount { get; set; }

    /// <summary>Diferencia = FinalCount − (InitialCash + ventasEfectivo − retiros).</summary>
    public decimal? Difference { get; set; }

    public List<CashWithdrawal> Withdrawals { get; set; } = [];
}

/// <summary>
/// Retiro de efectivo de la caja durante una sesión abierta (P2.2c).
/// El motivo es obligatorio (regla de negocio).
/// </summary>
public class CashWithdrawal
{
    public long Id { get; set; }

    public long CashSessionId { get; set; }

    public CashSession? CashSession { get; set; }

    public decimal Amount { get; set; }

    public string Reason { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}