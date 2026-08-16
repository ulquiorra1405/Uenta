using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Domain.Entities;
using POS.Infrastructure.Data;

namespace POS.Infrastructure.Repositories;

/// <summary>
/// Persiste compras (P5.2) con numeración atómica propia: la secuencia
/// Id=3 de la tabla Sequences (Id=1 es de ventas, Id=2 de devoluciones) se
/// incrementa con UPSERT+RETURNING en la MISMA transacción que inserta la
/// compra y actualiza el stock. Si algo falla, se revierte todo y no se quema
/// un número.
/// </summary>
public class PurchaseRepository : IPurchaseRepository
{
    private readonly PosDbContext _db;

    public PurchaseRepository(PosDbContext db) => _db = db;

    public async Task<long> AddAsync(Purchase purchase, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var sql = """
            INSERT INTO Sequences(Id, LastNumber) VALUES (3, 1)
            ON CONFLICT(Id) DO UPDATE SET LastNumber = LastNumber + 1
            RETURNING LastNumber;
            """;
        var numbers = await _db.Database
            .SqlQueryRaw<long>(sql)
            .ToListAsync(ct);

        purchase.Number = numbers.Single();

        _db.Purchases.Add(purchase);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return purchase.Id;
    }

    public async Task<Purchase?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return await _db.Purchases
            .Include(p => p.Items)
            .Include(p => p.User)
            .Include(p => p.Supplier)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<List<Purchase>> GetRecentAsync(int count = 20, CancellationToken ct = default)
    {
        // SQLite no traduce ORDER BY con DateTimeOffset → ordenar en memoria
        // (historial de la pantalla: pocas compras, frecuencia baja).
        var purchases = await _db.Purchases
            .Include(p => p.Items)
            .Include(p => p.User)
            .Include(p => p.Supplier)
            .ToListAsync(ct);

        return purchases
            .OrderByDescending(p => p.CreatedAt)
            .Take(count)
            .ToList();
    }
}