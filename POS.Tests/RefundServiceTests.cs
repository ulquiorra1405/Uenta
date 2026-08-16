using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.Application;
using POS.Application.Abstractions;
using POS.Application.Cash;
using POS.Application.Refunds;
using POS.Application.Sales;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.ValueObjects;
using POS.Infrastructure;
using POS.Infrastructure.Data;

namespace POS.Tests;

/// <summary>
/// Fase 2 (P5.1): devoluciones / notas de crédito — flujo con y sin recibo,
/// permisos por rol, candado de caja, restauración de stock y límite de
/// devolución por venta original.
/// </summary>
public class RefundServiceTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"pos-refund-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _services;
    private readonly PosDbContext _db;
    private readonly RefundService _refunds;
    private readonly SaleService _saleService;
    private readonly CashSessionService _cash;
    private readonly IUserRepository _users;
    private readonly IProductRepository _products;
    private readonly IPasswordHasher _hasher;
    private long _adminId;
    private long _cajeroId;

    public RefundServiceTests()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddInfrastructure($"Data Source={_dbPath};Pooling=False");
        _services = services.BuildServiceProvider();

        _db = _services.GetRequiredService<PosDbContext>();
        _db.Database.EnsureCreated();

        _refunds = _services.GetRequiredService<RefundService>();
        _saleService = _services.GetRequiredService<SaleService>();
        _cash = _services.GetRequiredService<CashSessionService>();
        _users = _services.GetRequiredService<IUserRepository>();
        _products = _services.GetRequiredService<IProductRepository>();
        _hasher = _services.GetRequiredService<IPasswordHasher>();

        _adminId = _users.AddAsync(new User
        {
            Username = "admin",
            DisplayName = "Admin",
            PasswordHash = _hasher.Hash("admin123"),
            Role = UserRole.Admin,
            IsActive = true
        }).GetAwaiter().GetResult();

        _cajeroId = _users.AddAsync(new User
        {
            Username = "cajero",
            DisplayName = "Cajero",
            PasswordHash = _hasher.Hash("cajero123"),
            Role = UserRole.Cajero,
            IsActive = true
        }).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _services.Dispose();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private async Task<long> OpenCashAsync(long userId, decimal initial = 0)
    {
        var result = await _cash.OpenAsync(new OpenCashRequest(userId, initial));
        Assert.True(result.IsSuccess);
        return result.Value!.Id;
    }

    private async Task<Product> AddProductAsync(string name = "Café", decimal price = 100m, decimal stock = 50)
    {
        var product = new Product { Name = name, Price = new Money(price), Cost = new Money(30m), Stock = stock, MinStock = 5, IsActive = true };
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return product;
    }

    /// <summary>Crea una venta de prueba del cajero y devuelve su Id.</summary>
    private async Task<long> SellAsync(long cashId, Product product, decimal qty = 1)
    {
        var result = await _saleService.CreateSaleAsync(new CreateSaleRequest
        {
            UserId = _cajeroId,
            CashSessionId = cashId,
            Items = [new SaleItemRequest { ProductId = product.Id, Quantity = qty }],
            Payments = [new PaymentRequest { Method = PaymentMethod.Cash, Amount = product.Price.Amount * qty }]
        });
        Assert.True(result.IsSuccess);
        return result.Value!.Id;
    }

    private static CreateRefundRequest RefundRequest(long userId, long cashId, long? saleId, string reason,
        Product product, decimal qty, PaymentMethod method = PaymentMethod.Cash) => new()
    {
        UserId = userId,
        CashSessionId = cashId,
        OriginalSaleId = saleId,
        Reason = reason,
        Items = [new RefundItemRequest(product.Id, qty, product.Price.Amount)],
        Payments = [new RefundPaymentRequest(method, product.Price.Amount * qty)]
    };

    [Fact]
    public async Task Create_ConRecibo_RestauraStockYRegistraMovimiento()
    {
        var cashId = await OpenCashAsync(_cajeroId);
        var product = await AddProductAsync(stock: 50);
        var saleId = await SellAsync(cashId, product, 2);
        Assert.Equal(48m, product.Stock); // 50 − 2 vendidos

        var result = await _refunds.CreateAsync(RefundRequest(_cajeroId, cashId, saleId, "", product, 2));

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Items); // 1 línea con qty 2
        var reloaded = await _products.GetByIdAsync(product.Id);
        Assert.Equal(50m, reloaded!.Stock); // stock restaurado

        var movements = await _db.StockMovements.Where(m => m.ProductId == product.Id).ToListAsync();
        Assert.Contains(movements, m => m.Type == StockMovementType.Entry && m.Quantity == 2);
    }

    [Fact]
    public async Task Create_SinRecibo_CajeroFallaporPermiso()
    {
        var cashId = await OpenCashAsync(_cajeroId);
        var product = await AddProductAsync();

        var result = await _refunds.CreateAsync(RefundRequest(_cajeroId, cashId, null, "cliente sin ticket", product, 1));

        Assert.False(result.IsSuccess);
        Assert.Equal("REFUND_NO_RECEIPT_PERMISSION", result.ErrorCode);
    }

    [Fact]
    public async Task Create_SinRecibo_AdminSinMotivo_Falla()
    {
        var cashId = await OpenCashAsync(_adminId);
        var product = await AddProductAsync();

        var result = await _refunds.CreateAsync(RefundRequest(_adminId, cashId, null, "  ", product, 1));

        Assert.False(result.IsSuccess);
        Assert.Equal("REFUND_REASON_REQUIRED", result.ErrorCode);
    }

    [Fact]
    public async Task Create_SinRecibo_AdminConMotivo_Exito()
    {
        var cashId = await OpenCashAsync(_adminId, initial: 500);
        var product = await AddProductAsync(stock: 10);

        var result = await _refunds.CreateAsync(RefundRequest(_adminId, cashId, null, "cliente sin ticket", product, 1));

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.OriginalSaleId);
        Assert.Equal("cliente sin ticket", result.Value!.Reason);
    }

    [Fact]
    public async Task Create_ExcedeLoVendido_Falla()
    {
        var cashId = await OpenCashAsync(_cajeroId);
        var product = await AddProductAsync();
        var saleId = await SellAsync(cashId, product, 1); // solo vendió 1

        var result = await _refunds.CreateAsync(RefundRequest(_cajeroId, cashId, saleId, "", product, 2));

        Assert.False(result.IsSuccess);
        Assert.Equal("REFUND_EXCEEDS_SALE", result.ErrorCode);
    }

    [Fact]
    public async Task Create_ReembolsoEfectivoMayorQueCaja_Falla()
    {
        var cashId = await OpenCashAsync(_cajeroId, initial: 0); // caja sin efectivo
        var product = await AddProductAsync(price: 100m);
        // Ventas en efectivo anteriores dan efectivo a la caja; aquí no hay ventas, solo 0 inicial.

        var result = await _refunds.CreateAsync(RefundRequest(_cajeroId, cashId, null, "x", product, 1));

        // Sin recibo → cajero no puede; el permiso se valida antes. Con admin:
        var cashAdmin = await OpenCashAsync(_adminId, initial: 0);
        var resultAdmin = await _refunds.CreateAsync(RefundRequest(_adminId, cashAdmin, null, "x", product, 1));
        Assert.False(resultAdmin.IsSuccess);
        Assert.Equal("CASH_INSUFFICIENT", resultAdmin.ErrorCode);
    }

    [Fact]
    public async Task Create_ReembolsoEfectivoDisponible_Exito()
    {
        var cashId = await OpenCashAsync(_cajeroId, initial: 500); // efectivo disponible
        var product = await AddProductAsync();
        var saleId = await SellAsync(cashId, product, 1); // +100 efectivo

        var result = await _refunds.CreateAsync(RefundRequest(_cajeroId, cashId, saleId, "", product, 1));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Create_ReembolsoMontoNoCoincide_Falla()
    {
        var cashId = await OpenCashAsync(_cajeroId, initial: 500);
        var product = await AddProductAsync();
        var saleId = await SellAsync(cashId, product, 1);

        var result = await _refunds.CreateAsync(new CreateRefundRequest
        {
            UserId = _cajeroId,
            CashSessionId = cashId,
            OriginalSaleId = saleId,
            Items = [new RefundItemRequest(product.Id, 1, product.Price.Amount)],
            Payments = [new RefundPaymentRequest(PaymentMethod.Cash, 50m)] // total 100 ≠ 50
        });

        Assert.False(result.IsSuccess);
        Assert.Equal("PAYMENT_MISMATCH", result.ErrorCode);
    }

    [Fact]
    public async Task Create_RegistraAuditoria()
    {
        var cashId = await OpenCashAsync(_cajeroId, initial: 500);
        var product = await AddProductAsync();
        var saleId = await SellAsync(cashId, product, 1);

        await _refunds.CreateAsync(RefundRequest(_cajeroId, cashId, saleId, "", product, 1));

        var log = await _db.AuditLogs.FirstOrDefaultAsync(a => a.Action == AuditAction.RefundCreated);
        Assert.NotNull(log);
        Assert.Contains("recibo", log!.Detail);
    }

    [Fact]
    public async Task GetRecent_DevuelveNotasRecientes()
    {
        var cashId = await OpenCashAsync(_adminId, initial: 500);
        var product = await AddProductAsync();
        await _refunds.CreateAsync(RefundRequest(_adminId, cashId, null, "devolución A", product, 1));

        var recent = await _refunds.GetRecentAsync();

        var refund = Assert.Single(recent);
        Assert.Equal("devolución A", refund.Reason);
        Assert.Equal("Admin", refund.UserName);
        Assert.True(refund.Number > 0);
    }
}