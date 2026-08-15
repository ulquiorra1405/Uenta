using POS.Application.Abstractions;
using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Application.Auth;

/// <summary>
/// Registro de auditoría (P2.1f). Los servicios de negocio lo inyectan para
/// dejar constancia de quién hizo qué: ventas, ajustes de stock, cambios de
/// precio, operaciones de caja y login.
/// </summary>
public class AuditService
{
    private readonly IAuditLogRepository _audit;
    private readonly IClock _clock;
    private readonly ICurrentSession _session;

    public AuditService(IAuditLogRepository audit, IClock clock, ICurrentSession session)
    {
        _audit = audit;
        _clock = clock;
        _session = session;
    }

    /// <summary>Registra una acción con el usuario de la sesión actual.</summary>
    public async Task LogAsync(AuditAction action, string detail, CancellationToken ct = default)
    {
        await _audit.AddAsync(new AuditLog
        {
            UserId = _session.CurrentUserId,
            Username = _session.CurrentUser?.Username,
            Action = action,
            Detail = detail,
            CreatedAt = _clock.Now
        }, ct);
    }

    /// <summary>Registra una acción con un usuario explícito (ej. login antes de sesión).</summary>
    public async Task LogAsync(long userId, string? username, AuditAction action, string detail, CancellationToken ct = default)
    {
        await _audit.AddAsync(new AuditLog
        {
            UserId = userId,
            Username = username,
            Action = action,
            Detail = detail,
            CreatedAt = _clock.Now
        }, ct);
    }
}