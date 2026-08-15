using Microsoft.Extensions.DependencyInjection;
using POS.Application;
using POS.Application.Abstractions;
using POS.Application.Auth;
using POS.Application.Cash;
using POS.Application.Sales;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.ValueObjects;
using POS.Infrastructure;
using POS.Infrastructure.Data;
using POS.Infrastructure.Services;

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
    private readonly long _adminUserId;
    private readonly long _cashSessionId;

    public SaleServiceTests()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddInfrastructure($"Data Source={_dbPath};Pooling=False");
        // Los tests no tocan impresoras del sistema: usan el printer de consola
        // (el DI de la app resuelve la térmica real por defecto).
        services.AddScoped<IReceiptPrinter, ConsoleReceiptPrinter>();
        _services = services.BuildServiceProvider();

        _db = _services.GetRequiredService<PosDbContext>();
        _db.Database.EnsureCreated();

        _saleService = _services.GetRequiredService<SaleService>();
        _printer = _services.GetRequiredService<IReceiptPrinter>();

        // P2.1/P2.2: la venta exige usuario con sesión y caja ABIERTA. Se siembra un
        // Admin (sin tope de descuento) con su caja abierta, como haría el arranque.
        var users = _services.GetRequiredService<IUserRepository>();
        var hasher = _services.GetRequiredService<IPasswordHasher>();
        var user = new User
        {
            Username = "admin",
            DisplayName = "Admin",
            PasswordHash = hasher.Hash("admin123"),
            Role = UserRole.Admin,
            IsActive = true
        };
        _adminUserId = users.AddAsync(user).GetAwaiter().GetResult();

        var cash = _services.GetRequiredService<CashSessionService>();
        var open = cash.OpenAsync(new OpenCashRequest(_adminUserId, InitialCash: 0m)).GetAwaiter().GetResult();
        _cashSessionId = open.Value!.Id;
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
            UserId = _adminUserId,
            CashSessionId = _cashSessionId,
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
            UserId = _adminUserId,
            CashSessionId = _cashSessionId,
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
            UserId = _adminUserId,
            CashSessionId = _cashSessionId,
            Items = [new SaleItemRequest { ProductId = 999, Quantity = 1 }],
            Payments = [new PaymentRequest { Method = PaymentMethod.Cash, Amount = 100m }]
        };

        var result = await _saleService.CreateSaleAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("PRODUCT_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task CrearVenta_SinCajaAbierta_FallaCashClosed()
    {
        var product = await SeedProductAsync();
        var request = new CreateSaleRequest
        {
            UserId = _adminUserId,
            CashSessionId = null, // sin caja abierta → la venta se bloquea (P2.2b)
            Items = [new SaleItemRequest { ProductId = product.Id, Quantity = 1 }],
            Payments = [new PaymentRequest { Method = PaymentMethod.Cash, Amount = 100m }]
        };

        var result = await _saleService.CreateSaleAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("CASH_CLOSED", result.ErrorCode);
    }

    [Fact]
    public async Task CrearVenta_CajaDeOtroUsuario_FallaCashNotOwned()
    {
        var product = await SeedProductAsync();
        var users = _services.GetRequiredService<IUserRepository>();
        var hasher = _services.GetRequiredService<IPasswordHasher>();
        var otherUser = new User
        {
            Username = "otro",
            DisplayName = "Otro",
            PasswordHash = hasher.Hash("otro123"),
            Role = UserRole.Cajero,
            IsActive = true
        };
        var otherId = await users.AddAsync(otherUser);
        var cash = _services.GetRequiredService<CashSessionService>();
        var open = await cash.OpenAsync(new OpenCashRequest(otherId, 0m));
        var otherCashId = open.Value!.Id;

        // El Admin intenta usar la caja del otro usuario → rechazado
        var request = new CreateSaleRequest
        {
            UserId = _adminUserId,
            CashSessionId = otherCashId,
            Items = [new SaleItemRequest { ProductId = product.Id, Quantity = 1 }],
            Payments = [new PaymentRequest { Method = PaymentMethod.Cash, Amount = 100m }]
        };

        var result = await _saleService.CreateSaleAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("CASH_NOT_OWNED", result.ErrorCode);
    }

    [Fact]
    public async Task CrearVenta_PagoInsuficiente_Falla()
    {
        var product = await SeedProductAsync();
        var request = new CreateSaleRequest
        {
            UserId = _adminUserId,
            CashSessionId = _cashSessionId,
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
            UserId = _adminUserId,
            CashSessionId = _cashSessionId,
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
            UserId = _adminUserId,
            CashSessionId = _cashSessionId,
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

    /// <summary>
    /// PARIDAD: lo que el ticket del cajero calcula (CartCalculator) es EXACTAMENTE
    /// lo que se persiste (SaleService). Ambos usan la misma fuente de verdad — si
    /// algún día divergen, este test lo detecta.
    /// </summary>
    [Fact]
    public async Task CrearVenta_TotalesCoincidenConPreviewDelCart()
    {
        var cafe = await SeedProductAsync("Café", price: 100m);
        var pan = await SeedProductAsync("Pan", price: 25m);

        // Carrito con descuentos de línea y global (mismos que mandaría el ViewModel).
        var line1Gross = CartCalculator.LineGross(100m, 2);
        var line1Discount = CartCalculator.LineDiscountByPercent(line1Gross, 10); // -20
        var line2Gross = CartCalculator.LineGross(25m, 4);
        var line2Discount = CartCalculator.LineDiscountByAmount(line2Gross, 10);  // -10
        var subtotalNet = CartCalculator.LineTotal(100m, 2, line1Discount)
                        + CartCalculator.LineTotal(25m, 4, line2Discount);        // 180 + 90 = 270
        var expected = CartCalculator.ComputeTotals(subtotalNet, globalDiscount: 20m); // 250

        var request = new CreateSaleRequest
        {
            UserId = _adminUserId,
            CashSessionId = _cashSessionId,
            Items =
            [
                new SaleItemRequest { ProductId = cafe.Id, Quantity = 2, UnitPrice = 100m, LineDiscount = line1Discount },
                new SaleItemRequest { ProductId = pan.Id, Quantity = 4, UnitPrice = 25m, LineDiscount = line2Discount }
            ],
            GlobalDiscount = 20m,
            Payments = [new PaymentRequest { Method = PaymentMethod.Cash, Amount = expected.Total }]
        };

        var result = await _saleService.CreateSaleAsync(request);

        Assert.True(result.IsSuccess);
        var sale = result.Value!;
        Assert.Equal(expected.Total, sale.Total.Amount);
        Assert.Equal(expected.Itbis, sale.Itbis.Amount);
        Assert.Equal(expected.BaseImponible, sale.Subtotal.Amount);
        Assert.Equal(20m, sale.Discount.Amount);
    }

    [Fact]
    public async Task CrearVenta_NumeracionConsecutiva_Y_VentaFallidaNoQuemaNumero()
    {
        var product = await SeedProductAsync();

        // 1ª venta válida → número 1
        var ok1 = await _saleService.CreateSaleAsync(new CreateSaleRequest
        {
            UserId = _adminUserId,
            CashSessionId = _cashSessionId,
            Items = [new SaleItemRequest { ProductId = product.Id, Quantity = 1 }],
            Payments = [new PaymentRequest { Method = PaymentMethod.Cash, Amount = 100m }]
        });
        Assert.True(ok1.IsSuccess);
        Assert.Equal(1, ok1.Value!.Number);

        // Venta inválida (descuento global supera total) → falla ANTES de persistir
        var bad = await _saleService.CreateSaleAsync(new CreateSaleRequest
        {
            UserId = _adminUserId,
            CashSessionId = _cashSessionId,
            Items = [new SaleItemRequest { ProductId = product.Id, Quantity = 1 }],
            GlobalDiscount = 500m,
            Payments = [new PaymentRequest { Method = PaymentMethod.Cash, Amount = 100m }]
        });
        Assert.True(bad.IsFailure);

        // Siguiente venta válida → número 2 (consecutivo, sin hueco)
        var ok2 = await _saleService.CreateSaleAsync(new CreateSaleRequest
        {
            UserId = _adminUserId,
            CashSessionId = _cashSessionId,
            Items = [new SaleItemRequest { ProductId = product.Id, Quantity = 1 }],
            Payments = [new PaymentRequest { Method = PaymentMethod.Cash, Amount = 100m }]
        });
        Assert.True(ok2.IsSuccess);
        Assert.Equal(2, ok2.Value!.Number);
    }

    /// <summary>Demo visible: vende 2 productos y "imprime" el recibo (consola).</summary>
    [Fact]
    public async Task Demo_VentaCompleta_ImprimeRecibo()
    {
        var cafe = await SeedProductAsync("Café con leche", price: 100m, stock: 10m);
        var pan = await SeedProductAsync("Pan de agua", price: 25m, stock: 50m);

        var request = new CreateSaleRequest
        {
            UserId = _adminUserId,
            CashSessionId = _cashSessionId,
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
