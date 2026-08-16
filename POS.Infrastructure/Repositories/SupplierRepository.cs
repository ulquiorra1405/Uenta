using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Domain.Entities;
using POS.Infrastructure.Data;

namespace POS.Infrastructure.Repositories;

public class SupplierRepository : ISupplierRepository
{
    private readonly PosDbContext _db;

    public SupplierRepository(PosDbContext db) => _db = db;

    public Task<List<Supplier>> GetAllAsync(CancellationToken ct = default)
        => _db.Suppliers.AsNoTracking().OrderBy(s => s.Name).ToListAsync(ct);

    public Task<Supplier?> GetByIdAsync(long id, CancellationToken ct = default)
        => _db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<bool> RncExistsAsync(string rnc, long? excludeId = null, CancellationToken ct = default)
    {
        var query = _db.Suppliers.AsNoTracking().Where(s => s.Rnc == rnc && s.Rnc != "");
        if (excludeId is { } id)
            query = query.Where(s => s.Id != id);
        return query.AnyAsync(ct);
    }

    public async Task<long> AddAsync(Supplier supplier, CancellationToken ct = default)
    {
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync(ct);
        return supplier.Id;
    }

    public async Task UpdateAsync(Supplier supplier, CancellationToken ct = default)
    {
        _db.Suppliers.Update(supplier);
        await _db.SaveChangesAsync(ct);
    }
}