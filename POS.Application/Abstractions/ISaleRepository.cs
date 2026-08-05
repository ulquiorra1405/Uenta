using POS.Domain.Entities;

namespace POS.Application.Abstractions;

public interface ISaleRepository
{
    /// <summary>Siguiente número de recibo correlativo del negocio.</summary>
    Task<long> GetNextNumberAsync(CancellationToken ct = default);

    /// <summary>
    /// Persiste la venta (items + pagos) y todos los cambios pendientes del
    /// contexto (incluye stock descontado) en una sola transacción.
    /// </summary>
    Task<long> AddAsync(Sale sale, CancellationToken ct = default);
}
