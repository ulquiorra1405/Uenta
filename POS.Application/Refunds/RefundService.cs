using POS.Application.Abstractions;
using POS.Application.Auth;
using POS.Application.Common;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.ValueObjects;

namespace POS.Application.Refunds;

public record RefundItemRequest(long ProductId, decimal Quantity, decimal? UnitPrice = null);

public record RefundPaymentRequest(PaymentMethod Method, decimal Amount);

public record RefundableLineDto
{
    public long ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public decimal UnitPrice { get; init; }
    public decimal SoldQty { get; init; }
    public decimal RefundedQty { get; init; }
    public decimal AvailableQty => Math.Max(0, SoldQty - RefundedQty);
}

public record SalePreviewDto
{
    public long Id { get; init; }
    public long Number { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public string? UserName { get; init; }
    public string? CustomerName { get; init; }
    public List<RefundableLineDto> Lines { get; init; } = [];
}

public record CreateRefundRequest
{
    public long UserId { get; init; }
    public long CashSessionId { get; init; }
    public long? OriginalSaleId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public List<RefundItemRequest> Items { get; init; } = [];
    public List<RefundPaymentRequest> Payments { get; init; } = [];
}

public record RefundItemDto
{
    public long ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public Money UnitPrice { get; init; }
    public Money Total { get; init; }
}

public record RefundPaymentDto
{
    public PaymentMethod Method { get; init; }
    public Money Amount { get; init; }
}

public record RefundDto
{
    public long Id { get; init; }
    public long Number { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public long UserId { get; init; }
    public string? UserName { get; init; }
    public long? OriginalSaleId { get; init; }
    public long? OriginalSaleNumber { get; init; }
    public string Reason { get; init; } = string.Empty;
    public Money Total { get; init; }
    public List<RefundItemDto> Items { get; init; } = [];
    public List<RefundPaymentDto> Payments { get; init; } = [];
}

/// <summary>
/// Caso de uso: Devolución / nota de crédito (P5.1). Revertir una venta (total o
/// parcial), devolver el dinero y restaurar el stock. Reglas:
/// - La devolución se registra contra la caja ACTUAL del vendedor que la procesa.
/// - Con recibo: cualquier rol con Sell. Sin recibo: solo RefundNoReceipt + motivo.
/// - Reembolso en efectivo solo si la caja actual tiene efectivo disponible.
/// - No se puede devolver más de lo vendido en la venta original.
/// </summary>
public class RefundService
{
    private readonly IRefundRepository _refunds;
    private readonly ISaleRepository _sales;
    private readonly ICashSessionRepository _cashSessions;
    private readonly IProductRepository _products;
    private readonly IStockMovementRepository _movements;
    private readonly IUserRepository _users;
    private readonly IClock _clock;
    private readonly AuditService _audit;

    public RefundService(
        IRefundRepository refunds,
        ISaleRepository sales,
        ICashSessionRepository cashSessions,
        IProductRepository products,
        IStockMovementRepository movements,
        IUserRepository users,
        IClock clock,
        AuditService audit)
    {
        _refunds = refunds;
        _sales = sales;
        _cashSessions = cashSessions;
        _products = products;
        _movements = movements;
        _users = users;
        _clock = clock;
        _audit = audit;
    }

    public async Task<Result<RefundDto>> CreateAsync(CreateRefundRequest request, CancellationToken ct = default)
    {
        // 1. Caja abierta y propia (regla P2.2, misma que la venta).
        if (request.CashSessionId <= 0)
            return Result.Failure<RefundDto>("CASH_CLOSED", "Abra la caja para procesar devoluciones.");

        var cashSession = await _cashSessions.GetByIdAsync(request.CashSessionId, ct);
        if (cashSession is null || cashSession.Status == CashSessionStatus.Closed)
            return Result.Failure<RefundDto>("CASH_CLOSED", "La caja está cerrada. Ábrala para procesar devoluciones.");
        if (cashSession.UserId != request.UserId)
            return Result.Failure<RefundDto>("CASH_NOT_OWNED", "La caja activa no pertenece a este usuario.");

        var user = await _users.GetByIdAsync(request.UserId, ct);
        if (user is null)
            return Result.Failure<RefundDto>("USER_NOT_FOUND", "El usuario no existe.");

        // 2. Permiso por nivel: con recibo = Refund; sin recibo = RefundNoReceipt.
        var hasRefund = Permissions.Has(user.Role, Permissions.Refund);
        var hasRefundNoReceipt = Permissions.Has(user.Role, Permissions.RefundNoReceipt);
        if (!hasRefund)
            return Result.Failure<RefundDto>("REFUND_PERMISSION_DENIED", "Su rol no permite procesar devoluciones.");

        var withReceipt = request.OriginalSaleId is not null;
        if (!withReceipt && !hasRefundNoReceipt)
            return Result.Failure<RefundDto>("REFUND_NO_RECEIPT_PERMISSION",
                "La devolución sin recibo requiere un supervisor.");

        // 3. Motivo obligatorio solo sin recibo.
        if (!withReceipt && string.IsNullOrWhiteSpace(request.Reason))
            return Result.Failure<RefundDto>("REFUND_REASON_REQUIRED",
                "El motivo de la devolución sin recibo es obligatorio.");

        // 4. Líneas y stock.
        if (request.Items.Count == 0)
            return Result.Failure<RefundDto>("REFUND_EMPTY", "La devolución no tiene productos.");

        // 5. Venta original (si aplica): debe existir y no devolver más de lo vendido.
        Sale? original = null;
        if (withReceipt)
        {
            original = await _sales.GetByIdAsync(request.OriginalSaleId!.Value, ct);
            if (original is null)
                return Result.Failure<RefundDto>("SALE_NOT_FOUND", "La venta original no existe.");

            var alreadyRefunded = (await _refunds.GetBySaleAsync(original.Id, ct))
                .SelectMany(r => r.Items)
                .GroupBy(i => i.ProductId)
                .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

            foreach (var line in request.Items)
            {
                var soldQty = original.Items.Where(i => i.ProductId == line.ProductId).Sum(i => i.Quantity);
                var prevQty = alreadyRefunded.GetValueOrDefault(line.ProductId, 0);
                if (line.Quantity + prevQty > soldQty)
                    return Result.Failure<RefundDto>("REFUND_EXCEEDS_SALE",
                        $"No se puede devolver más de lo vendido del producto (línea '{original.Items.FirstOrDefault(i => i.ProductId == line.ProductId)?.ProductName}').");
            }
        }

        // 6. Armar líneas: validar productos, restaurar stock, registrar movimiento.
        var items = new List<RefundItem>();
        var gross = Money.Zero;

        foreach (var line in request.Items)
        {
            if (line.Quantity <= 0)
                return Result.Failure<RefundDto>("INVALID_QUANTITY",
                    "La cantidad de una línea debe ser mayor que cero.");

            var product = await _products.GetByIdAsync(line.ProductId, ct);
            if (product is null)
                return Result.Failure<RefundDto>("PRODUCT_NOT_FOUND",
                    $"El producto {line.ProductId} no existe.");
            if (!product.IsActive)
                return Result.Failure<RefundDto>("PRODUCT_INACTIVE",
                    $"El producto '{product.Name}' está inactivo.");

            var unitPrice = line.UnitPrice is decimal up ? new Money(up) : product.Price;
            var lineTotal = Money.Round(unitPrice.Amount * line.Quantity);
            items.Add(new RefundItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Quantity = line.Quantity,
                UnitPrice = unitPrice,
                Total = lineTotal
            });
            gross += lineTotal;

            // Restaurar stock + movimiento de entrada.
            product.Stock += line.Quantity;
            var stockAfter = product.Stock;
            var movement = new StockMovement
            {
                ProductId = product.Id,
                Type = StockMovementType.Entry,
                Quantity = line.Quantity,
                StockAfter = stockAfter,
                Reason = withReceipt
                    ? $"Devolución recibo #{original!.Number}"
                    : $"Devolución sin recibo · {request.Reason.Trim()}",
                UserId = request.UserId,
                CreatedAt = _clock.Now
            };
            await _products.UpdateAsync(product, ct);
            await _movements.AddAsync(movement, ct);
        }

        // 7. Reembolso: al menos un pago, montos válidos, suma == total.
        if (request.Payments.Count == 0)
            return Result.Failure<RefundDto>("NO_PAYMENT", "La devolución no tiene ningún reembolso.");

        var paid = Money.Zero;
        var payments = new List<RefundPayment>();
        foreach (var p in request.Payments)
        {
            if (p.Amount <= 0)
                return Result.Failure<RefundDto>("INVALID_PAYMENT", "Un reembolso tiene un monto inválido.");
            paid += new Money(p.Amount);
            payments.Add(new RefundPayment { Method = p.Method, Amount = new Money(p.Amount) });
        }

        if (paid.Amount != gross.Amount)
            return Result.Failure<RefundDto>("PAYMENT_MISMATCH",
                $"Los reembolsos (RD$ {paid.Amount:N2}) no coinciden con el total devuelto (RD$ {gross.Amount:N2}).");

        // 8. Candado de caja: si hay reembolso en efectivo, la caja actual debe tenerlo.
        var cashRefund = payments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount.Amount);
        if (cashRefund > 0)
        {
            var cashSales = await _cashSessions.GetCashSalesTotalAsync(cashSession.Id, ct);
            var cashRefundsPrev = await _cashSessions.GetCashRefundsTotalAsync(cashSession.Id, ct);
            var withdrawals = cashSession.Withdrawals.Sum(w => w.Amount);
            var available = cashSession.InitialCash + cashSales - cashRefundsPrev - withdrawals;
            if (cashRefund > available)
                return Result.Failure<RefundDto>("CASH_INSUFFICIENT",
                    $"La caja tiene RD$ {available:N2} en efectivo; el reembolso es RD$ {cashRefund:N2}.");
        }

        // 9. Persistir (nota + items + pagos + stock en una transacción).
        var refund = new Refund
        {
            CreatedAt = _clock.Now,
            UserId = request.UserId,
            CashSessionId = cashSession.Id,
            OriginalSaleId = request.OriginalSaleId,
            Reason = request.Reason.Trim(),
            Status = RefundStatus.Completed,
            Total = gross,
            Items = items,
            Payments = payments
        };

        var refundId = await _refunds.AddAsync(refund, ct);
        await _movements.SaveChangesAsync(ct);

        // 10. Auditoría (P5.1): toda devolución queda registrada con usuario y fecha.
        await _audit.LogAsync(user.Id, user.Username, AuditAction.RefundCreated,
            $"Nota #{refund.Number} · RD$ {gross.Amount:N2} · " +
            (withReceipt ? $"recibo #{original!.Number}" : "sin recibo") +
            (string.IsNullOrWhiteSpace(request.Reason) ? "" : $" · {request.Reason.Trim()}"), ct);

        // 11. DTO de salida.
        var dto = new RefundDto
        {
            Id = refundId,
            Number = refund.Number,
            CreatedAt = refund.CreatedAt,
            UserId = refund.UserId,
            UserName = user.DisplayName,
            OriginalSaleId = original?.Id,
            OriginalSaleNumber = original?.Number,
            Reason = refund.Reason,
            Total = gross,
            Items = items.Select(i => new RefundItemDto
            {
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                Total = i.Total
            }).ToList(),
            Payments = payments.Select(p => new RefundPaymentDto { Method = p.Method, Amount = p.Amount }).ToList()
        };

        return Result.Success(dto);
    }

    /// <summary>Últimas devoluciones (historial de la pantalla), más reciente primero.</summary>
    public async Task<List<RefundDto>> GetRecentAsync(int count = 20, CancellationToken ct = default)
    {
        var refunds = await _refunds.GetRecentAsync(count, ct);
        return refunds.Select(r => new RefundDto
        {
            Id = r.Id,
            Number = r.Number,
            CreatedAt = r.CreatedAt,
            UserId = r.UserId,
            UserName = r.User?.DisplayName,
            OriginalSaleId = r.OriginalSaleId,
            OriginalSaleNumber = r.OriginalSale?.Number,
            Reason = r.Reason,
            Total = r.Total,
            Items = r.Items.Select(i => new RefundItemDto
            {
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                Total = i.Total
            }).ToList(),
            Payments = r.Payments.Select(p => new RefundPaymentDto { Method = p.Method, Amount = p.Amount }).ToList()
        }).ToList();
    }

    /// <summary>
    /// Preview de una venta para devolver (con recibo): líneas con lo vendido,
    /// lo ya devuelto y lo disponible. La UI arma la devolución a partir de esto.
    /// </summary>
    public async Task<Result<SalePreviewDto>> GetSalePreviewAsync(long number, CancellationToken ct = default)
    {
        var sale = await _sales.GetByNumberAsync(number, ct);
        if (sale is null)
            return Result.Failure<SalePreviewDto>("SALE_NOT_FOUND", $"No existe la venta con recibo #{number}.");

        var refunds = await _refunds.GetBySaleAsync(sale.Id, ct);
        var already = refunds
            .SelectMany(r => r.Items)
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

        var lines = sale.Items
            .GroupBy(i => i.ProductId)
            .Select(g => new RefundableLineDto
            {
                ProductId = g.Key,
                ProductName = g.First().ProductName,
                UnitPrice = g.First().UnitPrice.Amount,
                SoldQty = g.Sum(i => i.Quantity),
                RefundedQty = already.GetValueOrDefault(g.Key, 0)
            })
            .ToList();

        var dto = new SalePreviewDto
        {
            Id = sale.Id,
            Number = sale.Number,
            CreatedAt = sale.CreatedAt,
            UserName = sale.User?.DisplayName,
            CustomerName = sale.Customer?.Name,
            Lines = lines
        };
        return Result.Success(dto);
    }
}