using POS.Domain.Entities;

namespace POS.Application.Abstractions;

/// <summary>Repositorio del registro de auditoría (P2.1f).</summary>
public interface IAuditLogRepository
{
    Task<long> AddAsync(AuditLog entry, CancellationToken ct = default);
    Task<List<AuditLog>> GetRecentAsync(int take, CancellationToken ct = default);
}