using POS.Application.Abstractions;
using POS.Application.Common;
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
    public const decimal ItbisRate = 0.18m;

    private readonly IProductRepository _products;
    private readonly ISaleRepository _sales;
    private readonly IClock _clock;

    public SaleService(IProductRepository products, ISaleRepository sales, IClock clock)
    {
        _products = products;
        _sales = sales;
        _clock = clock;
    }

    public async Task<Result<SaleDto>> CreateSaleAsync(CreateSaleRequest request, CancellationToken ct = default)
    {
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

        var discount = new Money(request.GlobalDiscount);
        var total = gross - discount;
        if (total.Amount < 0)
            return Result.Failure<SaleDto>("DISCOUNT_EXCEEDS_TOTAL", "El descuento global supera el total de la venta.");

        var itbis = Money.Round(total.Amount * ItbisRate / (1 + ItbisRate)); // total → ITBIS (precio incluye ITBIS)
        var baseImponible = total - itbis;

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

        // 6. Persistir (stock + venta en una transacción)
        var sale = new Sale
        {
            Number = await _sales.GetNextNumberAsync(ct),
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

        // 7. DTO de salida
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

    /// <summary>ITBIS contenido en un total que YA lo incluye (precio retail RD).</summary>
    public static Money ItbisFromTotalIncluded(Money total) =>
        Money.Round(total.Amount * ItbisRate / (1 + ItbisRate));
}
