using POS.Domain.ValueObjects;

namespace POS.Application.Products;

public class CreateProductRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public string? Barcode { get; set; }
    public long? CategoryId { get; set; }

    /// <summary>Precio de venta al público, ITBIS incluido (P2).</summary>
    public decimal Price { get; set; }

    public decimal Cost { get; set; }
    public decimal Stock { get; set; }
    public decimal MinStock { get; set; }
}

public class UpdateProductRequest : CreateProductRequest
{
    public long Id { get; set; }
    public bool IsActive { get; set; }
}
