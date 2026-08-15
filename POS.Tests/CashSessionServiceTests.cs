using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.Application;
using POS.Application.Abstractions;
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
/// Fase 1B (P2.2): sesión de caja — apertura, retiros y cierre con diferencia.
/// </summary>
public class CashSessionServiceTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"pos-cash-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _services;
    private readonly PosDbContext _db;
    private readonly CashSessionService _cash;
    private readonly SaleService _saleService;
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private long _userId;

    public CashSessionServiceTests()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddInfrastructure($"Data Source={_dbPath};Pooling=False");
        _services = services.BuildServiceProvider();

        _db = _services.GetRequiredService<PosDbContext>();
        _db.Database.EnsureCreated();

        _cash = _services.GetRequiredService<CashSessionService>();
        _saleService = _services.GetRequiredService<SaleService>();
        _users = _services.GetRequiredService<IUserRepository>();
        _hasher = _services.GetRequiredService<IPasswordHasher>();

        var user = new User
        {
            Username = "cajero",
            DisplayName = "Cajero",
            PasswordHash = _hasher.Hash("cajero123"),
            Role = UserRole.Cajero,
            IsActive = true
        };
        _userId = _users.AddAsync(user).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _services.Dispose();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private async Task<long> SeedProductAsync(decimal price = 100m)
    {
        var product = new Product { Name = "P", Price = new Money(price), Cost = new Money(30m), Stock = 100, IsActive = true };
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return product.Id;
    }

    private async Task<long> SellAsync(long cashSessionId, PaymentMethod method, decimal amount)
    {
        var productId = await SeedProductAsync(price: amount);
        var request = new CreateSaleRequest
        {
            UserId = _userId,
            CashSessionId = cashSessionId,
            Items = [new SaleItemRequest { ProductId = productId, Quantity = 1 }],
            Payments = [new PaymentRequest { Method = method, Amount = amount }]
        };
        var result = await _saleService.CreateSaleAsync(request);
        Assert.True(result.IsSuccess);
        return result.Value!.Id;
    }

    [Fact]
    public async Task Open_NoAbierta_Ok()
    {
        var result = await _cash.OpenAsync(new OpenCashRequest(_userId, 500m));

        Assert.True(result.IsSuccess);
        Assert.Equal(500m, result.Value!.InitialCash);
        Assert.Equal(CashSessionStatus.Open, result.Value.Status);
    }

    [Fact]
    public async Task Open_YaAbierta_Falla()
    {
        await _cash.OpenAsync(new OpenCashRequest(_userId, 100m));

        var result = await _cash.OpenAsync(new OpenCashRequest(_userId, 200m));

        Assert.True(result.IsFailure);
        Assert.Equal("CASH_ALREADY_OPEN", result.ErrorCode);
    }

    [Fact]
    public async Task Open_EfectivoNegativo_Falla()
    {
        var result = await _cash.OpenAsync(new OpenCashRequest(_userId, -5m));

        Assert.True(result.IsFailure);
        Assert.Equal("INVALID_INITIAL_CASH", result.ErrorCode);
    }

    [Fact]
    public async Task Withdraw_SinMotivo_Falla()
    {
        var open = await _cash.OpenAsync(new OpenCashRequest(_userId, 500m));

        var result = await _cash.WithdrawAsync(new WithdrawRequest(open.Value!.Id, 100m, "   "));

        Assert.True(result.IsFailure);
        Assert.Equal("REASON_REQUIRED", result.ErrorCode);
    }

    [Fact]
    public async Task Withdraw_ConMotivo_Ok()
    {
        var open = await _cash.OpenAsync(new OpenCashRequest(_userId, 500m));

        var result = await _cash.WithdrawAsync(new WithdrawRequest(open.Value!.Id, 200m, "Pago a proveedor"));

        Assert.True(result.IsSuccess);
        Assert.Single(_db.CashWithdrawals);
    }

    [Fact]
    public async Task Close_SinVentas_DiferenciaEsConteoMenosInicial()
    {
        var open = await _cash.OpenAsync(new OpenCashRequest(_userId, 500m));

        var result = await _cash.CloseAsync(new CloseCashRequest(open.Value!.Id, 520m));

        Assert.True(result.IsSuccess);
        Assert.Equal(CashSessionStatus.Closed, result.Value!.Status);
        Assert.Equal(520m, result.Value.FinalCount);
        Assert.Equal(20m, result.Value.Difference); // 520 − 500
    }

    [Fact]
    public async Task Close_ConVentasYRetiros_DiferenciaCorrecta()
    {
        var open = await _cash.OpenAsync(new OpenCashRequest(_userId, 500m));
        var cashId = open.Value!.Id;

        // Venta en efectivo 300 → esperado = 500 + 300 = 800
        await SellAsync(cashId, PaymentMethod.Cash, 300m);
        // Retiro 50 → esperado = 800 − 50 = 750
        await _cash.WithdrawAsync(new WithdrawRequest(cashId, 50m, "Compra de bolsas"));

        var result = await _cash.CloseAsync(new CloseCashRequest(cashId, 750m));

        Assert.True(result.IsSuccess);
        Assert.Equal(750m, result.Value!.ExpectedCash);
        Assert.Equal(0m, result.Value.Difference); // conteo 750 − esperado 750
    }

    [Fact]
    public async Task Close_ConDiferencia_ReportaDescuadre()
    {
        var open = await _cash.OpenAsync(new OpenCashRequest(_userId, 100m));
        var cashId = open.Value!.Id;

        await SellAsync(cashId, PaymentMethod.Cash, 200m); // esperado 300

        var result = await _cash.CloseAsync(new CloseCashRequest(cashId, 290m));

        Assert.True(result.IsSuccess);
        Assert.Equal(-10m, result.Value!.Difference); // faltan 10
    }

    [Fact]
    public async Task Close_CajaYaCerrada_Falla()
    {
        var open = await _cash.OpenAsync(new OpenCashRequest(_userId, 100m));
        await _cash.CloseAsync(new CloseCashRequest(open.Value!.Id, 100m));

        var result = await _cash.CloseAsync(new CloseCashRequest(open.Value!.Id, 100m));

        Assert.True(result.IsFailure);
        Assert.Equal("CASH_ALREADY_CLOSED", result.ErrorCode);
    }

    [Fact]
    public async Task Withdraw_CajaCerrada_Falla()
    {
        var open = await _cash.OpenAsync(new OpenCashRequest(_userId, 100m));
        await _cash.CloseAsync(new CloseCashRequest(open.Value!.Id, 100m));

        var result = await _cash.WithdrawAsync(new WithdrawRequest(open.Value!.Id, 10m, "Tarde"));

        Assert.True(result.IsFailure);
        Assert.Equal("CASH_CLOSED", result.ErrorCode);
    }

    [Fact]
    public async Task GetOpenForUser_DespuesDeCerrar_DevuelveNull()
    {
        var open = await _cash.OpenAsync(new OpenCashRequest(_userId, 100m));
        await _cash.CloseAsync(new CloseCashRequest(open.Value!.Id, 100m));

        var result = await _cash.GetOpenForUserAsync(_userId);

        Assert.Null(result);
    }

    [Fact]
    public async Task Venta_SeAsociaALaCaja()
    {
        var open = await _cash.OpenAsync(new OpenCashRequest(_userId, 0m));
        var saleId = await SellAsync(open.Value!.Id, PaymentMethod.Cash, 200m);

        var sale = await _db.Sales.AsNoTracking().SingleAsync(s => s.Id == saleId);
        Assert.Equal(open.Value!.Id, sale.CashSessionId);
    }
}