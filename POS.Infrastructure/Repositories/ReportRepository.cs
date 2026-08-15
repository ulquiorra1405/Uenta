using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Infrastructure.Data;

namespace POS.Infrastructure.Repositories;

/// <summary>
/// Consultas de solo lectura para reportes (P4.2). Devuelve ventas completadas
/// dentro del rango [from, to), con Items y User cargados (los agregados se
/// calculan en <see cref="POS.Application.Reports.ReportService"/>).
/// </summary>
public class ReportRepository : IReportRepository
{
    private readonly PosDbContext _db;

    public ReportRepository(PosDbContext db) => _db = db;

    public async Task<List<Sale>> GetSalesAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        // EF Core 9 + SQLite no traduce comparaciones DateTimeOffset en el WHERE
        // ("could not be translated"). Para un POS local el volumen es manejable:
        // se materializan las ventas completadas y se filtra el rango en memoria.
        var completed = await _db.Sales
            .Include(s => s.Items)
            .Include(s => s.User)
            .Where(s => s.Status == SaleStatus.Completed)
            .AsNoTracking()
            .ToListAsync(ct);

        return completed
            .Where(s => s.CreatedAt >= from && s.CreatedAt < to)
            .ToList();
    }
}