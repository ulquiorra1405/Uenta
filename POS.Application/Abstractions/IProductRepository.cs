using POS.Domain.Entities;

namespace POS.Application.Abstractions;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken ct = default);
    Task<Product?> GetBySkuAsync(string sku, CancellationToken ct = default);

    /// <summary>Lista productos ACTIVOS (venta: dropdown de sugerencias, catálogo popup), filtrados por término.</summary>
    Task<List<Product>> SearchActiveAsync(string? term = null, CancellationToken ct = default);

    /// <summary>Lista TODOS los productos (gestión de catálogo, incluye inactivos), filtrados por término.</summary>
    Task<List<Product>> SearchAllAsync(string? term = null, CancellationToken ct = default);

    /// <summary>True si existe otro producto activo con el mismo SKU (excluyendo el indicado). Case-insensitive.</summary>
    Task<bool> ExistsBySkuAsync(string sku, long? excludeId = null, CancellationToken ct = default);

    /// <summary>True si existe otro producto activo con el mismo código de barras (excluyendo el indicado). Case-insensitive.</summary>
    Task<bool> ExistsByBarcodeAsync(string barcode, long? excludeId = null, CancellationToken ct = default);

    Task AddAsync(Product product, CancellationToken ct = default);

    /// <summary>
    /// Marca el producto para actualizar. NO persiste por sí solo: el guardado
    /// final ocurre en la transacción del caso de uso (mismo DbContext).
    /// </summary>
    Task UpdateAsync(Product product, CancellationToken ct = default);

    /// <summary>Persiste los cambios pendientes del contexto (catálogo, desactivación).</summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
