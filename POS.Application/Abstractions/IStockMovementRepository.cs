using POS.Domain.Entities;

namespace POS.Application.Abstractions;

/// <summary>
/// Puerta de salida de los movimientos de inventario (P3.2).
/// AddAsync solo marca el movimiento en el contexto; el guardado final ocurre
/// en la transacción del caso de uso (mismo DbContext), junto al stock del producto.
/// </summary>
public interface IStockMovementRepository
{
    Task AddAsync(StockMovement movement, CancellationToken ct = default);

    /// <summary>Historial de movimientos de un producto, más reciente primero.</summary>
    Task<List<StockMovement>> GetByProductAsync(long productId, CancellationToken ct = default);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}