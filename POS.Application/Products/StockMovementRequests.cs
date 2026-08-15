using POS.Domain.Enums;

namespace POS.Application.Products;

/// <summary>Petición para registrar un movimiento de inventario (P3.2).</summary>
public class AdjustStockRequest
{
    public long ProductId { get; set; }

    public StockMovementType Type { get; set; }

    /// <summary>Cantidad positiva del movimiento. En Adjustment, es el stock final declarado.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Motivo obligatorio (compra, merma, conteo, corrección…).</summary>
    public string Reason { get; set; } = string.Empty;

    public long UserId { get; set; }
}

/// <summary>Movimiento de inventario devuelto al cliente (UI/API).</summary>
public class StockMovementDto
{
    public long Id { get; set; }
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public StockMovementType Type { get; set; }
    public decimal Quantity { get; set; }
    public decimal StockAfter { get; set; }
    public string Reason { get; set; } = string.Empty;
    public long UserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}