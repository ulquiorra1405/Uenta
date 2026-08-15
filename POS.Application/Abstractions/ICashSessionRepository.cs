using POS.Domain.Entities;

namespace POS.Application.Abstractions;

/// <summary>Repositorio de sesiones de caja (P2.2).</summary>
public interface ICashSessionRepository
{
    Task<CashSession?> GetOpenByUserAsync(long userId, CancellationToken ct = default);
    Task<CashSession?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<long> AddAsync(CashSession session, CancellationToken ct = default);
    Task UpdateAsync(CashSession session, CancellationToken ct = default);
    Task AddWithdrawalAsync(CashWithdrawal withdrawal, CancellationToken ct = default);

    /// <summary>Suma de ventas en efectivo dentro de la sesión (para el cierre).</summary>
    Task<decimal> GetCashSalesTotalAsync(long cashSessionId, CancellationToken ct = default);

    /// <summary>Suma de ventas por método distinto de efectivo dentro de la sesión.</summary>
    Task<decimal> GetNonCashSalesTotalAsync(long cashSessionId, CancellationToken ct = default);
}