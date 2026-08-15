using Microsoft.Extensions.DependencyInjection;
using POS.Application;
using POS.Application.Products;
using POS.Domain.Enums;
using POS.Infrastructure;
using POS.Infrastructure.Data;

namespace POS.Tests;

/// <summary>
/// Casos de uso del inventario (P3.2) con SQLite real (archivo temporal):
/// movimientos de entrada/salida/ajuste, validaciones y efecto sobre el stock.
/// </summary>
public class InventoryServiceTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"pos-inv-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _services;
    private readonly ProductService _productService;
    private readonly InventoryService _inventoryService;

    public InventoryServiceTests()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddInfrastructure($"Data Source={_dbPath};Pooling=False");
        _services = services.BuildServiceProvider();

        var db = _services.GetRequiredService<PosDbContext>();
        db.Database.EnsureCreated();

        _productService = _services.GetRequiredService<ProductService>();
        _inventoryService = _services.GetRequiredService<InventoryService>();
    }

    public void Dispose()
    {
        _services.Dispose();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private async Task<long> CreateProductAsync(decimal stock = 10)
    {
        var created = await _productService.CreateAsync(new CreateProductRequest
        {
            Name = "Café con leche",
            Sku = "CAF-001",
            Price = 100m,
            Cost = 35m,
            Stock = stock,
            MinStock = 5
        });
        return created.Value!.Id;
    }

    private AdjustStockRequest ValidRequest(long productId, StockMovementType type = StockMovementType.Entry, decimal quantity = 5)
        => new()
        {
            ProductId = productId,
            Type = type,
            Quantity = quantity,
            Reason = "Compra a proveedor",
            UserId = 1
        };

    private async Task<decimal> CurrentStockAsync(long productId)
    {
        var all = await _productService.SearchAllAsync();
        return all.Single(p => p.Id == productId).Stock;
    }

    [Fact]
    public async Task Entry_AddsStock_AndRecordsMovement()
    {
        var productId = await CreateProductAsync(stock: 10);

        var result = await _inventoryService.AdjustStockAsync(ValidRequest(productId, StockMovementType.Entry, 5));

        Assert.True(result.IsSuccess);
        Assert.Equal(15m, result.Value!.StockAfter);
        Assert.Equal(15m, await CurrentStockAsync(productId));
        Assert.Equal("Compra a proveedor", result.Value.Reason);
    }

    [Fact]
    public async Task Exit_SubtractsStock()
    {
        var productId = await CreateProductAsync(stock: 10);

        var result = await _inventoryService.AdjustStockAsync(ValidRequest(productId, StockMovementType.Exit, 4));

        Assert.True(result.IsSuccess);
        Assert.Equal(6m, result.Value!.StockAfter);
        Assert.Equal(6m, await CurrentStockAsync(productId));
    }

    [Fact]
    public async Task Exit_AllowsNegativeStock_DecisionP3()
    {
        var productId = await CreateProductAsync(stock: 3);

        var result = await _inventoryService.AdjustStockAsync(ValidRequest(productId, StockMovementType.Exit, 10));

        Assert.True(result.IsSuccess);
        Assert.Equal(-7m, result.Value!.StockAfter);
    }

    [Fact]
    public async Task Adjustment_SetsExactStock_ConteoFisico()
    {
        var productId = await CreateProductAsync(stock: 20);

        var result = await _inventoryService.AdjustStockAsync(ValidRequest(productId, StockMovementType.Adjustment, 7));

        Assert.True(result.IsSuccess);
        Assert.Equal(7m, result.Value!.StockAfter);
        Assert.Equal(7m, await CurrentStockAsync(productId));
    }

    [Fact]
    public async Task ZeroQuantity_Fails()
    {
        var productId = await CreateProductAsync();

        var result = await _inventoryService.AdjustStockAsync(ValidRequest(productId, StockMovementType.Entry, 0));

        Assert.True(result.IsFailure);
        Assert.Equal("INVALID_QUANTITY", result.ErrorCode);
        // El stock no cambió.
        Assert.Equal(10m, await CurrentStockAsync(productId));
    }

    [Fact]
    public async Task EmptyReason_Fails()
    {
        var productId = await CreateProductAsync();

        var request = ValidRequest(productId);
        request.Reason = "   ";

        var result = await _inventoryService.AdjustStockAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("REASON_REQUIRED", result.ErrorCode);
    }

    [Fact]
    public async Task NonexistentProduct_Fails()
    {
        var result = await _inventoryService.AdjustStockAsync(ValidRequest(99999));

        Assert.True(result.IsFailure);
        Assert.Equal("PRODUCT_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task History_ReturnsMovementsNewestFirst()
    {
        var productId = await CreateProductAsync(stock: 10);

        await _inventoryService.AdjustStockAsync(ValidRequest(productId, StockMovementType.Entry, 5));
        await _inventoryService.AdjustStockAsync(ValidRequest(productId, StockMovementType.Exit, 2));
        await _inventoryService.AdjustStockAsync(ValidRequest(productId, StockMovementType.Adjustment, 20));

        var history = await _inventoryService.GetByProductAsync(productId);

        Assert.Equal(3, history.Count);
        Assert.Equal(StockMovementType.Adjustment, history[0].Type);   // el último primero
        Assert.Equal(20m, history[0].StockAfter);
        Assert.Equal(StockMovementType.Entry, history[2].Type);        // el primero al final
    }
}