using Microsoft.Extensions.DependencyInjection;
using POS.Application;
using POS.Application.Abstractions;
using POS.Application.Sales;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.ValueObjects;
using POS.Infrastructure;
using POS.Infrastructure.Data;

namespace POS.Tests;

/// <summary>
/// Integración real: Application + Infrastructure (EF Core + SQLite en archivo
/// temporal) + IReceiptPrinter. Valida la cadena completa de la arquitectura:
/// caso de uso → reglas → SQLite → Result&lt;T&gt; → recibo.
/// </summary>
public class SaleServiceTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"pos-test-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _services;
    private readonly SaleService _saleService;
    private readonly PosDbContext _db;
    private readonly IReceiptPrinter _printer;

    public SaleServiceTests()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddInfrastructure($"Data Source={_dbPath};Pooling=False");
        _services = services.BuildServiceProvider();

        _db = _services.GetRequiredService<PosDbContext>();
        _db.Database.EnsureCreated();

        _saleService = _services.GetRequiredService<SaleService>();
        _printer = _services.GetRequiredService<IReceiptPrinter>();
    }

    public void Dispose()
    {
        _services.Dispose();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private async Task<Product> SeedProductAsync(string name = "Café con leche", decimal price = 100m, decimal stock = 10m)
    {
        var product = new Product
        {
            Name = name,
            Price = new Money(price),
            Cost = new Money(30m),
            Stock = stock,
            IsActive = true
        };
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return product;
    }

    [Fact]
    public async Task CrearVenta_ProductoValido_CalculaTotalesYDescuentaStock()
    {
        var product = await SeedProductAsync();
        var request = new CreateSaleRequest
        {
            UserId = 1,
            Items = [new SaleItemRequest { ProductId = product.Id, Quantity = 2 }],
            Payments = [new PaymentRequest { Method = PaymentMethod.Cash, Amount = 200m }]
        };

        var result = await _saleService.CreateSaleAsync(request);

        Assert.True(result.IsSuccess);
        var sale = result.Value!;
        Assert.Equal(1, sale.Number);
        Assert.Equal(200m, sale.Total.Amount);
        Assert.Equal(30.51m, sale.Itbis.Amount);     // 200 × 18/118
        Assert.Equal(169.49m, sale.Subtotal.Amount); // 200 − 30.51
        Assert.Empty(sale.Warnings);

        var reloaded = await _db.Products.FindAsync(product.Id);
        Assert.Equal(8m, reloaded!.Stock);
    }

    [Fact]
    public async Task CrearVenta_StockNegativo_PermiteYAdvierte()
    {
        var product = await SeedProductAsync(stock: 1m);
        var request = new CreateSaleRequest
        {
            UserId = 1,
            Items = [new SaleItemRequest { ProductId = product.Id, Quantity = 5 }],
            Payments = [new PaymentRequest { Method = PaymentMethod.Cash, Amount = 500m }]
        };

        var result = await _saleService.CreateSaleAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Value!.Warnings, w => w.Contains("stock negativo"));
        Assert.Equal(-4m, (await _db.Products.FindAsync(product.Id))!.Stock);
    }

    [Fact]
    public async Task CrearVenta_ProductoInexistente_Falla()
    {
        var request = new CreateSaleRequest
        {
            UserId = 1,
            Items = [new SaleItemRequest { ProductId = 999, Quantity = 1 }],
            Payments = [new PaymentRequest { Method = PaymentMethod.Cash, Amount = 100m }]
        };

        var result = await _saleService.CreateSaleAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("PRODUCT_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task CrearVenta_PagoInsuficiente_Falla()
    {
        var product = await SeedProductAsync();
        var request = new CreateSaleRequest
        {
            UserId = 1,
            Items = [new SaleItemRequest { ProductId = product.Id, Quantity = 1 }],
            Payments = [new PaymentRequest { Method = PaymentMethod.Cash, Amount = 50m }]
        };

        var result = await _saleService.CreateSaleAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("PAYMENT_INSUFFICIENT", result.ErrorCode);
    }

    [Fact]
    public async Task CrearVenta_PagoMixto_Ok()
    {
        var product = await SeedProductAsync();
        var request = new CreateSaleRequest
        {
            UserId = 1,
            Items = [new SaleItemRequest { ProductId = product.Id, Quantity = 2 }],
            Payments =
            [
                new PaymentRequest { Method = PaymentMethod.Cash, Amount = 100m },
                new PaymentRequest { Method = PaymentMethod.Transfer, Amount = 100m }
            ]
        };

        var result = await _saleService.CreateSaleAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Payments.Count);
    }

    [Fact]
    public async Task CrearVenta_DescuentoGlobal_ReduceBaseImponible()
    {
        var product = await SeedProductAsync();
        var request = new CreateSaleRequest
        {
            UserId = 1,
            Items = [new SaleItemRequest { ProductId = product.Id, Quantity = 2 }], // 200
            GlobalDiscount = 50m,
            Payments = [new PaymentRequest { Method = PaymentMethod.Cash, Amount = 150m }]
        };

        var result = await _saleService.CreateSaleAsync(request);

        Assert.True(result.IsSuccess);
        var sale = result.Value!;
        Assert.Equal(150m, sale.Total.Amount);
        Assert.Equal(22.88m, sale.Itbis.Amount);     // 150 × 18/118
        Assert.Equal(127.12m, sale.Subtotal.Amount);
        Assert.Equal(50m, sale.Discount.Amount);
    }

    /// <summary>Demo visible: vende 2 productos y "imprime" el recibo (consola).</summary>
    [Fact]
    public async Task Demo_VentaCompleta_ImprimeRecibo()
    {
        var cafe = await SeedProductAsync("Café con leche", price: 100m, stock: 10m);
        var pan = await SeedProductAsync("Pan de agua", price: 25m, stock: 50m);

        var request = new CreateSaleRequest
        {
            UserId = 1,
            Items =
            [
                new SaleItemRequest { ProductId = cafe.Id, Quantity = 2 },
                new SaleItemRequest { ProductId = pan.Id, Quantity = 4 }
            ],
            Payments = [new PaymentRequest { Method = PaymentMethod.Cash, Amount = 300m }]
        };

        var result = await _saleService.CreateSaleAsync(request);

        Assert.True(result.IsSuccess);
        await _printer.PrintReceiptAsync(result.Value!);
    }
}
