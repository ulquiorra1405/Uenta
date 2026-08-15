using POS.Domain.Enums;
using POS.Domain.ValueObjects;

namespace POS.Domain.Entities;

/// <summary>
/// Venta (emite Recibo, comprobante interno).
/// La factura con NCF (e-CF DGII) se modelará en Fase 2 como Invoice,
/// separada de Sale — solo se reserva el espacio aquí (decisión D8).
/// </summary>
public class Sale
{
    public long Id { get; set; }

    /// <summary>Número de recibo correlativo del negocio.</summary>
    public long Number { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public long UserId { get; set; }
    public long? CustomerId { get; set; }
    public long? CashSessionId { get; set; }

    /// <summary>Vendedor que registró la venta.</summary>
    public User? User { get; set; }

    /// <summary>Cliente asociado (P4.1); null = venta anónima.</summary>
    public Customer? Customer { get; set; }

    /// <summary>Base imponible (total − ITBIS). Subtotal + Itbis = Total.</summary>
    public Money Subtotal { get; set; }

    /// <summary>ITBIS 18% calculado sobre el total (el precio YA lo incluye).</summary>
    public Money Itbis { get; set; }

    /// <summary>Descuento global aplicado a la venta, en RD$.</summary>
    public Money Discount { get; set; }

    /// <summary>Total a pagar: Subtotal − Discount.</summary>
    public Money Total { get; set; }

    public SaleStatus Status { get; set; } = SaleStatus.Completed;

    public List<SaleItem> Items { get; set; } = [];
    public List<Payment> Payments { get; set; } = [];
}
