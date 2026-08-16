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

    /// <summary>Venta con items y pagos (para validar devoluciones, P5.1).</summary>
    Task<Sale?> GetByIdAsync(long id, CancellationToken ct = default);

    /// <summary>Busca la venta por su número de recibo (devolución con recibo, P5.1).</summary>
    Task<Sale?> GetByNumberAsync(long number, CancellationToken ct = default);
}