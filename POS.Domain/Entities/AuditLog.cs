using POS.Domain.Enums;

namespace POS.Domain.Entities;

/// <summary>
/// Registro de auditoría (P2.1f): quién hizo qué y cuándo.
/// Toda venta, ajuste de stock, cambio de precio, login y operación de caja
/// queda aquí. <see cref="Detail"/> guarda contexto legible por humanos/JSON.
/// </summary>
public class AuditLog
{
    public long Id { get; set; }

    /// <summary>Usuario que ejecutó la acción (0 = sistema, ej. fallo de login sin usuario).</summary>
    public long UserId { get; set; }

    public string? Username { get; set; }

    public AuditAction Action { get; set; }

    /// <summary>Contexto de la acción, ej. "Recibo #42 · RD$ 1.250,00".</summary>
    public string Detail { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}