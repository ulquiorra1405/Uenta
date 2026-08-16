using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.Application;
using POS.Application.Abstractions;
using POS.Application.Purchases;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.ValueObjects;
using POS.Infrastructure;
using POS.Infrastructure.Data;

namespace POS.Tests;

/// <summary>
/// Fase 2 (P5.2): compras y proveedores — costo promedio ponderado, reposición
/// de stock con movimiento tipo Compra, permisos por rol y CRUD de proveedores.
/// </summary>
public class PurchaseServiceTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"pos-purchase-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _services;
    private readonly PosDbContext _db;
    private readonly PurchaseService _purchases;
    private readonly IUserRepository _users;
    private readonly IProductRepository _products;
    private readonly IPasswordHasher _hasher;
    private long _adminId;
    private long _cajeroId;

    public PurchaseServiceTests()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddInfrastructure($"Data Source={_dbPath};Pooling=False");
        _services = services.BuildServiceProvider();

        _db = _services.GetRequiredService<PosDbContext>();
        _db.Database.EnsureCreated();

        _purchases = _services.GetRequiredService<PurchaseService>();
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

    private async Task<Product> AddProductAsync(string name = "Café", decimal cost = 30m, decimal stock = 10)
    {
        var product = new Product { Name = name, Price = new Money(100m), Cost = new Money(cost), Stock = stock, MinStock = 5, IsActive = true };
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return product;
    }

    private async Task<long> AddSupplierAsync(string name = "Distribuidora DR", string rnc = "131234567")
    {
        var result = await _purchases.CreateSupplierAsync(new CreateSupplierRequest(name, rnc, "809-555-0100"));
        Assert.True(result.IsSuccess);
        return result.Value!.Id;
    }

    private static CreatePurchaseRequest PurchaseRequest(long userId, long? supplierId,
        params (Product Product, decimal Qty, decimal Cost)[] lines) => new()
    {
        UserId = userId,
        SupplierId = supplierId,
        Items = lines.Select(l => new CreatePurchaseLineRequest(l.Product.Id, l.Qty, l.Cost)).ToList()
    };

    [Fact]
    public async Task Create_ReponeStockYRegistraMovimientoCompra()
    {
        var product = await AddProductAsync(stock: 10);

        var result = await _purchases.CreateAsync(PurchaseRequest(_adminId, null, (product, 5, 40m)));

        Assert.True(result.IsSuccess);
        var reloaded = await _products.GetByIdAsync(product.Id);
        Assert.Equal(15m, reloaded!.Stock);

        var movements = await _db.StockMovements.Where(m => m.ProductId == product.Id).ToListAsync();
        Assert.Contains(movements, m => m.Type == StockMovementType.Entry && m.Quantity == 5 && m.Reason == "Compra");
    }

    [Fact]
    public async Task Create_CalculaCostoPromedioPonderado()
    {
        // Stock 10 a costo 30 → compra 10 a 50 → costo = (10×30 + 10×50)/20 = 40.
        var product = await AddProductAsync(cost: 30m, stock: 10);

        var result = await _purchases.CreateAsync(PurchaseRequest(_adminId, null, (product, 10, 50m)));

        Assert.True(result.IsSuccess);
        var reloaded = await _products.GetByIdAsync(product.Id);
        Assert.Equal(40m, reloaded!.Cost.Amount);
    }

    [Fact]
    public async Task Create_SinStockPrev_ElCostoEsElDeLaCompra()
    {
        // Stock 0 → compra 10 a 60 → costo = 60 (sin previo que ponderar).
        var product = await AddProductAsync(cost: 30m, stock: 0);

        var result = await _purchases.CreateAsync(PurchaseRequest(_adminId, null, (product, 10, 60m)));

        Assert.True(result.IsSuccess);
        var reloaded = await _products.GetByIdAsync(product.Id);
        Assert.Equal(60m, reloaded!.Cost.Amount);
        Assert.Equal(10m, reloaded!.Stock);
    }

    [Fact]
    public async Task Create_Cajero_FallaPorPermiso()
    {
        var product = await AddProductAsync();

        var result = await _purchases.CreateAsync(PurchaseRequest(_cajeroId, null, (product, 1, 40m)));

        Assert.False(result.IsSuccess);
        Assert.Equal("PURCHASE_PERMISSION_DENIED", result.ErrorCode);
    }

    [Fact]
    public async Task Create_ProveedorInexistente_Falla()
    {
        var product = await AddProductAsync();

        var result = await _purchases.CreateAsync(PurchaseRequest(_adminId, 999, (product, 1, 40m)));

        Assert.False(result.IsSuccess);
        Assert.Equal("SUPPLIER_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task Create_ConProveedor_RegistraNombreEnDto()
    {
        var product = await AddProductAsync();
        var supplierId = await AddSupplierAsync();

        var result = await _purchases.CreateAsync(PurchaseRequest(_adminId, supplierId, (product, 2, 40m)));

        Assert.True(result.IsSuccess);
        Assert.Equal("Distribuidora DR", result.Value!.SupplierName);
        Assert.Single(result.Value!.Items);
        Assert.Equal(2m, result.Value!.Items[0].Quantity);
    }

    [Fact]
    public async Task Create_LineaVacia_Falla()
    {
        var result = await _purchases.CreateAsync(new CreatePurchaseRequest { UserId = _adminId });

        Assert.False(result.IsSuccess);
        Assert.Equal("PURCHASE_EMPTY", result.ErrorCode);
    }

    [Fact]
    public async Task Create_RegistraAuditoria()
    {
        var product = await AddProductAsync();

        await _purchases.CreateAsync(PurchaseRequest(_adminId, null, (product, 1, 40m)));

        var log = await _db.AuditLogs.FirstOrDefaultAsync(a => a.Action == AuditAction.PurchaseCreated);
        Assert.NotNull(log);
        Assert.Contains("Compra", log!.Detail);
    }

    [Fact]
    public async Task GetRecent_DevuelveComprasRecientes()
    {
        var product = await AddProductAsync();
        await _purchases.CreateAsync(PurchaseRequest(_adminId, null, (product, 3, 40m)));

        var recent = await _purchases.GetRecentAsync();

        var purchase = Assert.Single(recent);
        Assert.Equal("Admin", purchase.UserName);
        Assert.Equal(120m, purchase.Total.Amount); // 3 × 40
        Assert.True(purchase.Number > 0);
    }

    // ─────────────────────────── Proveedores ───────────────────────────

    [Fact]
    public async Task Supplier_CrearYListar()
    {
        var id = await AddSupplierAsync();

        var all = await _purchases.GetSuppliersAsync();
        var supplier = Assert.Single(all);
        Assert.Equal(id, supplier.Id);
        Assert.Equal("Distribuidora DR", supplier.Name);
        Assert.Equal("131234567", supplier.Rnc);
    }

    [Fact]
    public async Task Supplier_RncDuplicado_Falla()
    {
        await AddSupplierAsync("Proveedor A", "131234567");
        await AddSupplierAsync("Proveedor B", "998877665");

        var result = await _purchases.CreateSupplierAsync(new CreateSupplierRequest("Proveedor C", "131234567", ""));

        Assert.False(result.IsSuccess);
        Assert.Equal("SUPPLIER_RNC_DUPLICATED", result.ErrorCode);
    }

    [Fact]
    public async Task Supplier_NombreVacio_Falla()
    {
        var result = await _purchases.CreateSupplierAsync(new CreateSupplierRequest("   ", "", ""));

        Assert.False(result.IsSuccess);
        Assert.Equal("SUPPLIER_NAME_REQUIRED", result.ErrorCode);
    }

    [Fact]
    public async Task Supplier_Actualizar()
    {
        var id = await AddSupplierAsync("Viejo nombre");

        var result = await _purchases.UpdateSupplierAsync(new UpdateSupplierRequest(id, "Nuevo nombre", "", ""));

        Assert.True(result.IsSuccess);
        var all = await _purchases.GetSuppliersAsync();
        Assert.Equal("Nuevo nombre", Assert.Single(all).Name);
    }
}