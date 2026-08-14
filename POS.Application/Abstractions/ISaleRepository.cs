using POS.Domain.Entities;

namespace POS.Application.Abstractions;

public interface ISaleRepository
{
    /// <summary>
    /// Persiste la venta (items + pagos) y todos los cambios pendientes del
    /// contexto (incluye stock descontado) en una sola transacción. Asigna el
    /// <see cref="Sale.Number"/> de forma atómica (secuencia con UPSERT+RETURNING)
    /// en esa misma transacción: consecutivos, sin carreras ni huecos.
    /// </summary>
    Task<long> AddAsync(Sale sale, CancellationToken ct = default);
}