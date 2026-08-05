using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Domain.Entities;
using POS.Infrastructure.Data;

namespace POS.Infrastructure.Repositories;

public class SaleRepository : ISaleRepository
{
    private readonly PosDbContext _db;

    public SaleRepository(PosDbContext db) => _db = db;

    public async Task<long> GetNextNumberAsync(CancellationToken ct = default) =>
        (await _db.Sales.MaxAsync(s => (long?)s.Number, ct) ?? 0) + 1;

    /// <summary>Persiste la venta y TODOS los cambios pendientes del contexto
    /// (incluye el stock descontado en los productos) en una sola transacción.</summary>
    public async Task<long> AddAsync(Sale sale, CancellationToken ct = default)
    {
        _db.Sales.Add(sale);
        await _db.SaveChangesAsync(ct);
        return sale.Id;
    }
}
