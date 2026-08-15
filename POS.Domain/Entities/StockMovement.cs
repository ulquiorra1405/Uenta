using POS.Domain.Enums;

namespace POS.Domain.Entities;

/// <summary>
/// Movimiento de inventario (P3.2): entrada, salida o ajuste de stock con motivo
/// y usuario. Es la fuente de auditoría del inventario: el stock del producto
/// refleja la suma de sus movimientos.
/// </summary>
public class StockMovement
{
    public long Id { get; set; }

    public long ProductId { get; set; }

    public Product? Product { get; set; }

    public StockMovementType Type { get; set; }

    /// <summary>Cantidad del movimiento, siempre positiva. El efecto sobre el stock
    /// depende del tipo: Entry suma, Exit resta, Adjustment fija el valor.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Stock resultante del producto después de este movimiento (histórico).</summary>
    public decimal StockAfter { get; set; }

    /// <summary>Motivo del movimiento (obligatorio): compra, merma, conteo, corrección…</summary>
    public string Reason { get; set; } = string.Empty;

    public long UserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}