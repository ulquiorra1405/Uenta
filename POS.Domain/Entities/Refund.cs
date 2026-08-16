using POS.Domain.Enums;
using POS.Domain.ValueObjects;

namespace POS.Domain.Entities;

/// <summary>
/// Devolución / nota de crédito (P5.1): revierte parcial o totalmente una venta,
/// devuelve el dinero (efectivo/tarjeta/transferencia) y restaura el stock.
/// <c>OriginalSaleId</c> null = devolución SIN recibo (manual, requiere permiso
/// <c>RefundNoReceipt</c> y motivo obligatorio).
/// </summary>
public class Refund
{
    public long Id { get; set; }

    /// <summary>Número de nota de crédito correlativo del negocio (secuencia propia).</summary>
    public long Number { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Vendedor que procesó la devolución.</summary>
    public long UserId { get; set; }

    public User? User { get; set; }

    /// <summary>Caja actual del vendedor (donde sale el efectivo si aplica).</summary>
    public long? CashSessionId { get; set; }

    /// <summary>Venta original; null = devolución sin recibo.</summary>
    public long? OriginalSaleId { get; set; }

    public Sale? OriginalSale { get; set; }

    /// <summary>Motivo. Obligatorio en devoluciones sin recibo.</summary>
    public string Reason { get; set; } = string.Empty;

    public RefundStatus Status { get; set; } = RefundStatus.Completed;

    /// <summary>Total devuelto (suma de líneas).</summary>
    public Money Total { get; set; }

    public List<RefundItem> Items { get; set; } = [];
    public List<RefundPayment> Payments { get; set; } = [];
}

/// <summary>Línea de una devolución (producto, cantidad y monto devuelto).</summary>
public class RefundItem
{
    public long Id { get; set; }
    public long RefundId { get; set; }
    public Refund? Refund { get; set; }

    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }

    /// <summary>Precio unitario cobrado al cliente en la venta (incluye ITBIS).</summary>
    public Money UnitPrice { get; set; }

    /// <summary>Monto devuelto de la línea: UnitPrice × Quantity.</summary>
    public Money Total { get; set; }
}

/// <summary>Reembolso de una devolución (uno o varios → pago mixto).</summary>
public class RefundPayment
{
    public long Id { get; set; }
    public long RefundId { get; set; }
    public Refund? Refund { get; set; }

    public PaymentMethod Method { get; set; }
    public Money Amount { get; set; }
}