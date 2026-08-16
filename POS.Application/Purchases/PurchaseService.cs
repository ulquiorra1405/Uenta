using POS.Application.Abstractions;
using POS.Application.Auth;
using POS.Application.Common;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.ValueObjects;

namespace POS.Application.Purchases;

public record CreatePurchaseLineRequest(long ProductId, decimal Quantity, decimal UnitCost);

public record CreatePurchaseRequest
{
    public long UserId { get; init; }
    public long? SupplierId { get; init; }
    public List<CreatePurchaseLineRequest> Items { get; init; } = [];
}

public record PurchaseLineDto
{
    public long ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public Money UnitCost { get; init; }
    public Money Total { get; init; }
}

public record PurchaseDto
{
    public long Id { get; init; }
    public long Number { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public long UserId { get; init; }
    public string? UserName { get; init; }
    public long? SupplierId { get; init; }
    public string? SupplierName { get; init; }
    public Money Total { get; init; }
    public List<PurchaseLineDto> Items { get; init; } = [];
}

public record CreateSupplierRequest(string Name, string Rnc, string Phone);

public record UpdateSupplierRequest(long Id, string Name, string Rnc, string Phone);

public record SupplierDto
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Rnc { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
}

/// <summary>
/// Caso de uso: Compras y proveedores (P5.2). Registrar compras que reponen
/// stock y registran el costo real del producto. Reglas:
/// - Costo promedio ponderado (decisión 15-ago): tras la compra,
///   Cost = (StockActual × CostoActual + Cantidad × CostoUnitario) / (StockActual + Cantidad).
/// - Solo contado en v1: no hay cuentas por pagar; la compra se paga al registrar.
/// - Proveedor opcional (v1); si se indica, debe existir.
/// - La compra genera un movimiento de stock tipo Entry con razón "Compra".
/// - Permiso ManagePurchases (Admin/Supervisor).
/// </summary>
public class PurchaseService
{
    private readonly IPurchaseRepository _purchases;
    private readonly ISupplierRepository _suppliers;
    private readonly IProductRepository _products;
    private readonly IStockMovementRepository _movements;
    private readonly IUserRepository _users;
    private readonly IClock _clock;
    private readonly AuditService _audit;

    public PurchaseService(
        IPurchaseRepository purchases,
        ISupplierRepository suppliers,
        IProductRepository products,
        IStockMovementRepository movements,
        IUserRepository users,
        IClock clock,
        AuditService audit)
    {
        _purchases = purchases;
        _suppliers = suppliers;
        _products = products;
        _movements = movements;
        _users = users;
        _clock = clock;
        _audit = audit;
    }

    /// <summary>
    /// Registra una compra: valida permiso y líneas, repone stock con
    /// movimiento Entry, recalcula el costo promedio ponderado y persiste todo
    /// en una sola transacción.
    /// </summary>
    public async Task<Result<PurchaseDto>> CreateAsync(CreatePurchaseRequest request, CancellationToken ct = default)
    {
        // 1. Usuario y permiso.
        var user = await _users.GetByIdAsync(request.UserId, ct);
        if (user is null)
            return Result.Failure<PurchaseDto>("USER_NOT_FOUND", "El usuario no existe.");
        if (!Permissions.Has(user.Role, Permissions.ManagePurchases))
            return Result.Failure<PurchaseDto>("PURCHASE_PERMISSION_DENIED",
                "Su rol no permite registrar compras.");

        // 2. Líneas.
        if (request.Items.Count == 0)
            return Result.Failure<PurchaseDto>("PURCHASE_EMPTY", "La compra no tiene productos.");

        // 3. Proveedor (opcional en v1; si se indica, debe existir).
        Supplier? supplier = null;
        if (request.SupplierId is { } sid)
        {
            supplier = await _suppliers.GetByIdAsync(sid, ct);
            if (supplier is null)
                return Result.Failure<PurchaseDto>("SUPPLIER_NOT_FOUND", "El proveedor no existe.");
        }

        // 4. Armar líneas: validar productos, reponer stock, recalcular costo promedio ponderado.
        var items = new List<PurchaseItem>();
        var total = Money.Zero;

        foreach (var line in request.Items)
        {
            if (line.Quantity <= 0)
                return Result.Failure<PurchaseDto>("INVALID_QUANTITY",
                    "La cantidad de una línea debe ser mayor que cero.");
            if (line.UnitCost < 0)
                return Result.Failure<PurchaseDto>("INVALID_COST",
                    "El costo unitario de una línea no puede ser negativo.");

            var product = await _products.GetByIdAsync(line.ProductId, ct);
            if (product is null)
                return Result.Failure<PurchaseDto>("PRODUCT_NOT_FOUND",
                    $"El producto {line.ProductId} no existe.");
            if (!product.IsActive)
                return Result.Failure<PurchaseDto>("PRODUCT_INACTIVE",
                    $"El producto '{product.Name}' está inactivo.");

            var lineTotal = Money.Round(line.UnitCost * line.Quantity);
            items.Add(new PurchaseItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Quantity = line.Quantity,
                UnitCost = new Money(line.UnitCost),
                Total = lineTotal
            });
            total += lineTotal;

            // Reponer stock + recalcular costo promedio ponderado.
            var oldCost = product.Cost.Amount;
            var oldStock = product.Stock;
            var newStock = oldStock + line.Quantity;
            var newCost = newStock > 0
                ? (oldStock * oldCost + line.Quantity * line.UnitCost) / newStock
                : line.UnitCost;

            product.Stock = newStock;
            product.Cost = Money.Round(newCost);

            var movement = new StockMovement
            {
                ProductId = product.Id,
                Type = StockMovementType.Entry,
                Quantity = line.Quantity,
                StockAfter = newStock,
                Reason = "Compra",
                UserId = request.UserId,
                CreatedAt = _clock.Now
            };
            await _products.UpdateAsync(product, ct);
            await _movements.AddAsync(movement, ct);
        }

        // 5. Persistir (compra + items + stock + movimientos en una transacción;
        //    el AddAsync del repo asigna el número y hace SaveChanges de todo el contexto).
        var purchase = new Purchase
        {
            CreatedAt = _clock.Now,
            UserId = request.UserId,
            SupplierId = request.SupplierId,
            Total = total,
            Items = items
        };

        var purchaseId = await _purchases.AddAsync(purchase, ct);
        await _movements.SaveChangesAsync(ct);

        // 6. Auditoría (P5.2): toda compra queda registrada con usuario y fecha.
        await _audit.LogAsync(user.Id, user.Username, AuditAction.PurchaseCreated,
            $"Compra #{purchase.Number} · RD$ {total.Amount:N2} · " +
            (supplier is null ? "sin proveedor" : supplier.Name), ct);

        // 7. DTO de salida.
        var dto = new PurchaseDto
        {
            Id = purchaseId,
            Number = purchase.Number,
            CreatedAt = purchase.CreatedAt,
            UserId = purchase.UserId,
            UserName = user.DisplayName,
            SupplierId = purchase.SupplierId,
            SupplierName = supplier?.Name,
            Total = total,
            Items = items.Select(i => new PurchaseLineDto
            {
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                UnitCost = i.UnitCost,
                Total = i.Total
            }).ToList()
        };

        return Result.Success(dto);
    }

    /// <summary>Últimas compras (historial de la pantalla), más reciente primero.</summary>
    public async Task<List<PurchaseDto>> GetRecentAsync(int count = 20, CancellationToken ct = default)
    {
        var purchases = await _purchases.GetRecentAsync(count, ct);
        return purchases.Select(p => new PurchaseDto
        {
            Id = p.Id,
            Number = p.Number,
            CreatedAt = p.CreatedAt,
            UserId = p.UserId,
            UserName = p.User?.DisplayName,
            SupplierId = p.SupplierId,
            SupplierName = p.Supplier?.Name,
            Total = p.Total,
            Items = p.Items.Select(i => new PurchaseLineDto
            {
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                UnitCost = i.UnitCost,
                Total = i.Total
            }).ToList()
        }).ToList();
    }

    // ─────────────────────────── Proveedores ───────────────────────────

    public async Task<List<SupplierDto>> GetSuppliersAsync(CancellationToken ct = default)
    {
        var suppliers = await _suppliers.GetAllAsync(ct);
        return suppliers.Select(ToSupplierDto).ToList();
    }

    public async Task<Result<SupplierDto>> CreateSupplierAsync(CreateSupplierRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result.Failure<SupplierDto>("SUPPLIER_NAME_REQUIRED", "El nombre del proveedor es obligatorio.");

        var rnc = NormalizeCode(request.Rnc);
        if (rnc is not null && await _suppliers.RncExistsAsync(rnc, ct: ct))
            return Result.Failure<SupplierDto>("SUPPLIER_RNC_DUPLICATED", $"Ya existe un proveedor con el RNC '{rnc}'.");

        var supplier = new Supplier
        {
            Name = request.Name.Trim(),
            Rnc = rnc ?? string.Empty,
            Phone = request.Phone.Trim(),
            CreatedAt = _clock.Now
        };

        await _suppliers.AddAsync(supplier, ct);
        await _audit.LogAsync(AuditAction.SupplierCreated,
            $"{supplier.Name}" + (string.IsNullOrEmpty(rnc) ? "" : $" · RNC {rnc}"), ct);

        return Result.Success(ToSupplierDto(supplier));
    }

    public async Task<Result<SupplierDto>> UpdateSupplierAsync(UpdateSupplierRequest request, CancellationToken ct = default)
    {
        var supplier = await _suppliers.GetByIdAsync(request.Id, ct);
        if (supplier is null)
            return Result.Failure<SupplierDto>("SUPPLIER_NOT_FOUND", "El proveedor no existe.");

        if (string.IsNullOrWhiteSpace(request.Name))
            return Result.Failure<SupplierDto>("SUPPLIER_NAME_REQUIRED", "El nombre del proveedor es obligatorio.");

        var rnc = NormalizeCode(request.Rnc);
        if (rnc is not null && await _suppliers.RncExistsAsync(rnc, request.Id, ct))
            return Result.Failure<SupplierDto>("SUPPLIER_RNC_DUPLICATED", $"Ya existe otro proveedor con el RNC '{rnc}'.");

        supplier.Name = request.Name.Trim();
        supplier.Rnc = rnc ?? string.Empty;
        supplier.Phone = request.Phone.Trim();

        await _suppliers.UpdateAsync(supplier, ct);
        return Result.Success(ToSupplierDto(supplier));
    }

    private static string? NormalizeCode(string? code) =>
        string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();

    private static SupplierDto ToSupplierDto(Supplier s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        Rnc = s.Rnc,
        Phone = s.Phone
    };
}