using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Domain.Entities;
using POS.Infrastructure.Data;

namespace POS.Infrastructure.Repositories;

public class StockMovementRepository : IStockMovementRepository
{
    private readonly PosDbContext _db;

    public StockMovementRepository(PosDbContext db) => _db = db;

    /// <summary>Marca el movimiento para insertar. NO persiste: el guardado final lo
    /// hace el caso de uso (mismo DbContext) para que stock + movimiento queden
    /// en una sola transacción.</summary>
    public Task AddAsync(StockMovement movement, CancellationToken ct = default)
    {
        _db.StockMovements.Add(movement);
        return Task.CompletedTask;
    }

    public async Task<List<StockMovement>> GetByProductAsync(long productId, CancellationToken ct = default)
    {
        // SQLite no soporta DateTimeOffset en ORDER BY del servidor → se ordena en cliente.
        var movements = await _db.StockMovements
            .Include(m => m.Product)
            .Where(m => m.ProductId == productId)
            .ToListAsync(ct);

        return movements.OrderByDescending(m => m.CreatedAt).ToList();
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        _db.SaveChangesAsync(ct);
}