using POS.Domain.ValueObjects;

namespace POS.Application.Products;

public class ProductDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public string? Barcode { get; set; }
    public long? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public Money Price { get; set; }      // incluye ITBIS (P2)
    public Money Cost { get; set; }
    public decimal Stock { get; set; }
    public decimal MinStock { get; set; }
    public bool IsActive { get; set; }

    public bool LowStock => Stock <= MinStock;
}
