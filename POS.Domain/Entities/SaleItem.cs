using POS.Domain.ValueObjects;

namespace POS.Domain.Entities;

public class SaleItem
{
    public long Id { get; set; }
    public long SaleId { get; set; }
    public Sale Sale { get; set; } = null!;

    public long ProductId { get; set; }
    /// <summary>Nombre congelado al momento de la venta (el catálogo puede cambiar después).</summary>
    public string ProductName { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    /// <summary>Precio unitario cobrado. INCLUYE ITBIS.</summary>
    public Money UnitPrice { get; set; }

    /// <summary>Descuento total de la línea, en RD$.</summary>
    public Money LineDiscount { get; set; }

    /// <summary>Total de la línea: (UnitPrice × Quantity) − LineDiscount.</summary>
    public Money Total { get; set; }
}
