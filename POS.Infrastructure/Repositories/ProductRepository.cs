using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Domain.Entities;
using POS.Infrastructure.Data;

namespace POS.Infrastructure.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly PosDbContext _db;

    public ProductRepository(PosDbContext db) => _db = db;

    public Task<Product?> GetByIdAsync(long id, CancellationToken ct = default) =>
        _db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken ct = default) =>
        _db.Products.FirstOrDefaultAsync(p => p.Barcode == barcode, ct);

    public Task<Product?> GetBySkuAsync(string sku, CancellationToken ct = default) =>
        _db.Products.FirstOrDefaultAsync(p => p.Sku == sku, ct);

    public async Task<List<Product>> SearchAsync(string? term = null, CancellationToken ct = default)
    {
        var query = _db.Products.Include(p => p.Category).AsNoTracking();

        if (!string.IsNullOrWhiteSpace(term))
        {
            term = term.Trim();
            var lower = term.ToLowerInvariant();
            query = query.Where(p => p.IsActive &&
                (p.Name.ToLower().Contains(lower) ||
                 (p.Sku != null && p.Sku.ToLower().Contains(lower)) ||
                 (p.Barcode != null && p.Barcode.ToLower().Contains(lower))));
        }

        return await query.OrderBy(p => p.Name).ToListAsync(ct);
    }

    public Task<bool> ExistsBySkuAsync(string sku, long? excludeId = null, CancellationToken ct = default)
    {
        var query = _db.Products.Where(p => p.Sku == sku && p.IsActive);
        if (excludeId is long id)
            query = query.Where(p => p.Id != id);
        return query.AnyAsync(ct);
    }

    public async Task AddAsync(Product product, CancellationToken ct = default)
    {
        _db.Products.Add(product);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Marca el producto para actualizar. NO persiste: el guardado final
    /// lo hace la transacción del caso de uso (mismo DbContext).</summary>
    public Task UpdateAsync(Product product, CancellationToken ct = default)
    {
        _db.Products.Update(product);
        return Task.CompletedTask;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        _db.SaveChangesAsync(ct);
}
