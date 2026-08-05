using POS.Domain.Entities;

namespace POS.Application.Abstractions;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken ct = default);
    Task<Product?> GetBySkuAsync(string sku, CancellationToken ct = default);

    /// <summary>Lista productos activos (opcionalmente filtrados por término).</summary>
    Task<List<Product>> SearchAsync(string? term = null, CancellationToken ct = default);

    /// <summary>True si existe otro producto activo con el mismo SKU (excluyendo el indicado).</summary>
    Task<bool> ExistsBySkuAsync(string sku, long? excludeId = null, CancellationToken ct = default);

    Task AddAsync(Product product, CancellationToken ct = default);

    /// <summary>
    /// Marca el producto para actualizar. NO persiste por sí solo: el guardado
    /// final ocurre en la transacción del caso de uso (mismo DbContext).
    /// </summary>
    Task UpdateAsync(Product product, CancellationToken ct = default);

    /// <summary>Persiste los cambios pendientes del contexto (catálogo, desactivación).</summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
