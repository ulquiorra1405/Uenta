using POS.Domain.Entities;

namespace POS.Application.Abstractions;

/// <summary>
/// Puerta de salida de devoluciones (P5.1). Solo persiste: toda la validación
/// (caja, permisos, stock, reembolso) vive en RefundService.
/// </summary>
public interface IRefundRepository
{
    /// <summary>
    /// Persiste la devolución (items + pagos + restauración de stock) en una sola
    /// transacción, asignando <see cref="Refund.Number"/> de forma atómica
    /// (secuencia propia Id=2 con UPSERT+RETURNING). Si algo falla, se revierte todo.
    /// </summary>
    Task<long> AddAsync(Refund refund, CancellationToken ct = default);

    /// <summary>Devuelve la devolución con items y pagos.</summary>
    Task<Refund?> GetByIdAsync(long id, CancellationToken ct = default);

    /// <summary>Devoluciones de una venta (para no devolver más de lo vendido).</summary>
    Task<List<Refund>> GetBySaleAsync(long saleId, CancellationToken ct = default);

    /// <summary>Últimas devoluciones (historial de la pantalla), más reciente primero.</summary>
    Task<List<Refund>> GetRecentAsync(int count = 20, CancellationToken ct = default);
}