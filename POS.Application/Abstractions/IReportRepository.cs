using POS.Domain.Entities;

namespace POS.Application.Abstractions;

/// <summary>
/// Consultas de solo lectura para reportes (P4.2). Nunca escriben: los agregados
/// se calculan desde las ventas completadas dentro del rango solicitado.
/// </summary>
public interface IReportRepository
{
    /// <summary>Ventas completadas dentro de [from, to).</summary>
    Task<List<Sale>> GetSalesAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
}