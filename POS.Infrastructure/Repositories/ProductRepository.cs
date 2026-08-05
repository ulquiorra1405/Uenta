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

    /// <summary>Marca el producto para actualizar. NO persiste: el guardado final
    /// lo hace la transacción del caso de uso (mismo DbContext).</summary>
    public Task UpdateAsync(Product product, CancellationToken ct = default)
    {
        _db.Products.Update(product);
        return Task.CompletedTask;
    }
}
