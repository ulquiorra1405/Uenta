using POS.Domain.Entities;

namespace POS.Application.Abstractions;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken ct = default);
    Task<Product?> GetBySkuAsync(string sku, CancellationToken ct = default);

    /// <summary>
    /// Marca el producto para actualizar. NO persiste por sí solo: el guardado
    /// final ocurre en la transacción del caso de uso (mismo DbContext).
    /// </summary>
    Task UpdateAsync(Product product, CancellationToken ct = default);
}
