using POS.Application.Abstractions;
using POS.Application.Auth;
using POS.Application.Common;
using POS.Application.Settings;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.ValueObjects;

namespace POS.Application.Sales;

/// <summary>
/// Caso de uso: CrearVenta. Toda la lógica de negocio de una venta vive aquí —
/// la UI (WPF hoy, API REST mañana) solo la invoca. Cero lógica en code-behind.
/// </summary>
public class SaleService
{
    public const decimal ItbisRate = CartCalculator.ItbisRate;

    private readonly IProductRepository _products;
    private readonly ISaleRepository _sales;
    private readonly IClock _clock;
    private readonly IUserRepository _users;
    private readonly ICashSessionRepository _cashSessions;
    private readonly SettingsService _settings;
    private readonly AuditService _audit;

    public SaleService(
        IProductRepository products,
        ISaleRepository sales,
        IClock clock,
        IUserRepository users,
        ICashSessionRepository cashSessions,
        SettingsService settings,
        AuditService audit)
    {
        _products = products;
        _sales = sales;
        _clock = clock;
        _users = users;
        _cashSessions = cashSessions;
        _settings = settings;
        _audit = audit;
    }

    public async Task<Result<SaleDto>> CreateSaleAsync(CreateSaleRequest request, CancellationToken ct = default)
    {
        // 0. Reglas de caja (P2.2): solo se cobra con caja ABIERTA del usuario.
        //    El CashSessionId lo resuelve la UI desde la sesión activa; aquí se valida
        //    que esa caja exista, esté abierta y pertenezca al vendedor.
        if (request.CashSessionId is null)
            return Result.Failure<SaleDto>("CASH_CLOSED", "Abra la caja para cobrar.");

        var cashSession = await _cashSessions.GetByIdAsync(request.CashSessionId.Value, ct);
        if (cashSession is null)
            return Result.Failure<SaleDto>("CASH_CLOSED", "Abra la caja para cobrar.");
        if (cashSession.Status == CashSessionStatus.Closed)
            return Result.Failure<SaleDto>("CASH_CLOSED", "La caja está cerrada. Ábrala para cobrar.");
        if (cashSession.UserId != request.UserId)
            return Result.Failure<SaleDto>("CASH_NOT_OWNED", "La caja activa no pertenece a este usuario.");

        // 0b. Tope de descuento por rol (P2.1d): Cajero ≤10%, Supervisor ≤25%, Admin ∞ — configurable.
        var seller = await _users.GetByIdAsync(request.UserId, ct);
        if (seller is null)
            return Result.Failure<SaleDto>("USER_NOT_FOUND", "El vendedor no existe.");

        // 1. Validación básica
        if (request.Items.Count == 0)
            return Result.Failure<SaleDto>("SALE_EMPTY", "La venta no tiene productos.");

        // 2. Cargar productos, armar líneas y descontar stock
        var items = new List<SaleItem>();
        var warnings = new List<string>();

        foreach (var line in request.Items)
        {
            if (line.Quantity <= 0)
                return Result.Failure<SaleDto>("INVALID_QUANTITY", "La cantidad de un producto debe ser mayor que cero.");

            var product = await _products.GetByIdAsync(line.ProductId, ct);
            if (product is null)
                return Result.Failure<SaleDto>("PRODUCT_NOT_FOUND", $"El producto {line.ProductId} no existe.");
            if (!product.IsActive)
                return Result.Failure<SaleDto>("PRODUCT_INACTIVE", $"El producto '{product.Name}' está inactivo.");

            var unitPrice = line.UnitPrice is decimal p ? new Money(p) : product.Price;
            var lineTotal = Money.Round(unitPrice.Amount * line.Quantity - line.LineDiscount);
            if (lineTotal.Amount < 0)
                return Result.Failure<SaleDto>("INVALID_DISCOUNT", $"El descuento de '{product.Name}' supera su total.");

            items.Add(new SaleItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Quantity = line.Quantity,
                UnitPrice = unitPrice,
                LineDiscount = new Money(line.LineDiscount),
                Total = lineTotal
            });

            // 3. Descontar stock — se permite negativo temporalmente (decisión P3)
            product.Stock -= line.Quantity;
            if (product.Stock < 0)
                warnings.Add($"'{product.Name}' quedó con stock negativo ({product.Stock:N0}).");

            await _products.UpdateAsync(product, ct);
        }

        // 4. Totales (ITBIS 18% incluido en el precio, desglose retail RD)
        //    Total = lo que paga el cliente. Subtotal = base imponible (sin ITBIS).
        var gross = Money.Zero;
        foreach (var item in items)
            gross += item.Total;

        var totals = CartCalculator.ComputeTotals(gross.Amount, request.GlobalDiscount);
        if (totals.DiscountExceedsSubtotal)
            return Result.Failure<SaleDto>("DISCOUNT_EXCEEDS_TOTAL", "El descuento global supera el total de la venta.");

        // 0c. Validar tope de descuento del rol ANTES de persistir (regla P2.1d).
        if (request.GlobalDiscount > 0)
        {
            var discountPercent = gross.Amount > 0 ? request.GlobalDiscount / gross.Amount * 100m : 0m;
            var limit = await GetDiscountLimitAsync(seller.Role, ct);
            if (limit is { } max && discountPercent > max)
                return Result.Failure<SaleDto>("DISCOUNT_LIMIT_EXCEEDED",
                    $"El descuento ({discountPercent:N1}%) supera el tope de su rol ({max:N0}%).");
        }

        var total = new Money(totals.Total);
        var itbis = new Money(totals.Itbis);
        var baseImponible = new Money(totals.BaseImponible);
        var discount = new Money(request.GlobalDiscount);

        // 5. Pagos (uno o varios → permite pago mixto)
        if (request.Payments.Count == 0)
            return Result.Failure<SaleDto>("NO_PAYMENT", "La venta no tiene ningún pago.");

        var paid = Money.Zero;
        var payments = new List<Payment>();
        foreach (var p in request.Payments)
        {
            if (p.Amount <= 0)
                return Result.Failure<SaleDto>("INVALID_PAYMENT", "Un pago tiene un monto inválido.");
            paid += new Money(p.Amount);
            payments.Add(new Payment { Method = p.Method, Amount = new Money(p.Amount) });
        }

        if (paid.Amount < total.Amount)
            return Result.Failure<SaleDto>("PAYMENT_INSUFFICIENT",
                $"Faltan RD$ {Money.Round(total.Amount - paid.Amount):N2} para completar la venta.");

        // 6. Persistir (stock + venta en una transacción; la numeración la asigna AddAsync)
        var sale = new Sale
        {
            CreatedAt = _clock.Now,
            UserId = request.UserId,
            CustomerId = request.CustomerId,
            CashSessionId = request.CashSessionId,
            Subtotal = baseImponible,
            Itbis = itbis,
            Discount = discount,
            Total = total,
            Status = SaleStatus.Completed,
            Items = items,
            Payments = payments
        };

        var saleId = await _sales.AddAsync(sale, ct);

        // 7. Auditoría (P2.1f): toda venta queda registrada con usuario y fecha.
        await _audit.LogAsync(seller.Id, seller.Username, AuditAction.SaleCreated,
            $"Recibo #{sale.Number} · RD$ {total.Amount:N2} · {items.Count} línea(s)", ct);

        // 8. DTO de salida
        var dto = new SaleDto
        {
            Id = saleId,
            Number = sale.Number,
            CreatedAt = sale.CreatedAt,
            UserId = sale.UserId,
            CustomerId = sale.CustomerId,
            Subtotal = baseImponible,
            Itbis = itbis,
            Discount = discount,
            Total = total,
            Items = items.Select(i => new SaleItemDto
            {
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                LineDiscount = i.LineDiscount,
                Total = i.Total
            }).ToList(),
            Payments = payments.Select(p => new PaymentDto { Method = p.Method, Amount = p.Amount }).ToList(),
            Warnings = warnings
        };

        return Result.Success(dto);
    }

    /// <summary>
    /// Tope de descuento global (%) según rol, desde Ajustes (configurable, regla P8).
    /// Admin y roles sin tope devuelven null (sin límite).
    /// </summary>
    private async Task<decimal?> GetDiscountLimitAsync(UserRole role, CancellationToken ct = default)
    {
        var key = role switch
        {
            UserRole.Cajero => SettingKeys.DiscountLimitCajero,
            UserRole.Supervisor => SettingKeys.DiscountLimitSupervisor,
            _ => null
        };
        if (key is null) return null;
        var raw = await _settings.GetIntAsync(key, role == UserRole.Cajero ? 10 : 25, ct);
        return raw > 0 ? raw : null;
    }
}
