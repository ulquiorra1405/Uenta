using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Domain.Entities;
using POS.Infrastructure.Data;

namespace POS.Infrastructure.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly PosDbContext _db;

    public CategoryRepository(PosDbContext db) => _db = db;

    public Task<List<Category>> GetAllActiveAsync(CancellationToken ct = default) =>
        _db.Categories.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

    /// <summary>Todas las categorías con conteo de productos (una sola consulta agrupada).</summary>
    public async Task<List<CategoryWithCount>> GetAllWithProductCountAsync(CancellationToken ct = default)
    {
        var categories = await _db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

        var counts = await _db.Products
            .Where(p => p.CategoryId != null)
            .GroupBy(p => p.CategoryId!.Value)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Count, ct);

        return categories
            .Select(c => new CategoryWithCount(c, counts.GetValueOrDefault(c.Id)))
            .ToList();
    }

    public Task<Category?> GetByIdAsync(long id, CancellationToken ct = default) =>
        _db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<bool> ExistsByNameAsync(string name, long? excludeId = null, CancellationToken ct = default)
    {
        var normalized = name.Trim().ToLowerInvariant();
        var query = _db.Categories.AsNoTracking().Where(c => c.Name.ToLower() == normalized);
        if (excludeId is not null)
            query = query.Where(c => c.Id != excludeId.Value);
        return query.AnyAsync(ct);
    }

    public async Task AddAsync(Category category, CancellationToken ct = default)
    {
        _db.Categories.Add(category);
        await _db.SaveChangesAsync(ct);
    }

    public Task UpdateAsync(Category category, CancellationToken ct = default)
    {
        _db.Categories.Update(category);
        return Task.CompletedTask;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        _db.SaveChangesAsync(ct);
}