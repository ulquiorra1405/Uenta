using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Infrastructure.Data;

namespace POS.Infrastructure.Repositories;

/// <summary>
/// Persiste devoluciones (P5.1) con numeración atómica propia: la secuencia
/// Id=2 de la tabla Sequences (Id=1 es de ventas) se incrementa con
/// UPSERT+RETURNING en la MISMA transacción que inserta la nota de crédito y
/// restaura el stock. Si algo falla, se revierte todo y no se quema un número.
/// </summary>
public class RefundRepository : IRefundRepository
{
    private readonly PosDbContext _db;

    public RefundRepository(PosDbContext db) => _db = db;

    public async Task<long> AddAsync(Refund refund, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var sql = """
            INSERT INTO Sequences(Id, LastNumber) VALUES (2, 1)
            ON CONFLICT(Id) DO UPDATE SET LastNumber = LastNumber + 1
            RETURNING LastNumber;
            """;
        var numbers = await _db.Database
            .SqlQueryRaw<long>(sql)
            .ToListAsync(ct);

        refund.Number = numbers.Single();

        _db.Refunds.Add(refund);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return refund.Id;
    }

    public async Task<Refund?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return await _db.Refunds
            .Include(r => r.Items)
            .Include(r => r.Payments)
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<List<Refund>> GetBySaleAsync(long saleId, CancellationToken ct = default)
    {
        return await _db.Refunds
            .Include(r => r.Items)
            .Where(r => r.OriginalSaleId == saleId && r.Status == RefundStatus.Completed)
            .ToListAsync(ct);
    }

    public async Task<List<Refund>> GetRecentAsync(int count = 20, CancellationToken ct = default)
    {
        // SQLite no traduce ORDER BY con DateTimeOffset → ordenar en memoria
        // (historial de la pantalla: pocas devoluciones, frecuencia baja).
        var refunds = await _db.Refunds
            .Include(r => r.Items)
            .Include(r => r.Payments)
            .Include(r => r.User)
            .Include(r => r.OriginalSale)
            .ToListAsync(ct);

        return refunds
            .OrderByDescending(r => r.CreatedAt)
            .Take(count)
            .ToList();
    }
}