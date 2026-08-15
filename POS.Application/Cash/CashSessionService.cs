using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Application.Auth;
using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Application.Cash;

public record OpenCashRequest(long UserId, decimal InitialCash);
public record WithdrawRequest(long CashSessionId, decimal Amount, string Reason);
public record CloseCashRequest(long CashSessionId, decimal FinalCount);

public record CashSessionDto
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string? UserName { get; set; }
    public DateTimeOffset OpenedAt { get; set; }
    public decimal InitialCash { get; set; }
    public CashSessionStatus Status { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public decimal? FinalCount { get; set; }
    public decimal? Difference { get; set; }
    public decimal CashSalesTotal { get; set; }
    public decimal NonCashSalesTotal { get; set; }
    public decimal ExpectedCash { get; set; }
}

/// <summary>
/// Casos de uso de caja (P2.2): apertura con efectivo inicial, retiros con
/// motivo y cierre con conteo. Regla: UNA caja abierta por usuario a la vez.
/// </summary>
public class CashSessionService
{
    private readonly ICashSessionRepository _sessions;
    private readonly IClock _clock;
    private readonly AuditService _audit;

    public CashSessionService(ICashSessionRepository sessions, IClock clock, AuditService audit)
    {
        _sessions = sessions;
        _clock = clock;
        _audit = audit;
    }

    public async Task<Result<CashSessionDto>> OpenAsync(OpenCashRequest request, CancellationToken ct = default)
    {
        if (request.InitialCash < 0)
            return Result.Failure<CashSessionDto>("INVALID_INITIAL_CASH", "El efectivo inicial no puede ser negativo.");

        var open = await _sessions.GetOpenByUserAsync(request.UserId, ct);
        if (open is not null)
            return Result.Failure<CashSessionDto>("CASH_ALREADY_OPEN",
                $"Ya hay una caja abierta (#{open.Id}). Ciérrela antes de abrir otra.");

        var session = new CashSession
        {
            UserId = request.UserId,
            OpenedAt = _clock.Now,
            InitialCash = request.InitialCash,
            Status = CashSessionStatus.Open
        };

        var id = await _sessions.AddAsync(session, ct);
        await _audit.LogAsync(request.UserId, null, AuditAction.CashOpened,
            $"Caja #{id} abierta · fondo RD$ {request.InitialCash:N2}", ct);

        return Result.Success(ToDto(session, id, 0, 0));
    }

    public async Task<Result<CashSessionDto>> WithdrawAsync(WithdrawRequest request, CancellationToken ct = default)
    {
        if (request.Amount <= 0)
            return Result.Failure<CashSessionDto>("INVALID_AMOUNT", "El monto del retiro debe ser mayor que cero.");
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Result.Failure<CashSessionDto>("REASON_REQUIRED", "El motivo del retiro es obligatorio.");

        var session = await _sessions.GetByIdAsync(request.CashSessionId, ct);
        if (session is null)
            return Result.Failure<CashSessionDto>("CASH_NOT_FOUND", "La caja no existe.");
        if (session.Status == CashSessionStatus.Closed)
            return Result.Failure<CashSessionDto>("CASH_CLOSED", "La caja está cerrada; no se pueden hacer retiros.");

        var withdrawal = new CashWithdrawal
        {
            CashSessionId = session.Id,
            Amount = request.Amount,
            Reason = request.Reason.Trim(),
            CreatedAt = _clock.Now
        };
        await _sessions.AddWithdrawalAsync(withdrawal, ct);

        await _audit.LogAsync(session.UserId, null, AuditAction.CashWithdrawn,
            $"Caja #{session.Id} · retiro RD$ {request.Amount:N2} · {request.Reason.Trim()}", ct);

        var cashSales = await _sessions.GetCashSalesTotalAsync(session.Id, ct);
        var nonCash = await _sessions.GetNonCashSalesTotalAsync(session.Id, ct);
        return Result.Success(ToDto(session, session.Id, cashSales, nonCash));
    }

    public async Task<Result<CashSessionDto>> CloseAsync(CloseCashRequest request, CancellationToken ct = default)
    {
        var session = await _sessions.GetByIdAsync(request.CashSessionId, ct);
        if (session is null)
            return Result.Failure<CashSessionDto>("CASH_NOT_FOUND", "La caja no existe.");
        if (session.Status == CashSessionStatus.Closed)
            return Result.Failure<CashSessionDto>("CASH_ALREADY_CLOSED", "La caja ya está cerrada.");
        if (request.FinalCount < 0)
            return Result.Failure<CashSessionDto>("INVALID_COUNT", "El conteo no puede ser negativo.");

        var cashSales = await _sessions.GetCashSalesTotalAsync(session.Id, ct);
        var nonCash = await _sessions.GetNonCashSalesTotalAsync(session.Id, ct);
        var withdrawals = session.Withdrawals.Sum(w => w.Amount);
        var expected = session.InitialCash + cashSales - withdrawals;
        var difference = request.FinalCount - expected;

        session.Status = CashSessionStatus.Closed;
        session.ClosedAt = _clock.Now;
        session.FinalCount = request.FinalCount;
        session.Difference = difference;
        await _sessions.UpdateAsync(session, ct);

        await _audit.LogAsync(session.UserId, null, AuditAction.CashClosed,
            $"Caja #{session.Id} cerrada · conteo RD$ {request.FinalCount:N2} · esperado RD$ {expected:N2} · diferencia RD$ {difference:N2}", ct);

        var dto = ToDto(session, session.Id, cashSales, nonCash);
        dto.ExpectedCash = expected;
        return Result.Success(dto);
    }

    /// <summary>La caja abierta del usuario, con totales para mostrar en el badge/cierre.</summary>
    public async Task<CashSessionDto?> GetOpenForUserAsync(long userId, CancellationToken ct = default)
    {
        var session = await _sessions.GetOpenByUserAsync(userId, ct);
        if (session is null) return null;

        var cashSales = await _sessions.GetCashSalesTotalAsync(session.Id, ct);
        var nonCash = await _sessions.GetNonCashSalesTotalAsync(session.Id, ct);
        return ToDto(session, session.Id, cashSales, nonCash);
    }

    private static CashSessionDto ToDto(CashSession s, long id, decimal cashSales, decimal nonCash)
    {
        var withdrawals = s.Withdrawals.Sum(w => w.Amount);
        return new CashSessionDto
        {
            Id = id,
            UserId = s.UserId,
            UserName = s.User?.DisplayName,
            OpenedAt = s.OpenedAt,
            InitialCash = s.InitialCash,
            Status = s.Status,
            ClosedAt = s.ClosedAt,
            FinalCount = s.FinalCount,
            Difference = s.Difference,
            CashSalesTotal = cashSales,
            NonCashSalesTotal = nonCash,
            ExpectedCash = s.InitialCash + cashSales - withdrawals
        };
    }
}