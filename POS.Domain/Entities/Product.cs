using POS.Domain.ValueObjects;

namespace POS.Domain.Entities;

public class Product
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public string? Barcode { get; set; }
    public long? CategoryId { get; set; }
    public Category? Category { get; set; }

    /// <summary>Precio de venta al público. INCLUYE ITBIS 18% (decisión P2).</summary>
    public Money Price { get; set; }

    /// <summary>Costo del producto (para margen). No incluye ITBIS.</summary>
    public Money Cost { get; set; }

    /// <summary>
    /// Stock actual. Puede quedar negativo temporalmente (decisión P3):
    /// se permite vender sin stock y el carrito lo advierte, no lo bloquea.
    /// El manejo formal de stock/almacenes se define en una fase posterior.
    /// </summary>
    public decimal Stock { get; set; }

    /// <summary>Stock mínimo para alerta de reposición.</summary>
    public decimal MinStock { get; set; }

    public bool IsActive { get; set; } = true;
}
