using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Domain.Entities;
using POS.Infrastructure.Data;

namespace POS.Infrastructure.Repositories;

public class SettingRepository : ISettingRepository
{
    private readonly PosDbContext _db;

    public SettingRepository(PosDbContext db) => _db = db;

    public async Task<Setting?> GetByKeyAsync(string key, CancellationToken ct = default)
        => await _db.Settings.FirstOrDefaultAsync(s => s.Key == key, ct);

    public async Task<List<Setting>> GetAllAsync(CancellationToken ct = default)
        => await _db.Settings.AsNoTracking().ToListAsync(ct);

    /// <summary>Upsert por clave (única): inserta si no existe, actualiza si existe.</summary>
    public async Task SaveAsync(Setting setting, CancellationToken ct = default)
    {
        var existing = await _db.Settings.FirstOrDefaultAsync(s => s.Key == setting.Key, ct);
        if (existing is null)
        {
            _db.Settings.Add(setting);
        }
        else
        {
            existing.Value = setting.Value;
        }
        await _db.SaveChangesAsync(ct);
    }
}