using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Domain.Entities;
using POS.Infrastructure.Data;

namespace POS.Infrastructure.Repositories;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly PosDbContext _db;

    public AuditLogRepository(PosDbContext db) => _db = db;

    public async Task<long> AddAsync(AuditLog entry, CancellationToken ct = default)
    {
        _db.AuditLogs.Add(entry);
        await _db.SaveChangesAsync(ct);
        return entry.Id;
    }

    public async Task<List<AuditLog>> GetRecentAsync(int take, CancellationToken ct = default)
    {
        // SQLite no ordena por DateTimeOffset en SQL → ordenar en memoria.
        var entries = await _db.AuditLogs.AsNoTracking()
            .Take(take * 2)
            .ToListAsync(ct);
        return entries.OrderByDescending(a => a.CreatedAt).Take(take).ToList();
    }
}