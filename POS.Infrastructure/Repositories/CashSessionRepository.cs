using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Infrastructure.Data;

namespace POS.Infrastructure.Repositories;

public class CashSessionRepository : ICashSessionRepository
{
    private readonly PosDbContext _db;

    public CashSessionRepository(PosDbContext db) => _db = db;

    public Task<CashSession?> GetOpenByUserAsync(long userId, CancellationToken ct = default)
        => _db.CashSessions
            .Include(s => s.User)
            .Include(s => s.Withdrawals)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == CashSessionStatus.Open, ct);

    public Task<CashSession?> GetByIdAsync(long id, CancellationToken ct = default)
        => _db.CashSessions
            .Include(s => s.User)
            .Include(s => s.Withdrawals)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<long> AddAsync(CashSession session, CancellationToken ct = default)
    {
        _db.CashSessions.Add(session);
        await _db.SaveChangesAsync(ct);
        return session.Id;
    }

    public async Task UpdateAsync(CashSession session, CancellationToken ct = default)
    {
        _db.CashSessions.Update(session);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AddWithdrawalAsync(CashWithdrawal withdrawal, CancellationToken ct = default)
    {
        _db.CashWithdrawals.Add(withdrawal);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Efectivo recibido en la caja: suma de pagos EN EFECTIVO de sus ventas.</summary>
    public async Task<decimal> GetCashSalesTotalAsync(long cashSessionId, CancellationToken ct = default)
    {
        // SQLite no traduce el ValueConverter de Money en un Sum con Join → sumar en memoria
        // (operación de cierre: pocos pagos por caja, frecuencia baja).
        var payments = await _db.Payments.AsNoTracking()
            .Where(p => p.Sale.CashSessionId == cashSessionId && p.Method == PaymentMethod.Cash)
            .ToListAsync(ct);
        return payments.Sum(p => p.Amount.Amount);
    }

    /// <summary>Ventas pagadas por tarjeta/transferencia en la caja (referencia del cierre).</summary>
    public async Task<decimal> GetNonCashSalesTotalAsync(long cashSessionId, CancellationToken ct = default)
    {
        var payments = await _db.Payments.AsNoTracking()
            .Where(p => p.Sale.CashSessionId == cashSessionId && p.Method != PaymentMethod.Cash)
            .ToListAsync(ct);
        return payments.Sum(p => p.Amount.Amount);
    }

    /// <summary>Suma de DEVOLUCIONES en efectivo dentro de la sesión (restan del esperado, P5.1).</summary>
    public async Task<decimal> GetCashRefundsTotalAsync(long cashSessionId, CancellationToken ct = default)
    {
        var refunds = await _db.RefundPayments.AsNoTracking()
            .Where(p => p.Refund.CashSessionId == cashSessionId && p.Method == PaymentMethod.Cash)
            .ToListAsync(ct);
        return refunds.Sum(p => p.Amount.Amount);
    }
}