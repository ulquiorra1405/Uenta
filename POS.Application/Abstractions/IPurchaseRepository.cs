using POS.Domain.Entities;

namespace POS.Application.Abstractions;

/// <summary>
/// Puerta de salida de compras (P5.2). Solo persiste: toda la validación
/// (permisos, líneas, costo promedio ponderado) vive en PurchaseService.
/// </summary>
public interface IPurchaseRepository
{
    /// <summary>
    /// Persiste la compra (items + actualización de stock) en una sola
    /// transacción, asignando <see cref="Purchase.Number"/> de forma atómica
    /// (secuencia propia Id=3 con UPSERT+RETURNING). Si algo falla, se revierte todo.
    /// </summary>
    Task<long> AddAsync(Purchase purchase, CancellationToken ct = default);

    Task<Purchase?> GetByIdAsync(long id, CancellationToken ct = default);

    /// <summary>Últimas compras (historial de la pantalla), más reciente primero.</summary>
    Task<List<Purchase>> GetRecentAsync(int count = 20, CancellationToken ct = default);
}