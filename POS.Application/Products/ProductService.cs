using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Domain.Entities;
using POS.Domain.ValueObjects;

namespace POS.Application.Products;

/// <summary>
/// Casos de uso del catálogo de productos. La UI (WPF o API) solo invoca estos
/// métodos; las reglas de negocio viven aquí.
/// </summary>
public class ProductService
{
    private readonly IProductRepository _products;

    public ProductService(IProductRepository products) => _products = products;

    public async Task<List<ProductDto>> SearchAsync(string? term = null, CancellationToken ct = default)
    {
        var products = await _products.SearchAsync(term, ct);
        return products.Select(ToDto).ToList();
    }

    public async Task<Result<ProductDto>> CreateAsync(CreateProductRequest request, CancellationToken ct = default)
    {
        var validation = Validate(request);
        if (validation is { } error)
            return Result.Failure<ProductDto>(error.ErrorCode, error.ErrorMessage);

        if (!string.IsNullOrWhiteSpace(request.Sku) && await _products.ExistsBySkuAsync(request.Sku!, ct: ct))
            return Result.Failure<ProductDto>("SKU_DUPLICATED", $"Ya existe un producto con el SKU '{request.Sku}'.");

        var product = new Product
        {
            Name = request.Name.Trim(),
            Sku = string.IsNullOrWhiteSpace(request.Sku) ? null : request.Sku.Trim(),
            Barcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : request.Barcode.Trim(),
            CategoryId = request.CategoryId,
            Price = new Money(request.Price),
            Cost = new Money(request.Cost),
            Stock = request.Stock,
            MinStock = request.MinStock,
            IsActive = true
        };

        await _products.AddAsync(product, ct);
        return Result.Success(ToDto(product));
    }

    public async Task<Result<ProductDto>> UpdateAsync(UpdateProductRequest request, CancellationToken ct = default)
    {
        var product = await _products.GetByIdAsync(request.Id, ct);
        if (product is null)
            return Result.Failure<ProductDto>("PRODUCT_NOT_FOUND", "El producto no existe.");

        var validation = Validate(request);
        if (validation is { } error)
            return Result.Failure<ProductDto>(error.ErrorCode, error.ErrorMessage);

        if (!string.IsNullOrWhiteSpace(request.Sku) &&
            await _products.ExistsBySkuAsync(request.Sku!, request.Id, ct))
            return Result.Failure<ProductDto>("SKU_DUPLICATED", $"Ya existe otro producto con el SKU '{request.Sku}'.");

        product.Name = request.Name.Trim();
        product.Sku = string.IsNullOrWhiteSpace(request.Sku) ? null : request.Sku.Trim();
        product.Barcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : request.Barcode.Trim();
        product.CategoryId = request.CategoryId;
        product.Price = new Money(request.Price);
        product.Cost = new Money(request.Cost);
        product.MinStock = request.MinStock;
        product.IsActive = request.IsActive;

        await _products.UpdateAsync(product, ct);
        await _products.SaveChangesAsync(ct);
        return Result.Success(ToDto(product));
    }

    /// <summary>Desactiva un producto (borrado lógico: conserva historial de ventas).</summary>
    public async Task<Result> DeactivateAsync(long id, CancellationToken ct = default)
    {
        var product = await _products.GetByIdAsync(id, ct);
        if (product is null)
            return Result.Failure("PRODUCT_NOT_FOUND", "El producto no existe.");

        product.IsActive = false;
        await _products.UpdateAsync(product, ct);
        await _products.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static (string ErrorCode, string ErrorMessage)? Validate(CreateProductRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ("NAME_REQUIRED", "El nombre del producto es obligatorio.");
        if (request.Price <= 0)
            return ("INVALID_PRICE", "El precio debe ser mayor que cero.");
        if (request.Cost < 0)
            return ("INVALID_COST", "El costo no puede ser negativo.");
        if (request.Stock < 0)
            return ("INVALID_STOCK", "El stock inicial no puede ser negativo.");
        if (request.MinStock < 0)
            return ("INVALID_MIN_STOCK", "El stock mínimo no puede ser negativo.");
        return null;
    }

    private static ProductDto ToDto(Product p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Sku = p.Sku,
        Barcode = p.Barcode,
        CategoryId = p.CategoryId,
        CategoryName = p.Category?.Name,
        Price = p.Price,
        Cost = p.Cost,
        Stock = p.Stock,
        MinStock = p.MinStock,
        IsActive = p.IsActive
    };
}
