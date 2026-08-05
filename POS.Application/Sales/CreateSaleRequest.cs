using POS.Domain.Enums;

namespace POS.Application.Sales;

public class CreateSaleRequest
{
    public long UserId { get; set; }
    public long? CustomerId { get; set; }
    public long? CashSessionId { get; set; }

    public List<SaleItemRequest> Items { get; set; } = [];

    public List<PaymentRequest> Payments { get; set; } = [];

    /// <summary>Descuento global de la venta, en RD$ (opcional).</summary>
    public decimal GlobalDiscount { get; set; }
}

public class SaleItemRequest
{
    public long ProductId { get; set; }
    public decimal Quantity { get; set; }

    /// <summary>Opcional: si es null, se usa el precio vigente del producto.</summary>
    public decimal? UnitPrice { get; set; }

    /// <summary>Descuento de la línea, en RD$.</summary>
    public decimal LineDiscount { get; set; }
}

public class PaymentRequest
{
    public PaymentMethod Method { get; set; }
    public decimal Amount { get; set; }
}
