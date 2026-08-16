using POS.Domain.ValueObjects;

namespace POS.Domain.Entities;

/// <summary>
/// Compra a proveedor (P5.2): repone stock y registra el costo real del
/// producto (costo promedio ponderado). Solo contado en v1 — sin cuentas
/// por pagar (decisión con Bryan, 15-ago-2026). Documento interno, no fiscal.
/// </summary>
public class Purchase
{
    public long Id { get; set; }

    /// <summary>Número de compra correlativo del negocio (secuencia propia Id=3).</summary>
    public long Number { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Usuario que registró la compra.</summary>
    public long UserId { get; set; }

    public User? User { get; set; }

    /// <summary>Proveedor; null = compra sin proveedor (se permite en v1).</summary>
    public long? SupplierId { get; set; }

    public Supplier? Supplier { get; set; }

    /// <summary>Total pagado (suma de líneas: UnitCost × Quantity).</summary>
    public Money Total { get; set; }

    public List<PurchaseItem> Items { get; set; } = [];
}

/// <summary>Línea de una compra (producto, cantidad y costo unitario real).</summary>
public class PurchaseItem
{
    public long Id { get; set; }
    public long PurchaseId { get; set; }
    public Purchase? Purchase { get; set; }

    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }

    /// <summary>Costo unitario pagado al proveedor (no incluye ITBIS).</summary>
    public Money UnitCost { get; set; }

    /// <summary>Total de la línea: UnitCost × Quantity.</summary>
    public Money Total { get; set; }
}