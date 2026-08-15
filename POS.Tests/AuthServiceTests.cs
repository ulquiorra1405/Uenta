using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.Application;
using POS.Application.Abstractions;
using POS.Application.Auth;
using POS.Application.Cash;
using POS.Application.Sales;
using POS.Application.Settings;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.ValueObjects;
using POS.Infrastructure;
using POS.Infrastructure.Data;
using POS.Infrastructure.Services;

namespace POS.Tests;

/// <summary>
/// Fase 1B (P2.1): login local, permisos y auditoría, con SQLite real.
/// </summary>
public class AuthServiceTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"pos-auth-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _services;
    private readonly PosDbContext _db;
    private readonly AuthService _auth;
    private readonly IPasswordHasher _hasher;
    private readonly IUserRepository _users;

    public AuthServiceTests()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddInfrastructure($"Data Source={_dbPath};Pooling=False");
        _services = services.BuildServiceProvider();

        _db = _services.GetRequiredService<PosDbContext>();
        _db.Database.EnsureCreated();

        _auth = _services.GetRequiredService<AuthService>();
        _hasher = _services.GetRequiredService<IPasswordHasher>();
        _users = _services.GetRequiredService<IUserRepository>();
    }

    public void Dispose()
    {
        _services.Dispose();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private async Task<User> SeedUserAsync(string username = "admin", string password = "admin123", UserRole role = UserRole.Admin, bool active = true)
    {
        var user = new User
        {
            Username = username,
            DisplayName = username,
            PasswordHash = _hasher.Hash(password),
            Role = role,
            IsActive = active
        };
        await _users.AddAsync(user);
        return user;
    }

    [Fact]
    public async Task PasswordHasher_Circle_RoundTrip()
    {
        var hash = _hasher.Hash("secreto");
        Assert.NotEqual("secreto", hash);
        Assert.True(_hasher.Verify("secreto", hash));
        Assert.False(_hasher.Verify("otra", hash));
    }

    [Fact]
    public async Task Login_CredencialesCorrectas_Ok()
    {
        await SeedUserAsync();

        var result = await _auth.ValidateAsync("admin", "admin123");

        Assert.True(result.IsSuccess);
        Assert.Equal("admin", result.User!.Username);
    }

    [Fact]
    public async Task Login_UsernameCaseInsensitive_Ok()
    {
        await SeedUserAsync();

        var result = await _auth.ValidateAsync("ADMIN", "admin123");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Login_PasswordIncorrecta_Falla()
    {
        await SeedUserAsync();

        var result = await _auth.ValidateAsync("admin", "incorrecta");

        Assert.True(result.IsFailure);
        Assert.Equal("INVALID_CREDENTIALS", result.ErrorCode);
    }

    [Fact]
    public async Task Login_UsuarioInexistente_Falla()
    {
        var result = await _auth.ValidateAsync("nadie", "x123");

        Assert.True(result.IsFailure);
        Assert.Equal("INVALID_CREDENTIALS", result.ErrorCode);
    }

    [Fact]
    public async Task Login_UsuarioInactivo_Falla()
    {
        await SeedUserAsync(active: false);

        var result = await _auth.ValidateAsync("admin", "admin123");

        Assert.True(result.IsFailure);
        Assert.Equal("USER_INACTIVE", result.ErrorCode);
    }

    [Fact]
    public async Task Auditoria_LoginOkYFallo_QuedanRegistrados()
    {
        await SeedUserAsync();
        await _auth.ValidateAsync("admin", "admin123");     // OK
        await _auth.ValidateAsync("admin", "mala");          // fallo

        var all = await _db.AuditLogs.AsNoTracking().ToListAsync();
        var entries = all.OrderBy(a => a.CreatedAt).ToList(); // SQLite: ordenar en memoria
        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.Action == AuditAction.Login);
        Assert.Contains(entries, e => e.Action == AuditAction.LoginFailed);
        Assert.All(entries, e => Assert.True(e.CreatedAt != default));
    }
}

/// <summary>
/// Fase 1B (P2.1d): tope de descuento por rol aplicado en la venta.
/// </summary>
public class DiscountLimitTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"pos-disc-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _services;
    private readonly SaleService _saleService;
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly CashSessionService _cash;
    private readonly SettingsService _settings;
    private readonly PosDbContext _db;

    public DiscountLimitTests()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddInfrastructure($"Data Source={_dbPath};Pooling=False");
        _services = services.BuildServiceProvider();

        _db = _services.GetRequiredService<PosDbContext>();
        _db.Database.EnsureCreated();

        _saleService = _services.GetRequiredService<SaleService>();
        _users = _services.GetRequiredService<IUserRepository>();
        _hasher = _services.GetRequiredService<IPasswordHasher>();
        _cash = _services.GetRequiredService<CashSessionService>();
        _settings = _services.GetRequiredService<SettingsService>();
    }

    public void Dispose()
    {
        _services.Dispose();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private async Task<(long userId, long cashId)> OpenSessionAsync(UserRole role)
    {
        var user = new User
        {
            Username = Guid.NewGuid().ToString("N")[..10],
            DisplayName = "U",
            PasswordHash = _hasher.Hash("123456"),
            Role = role,
            IsActive = true
        };
        var userId = await _users.AddAsync(user);
        var open = await _cash.OpenAsync(new OpenCashRequest(userId, 0m));
        return (userId, open.Value!.Id);
    }

    private async Task<long> SeedProductAsync(decimal price = 100m)
    {
        var product = new Product { Name = "P", Price = new Money(price), Cost = new Money(30m), Stock = 10, IsActive = true };
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return product.Id;
    }

    [Fact]
    public async Task Cajero_DescuentoSuperaTope_Falla()
    {
        var (userId, cashId) = await OpenSessionAsync(UserRole.Cajero); // tope 10%
        var productId = await SeedProductAsync(price: 100m);

        var request = new CreateSaleRequest
        {
            UserId = userId,
            CashSessionId = cashId,
            Items = [new SaleItemRequest { ProductId = productId, Quantity = 1 }], // 100
            GlobalDiscount = 15m, // 15% > 10%
            Payments = [new PaymentRequest { Method = PaymentMethod.Cash, Amount = 85m }]
        };

        var result = await _saleService.CreateSaleAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("DISCOUNT_LIMIT_EXCEEDED", result.ErrorCode);
    }

    [Fact]
    public async Task Cajero_DescuentoDentroDelTope_Ok()
    {
        var (userId, cashId) = await OpenSessionAsync(UserRole.Cajero);
        var productId = await SeedProductAsync(price: 100m);

        var request = new CreateSaleRequest
        {
            UserId = userId,
            CashSessionId = cashId,
            Items = [new SaleItemRequest { ProductId = productId, Quantity = 1 }],
            GlobalDiscount = 10m, // 10% = tope
            Payments = [new PaymentRequest { Method = PaymentMethod.Cash, Amount = 90m }]
        };

        var result = await _saleService.CreateSaleAsync(request);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Supervisor_Descuento25_FallaY_10_Ok()
    {
        var (userId, cashId) = await OpenSessionAsync(UserRole.Supervisor); // tope 25%
        var productId = await SeedProductAsync(price: 100m);

        var over = await _saleService.CreateSaleAsync(new CreateSaleRequest
        {
            UserId = userId,
            CashSessionId = cashId,
            Items = [new SaleItemRequest { ProductId = productId, Quantity = 4 }], // 400
            GlobalDiscount = 110m, // 27.5% > 25%
            Payments = [new PaymentRequest { Method = PaymentMethod.Cash, Amount = 290m }]
        });
        Assert.True(over.IsFailure);
        Assert.Equal("DISCOUNT_LIMIT_EXCEEDED", over.ErrorCode);

        var ok = await _saleService.CreateSaleAsync(new CreateSaleRequest
        {
            UserId = userId,
            CashSessionId = cashId,
            Items = [new SaleItemRequest { ProductId = productId, Quantity = 4 }],
            GlobalDiscount = 100m, // 25% = tope
            Payments = [new PaymentRequest { Method = PaymentMethod.Cash, Amount = 300m }]
        });
        Assert.True(ok.IsSuccess);
    }

    [Fact]
    public async Task Admin_DescuentoSinTope_Ok()
    {
        var (userId, cashId) = await OpenSessionAsync(UserRole.Admin);
        var productId = await SeedProductAsync(price: 100m);

        var request = new CreateSaleRequest
        {
            UserId = userId,
            CashSessionId = cashId,
            Items = [new SaleItemRequest { ProductId = productId, Quantity = 2 }], // 200
            GlobalDiscount = 100m, // 50% — Admin sin tope
            Payments = [new PaymentRequest { Method = PaymentMethod.Cash, Amount = 100m }]
        };

        var result = await _saleService.CreateSaleAsync(request);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task TopeConfigurable_SeLeeDeSettings()
    {
        var (userId, cashId) = await OpenSessionAsync(UserRole.Cajero);
        var productId = await SeedProductAsync(price: 100m);
        await _settings.SetIntAsync(SettingKeys.DiscountLimitCajero, 20); // tope subido a 20%

        var request = new CreateSaleRequest
        {
            UserId = userId,
            CashSessionId = cashId,
            Items = [new SaleItemRequest { ProductId = productId, Quantity = 1 }],
            GlobalDiscount = 15m, // 15% < 20% nuevo tope
            Payments = [new PaymentRequest { Method = PaymentMethod.Cash, Amount = 85m }]
        };

        var result = await _saleService.CreateSaleAsync(request);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Auditoria_Venta_QuedaRegistrada()
    {
        var (userId, cashId) = await OpenSessionAsync(UserRole.Admin);
        var productId = await SeedProductAsync(price: 100m);

        var request = new CreateSaleRequest
        {
            UserId = userId,
            CashSessionId = cashId,
            Items = [new SaleItemRequest { ProductId = productId, Quantity = 1 }],
            Payments = [new PaymentRequest { Method = PaymentMethod.Cash, Amount = 100m }]
        };

        await _saleService.CreateSaleAsync(request);

        var entries = await _db.AuditLogs.AsNoTracking().ToListAsync();
        Assert.Contains(entries, e => e.Action == AuditAction.SaleCreated && e.Detail.Contains("Recibo #1"));
    }
}