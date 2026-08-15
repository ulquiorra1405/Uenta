using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Application.Products;

/// <summary>
/// Casos de uso del inventario (P3.2): registrar movimientos de entrada/salida/ajuste
/// con motivo y usuario, y consultar el historial de un producto. El stock del producto
/// es la consecuencia de sus movimientos — nunca se edita "a mano" desde aquí.
/// </summary>
public class InventoryService
{
    private readonly IProductRepository _products;
    private readonly IStockMovementRepository _movements;
    private readonly IClock _clock;

    public InventoryService(
        IProductRepository products,
        IStockMovementRepository movements,
        IClock clock)
    {
        _products = products;
        _movements = movements;
        _clock = clock;
    }

    /// <summary>
    /// Registra un movimiento y actualiza el stock del producto:
    /// Entry suma, Exit resta (permite negativo, decisión P3), Adjustment fija el valor.
    /// Persistencia atómica: movimiento + stock en el mismo contexto (una transacción).
    /// </summary>
    public async Task<Result<StockMovementDto>> AdjustStockAsync(
        AdjustStockRequest request, CancellationToken ct = default)
    {
        // 1. Validación básica
        if (request.Quantity <= 0)
            return Result.Failure<StockMovementDto>("INVALID_QUANTITY",
                "La cantidad del movimiento debe ser mayor que cero.");
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Result.Failure<StockMovementDto>("REASON_REQUIRED",
                "El motivo del movimiento es obligatorio.");

        // 2. Producto existe
        var product = await _products.GetByIdAsync(request.ProductId, ct);
        if (product is null)
            return Result.Failure<StockMovementDto>("PRODUCT_NOT_FOUND",
                "El producto no existe.");

        // 3. Aplicar el movimiento al stock
        var stockAfter = request.Type switch
        {
            StockMovementType.Entry => product.Stock + request.Quantity,
            StockMovementType.Exit => product.Stock - request.Quantity,
            StockMovementType.Adjustment => request.Quantity, // conteo físico declarado
            _ => product.Stock
        };

        product.Stock = stockAfter;

        var movement = new StockMovement
        {
            ProductId = product.Id,
            Type = request.Type,
            Quantity = request.Quantity,
            StockAfter = stockAfter,
            Reason = request.Reason.Trim(),
            UserId = request.UserId,
            CreatedAt = _clock.Now
        };

        // 4. Persistir (movimiento + stock en el mismo contexto → transacción única)
        await _products.UpdateAsync(product, ct);
        await _movements.AddAsync(movement, ct);
        await _movements.SaveChangesAsync(ct);

        // 5. DTO de salida (Id asignado por la persistencia)
        var dto = new StockMovementDto
        {
            Id = movement.Id,
            ProductId = product.Id,
            ProductName = product.Name,
            Type = movement.Type,
            Quantity = movement.Quantity,
            StockAfter = movement.StockAfter,
            Reason = movement.Reason,
            UserId = movement.UserId,
            CreatedAt = movement.CreatedAt
        };

        return Result.Success(dto);
    }

    /// <summary>Historial de movimientos de un producto, más reciente primero.</summary>
    public async Task<List<StockMovementDto>> GetByProductAsync(
        long productId, CancellationToken ct = default)
    {
        var movements = await _movements.GetByProductAsync(productId, ct);
        return movements
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new StockMovementDto
            {
                Id = m.Id,
                ProductId = m.ProductId,
                ProductName = m.Product?.Name ?? string.Empty,
                Type = m.Type,
                Quantity = m.Quantity,
                StockAfter = m.StockAfter,
                Reason = m.Reason,
                UserId = m.UserId,
                CreatedAt = m.CreatedAt
            })
            .ToList();
    }
}