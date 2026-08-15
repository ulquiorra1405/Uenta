using POS.Domain.Enums;
using POS.Domain.ValueObjects;

namespace POS.Application.Sales;

/// <summary>Venta lista para mostrar/exportar. Nunca se exponen entidades de dominio a la UI.</summary>
public class SaleDto
{
    public long Id { get; set; }
    public long Number { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public long UserId { get; set; }
    public long? CustomerId { get; set; }
    public long? CashSessionId { get; set; }

    public Money Subtotal { get; set; }
    public Money Itbis { get; set; }
    public Money Discount { get; set; }
    public Money Total { get; set; }

    public List<SaleItemDto> Items { get; set; } = [];
    public List<PaymentDto> Payments { get; set; } = [];

    /// <summary>Avisos no bloqueantes (ej.: producto quedó con stock negativo).</summary>
    public List<string> Warnings { get; set; } = [];
}

public class SaleItemDto
{
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public Money UnitPrice { get; set; }
    public Money LineDiscount { get; set; }
    public Money Total { get; set; }
}

public class PaymentDto
{
    public PaymentMethod Method { get; set; }
    public Money Amount { get; set; }
}
