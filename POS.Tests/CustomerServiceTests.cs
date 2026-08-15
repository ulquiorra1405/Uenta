using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.Application;
using POS.Application.Abstractions;
using POS.Application.Cash;
using POS.Application.Customers;
using POS.Application.Sales;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.ValueObjects;
using POS.Infrastructure;
using POS.Infrastructure.Data;

namespace POS.Tests;

/// <summary>
/// Fase 1D (P4.1): CRM básico — CRUD de clientes, validación de RNC duplicado
/// e historial de compras asociadas a un cliente.
/// </summary>
public class CustomerServiceTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"pos-customer-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _services;
    private readonly PosDbContext _db;
    private readonly CustomerService _customers;
    private readonly SaleService _saleService;
    private readonly CashSessionService _cash;
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private long _userId;

    public CustomerServiceTests()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddInfrastructure($"Data Source={_dbPath};Pooling=False");
        _services = services.BuildServiceProvider();

        _db = _services.GetRequiredService<PosDbContext>();
        _db.Database.EnsureCreated();

        _customers = _services.GetRequiredService<CustomerService>();
        _saleService = _services.GetRequiredService<SaleService>();
        _cash = _services.GetRequiredService<CashSessionService>();
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

    private static CreateCustomerRequest Request(string name = "María Pérez", string rnc = "001-1234567-8") =>
        new(name, "809-555-0101", rnc, "maria@ejemplo.com");

    /// <summary>Abre una caja para el usuario de prueba (la venta la exige, P2.2).</summary>
    private async Task<long> OpenCashAsync()
    {
        var result = await _cash.OpenAsync(new OpenCashRequest(_userId, 0));
        Assert.True(result.IsSuccess);
        return result.Value!.Id;
    }

    [Fact]
    public async Task Create_Valido_AgregaCliente()
    {
        var result = await _customers.CreateAsync(Request());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("María Pérez", result.Value!.Name);
        Assert.True(result.Value.Id > 0);
    }

    [Fact]
    public async Task Create_SinNombre_Falla()
    {
        var result = await _customers.CreateAsync(Request(name: "  "));

        Assert.False(result.IsSuccess);
        Assert.Equal("NAME_REQUIRED", result.ErrorCode);
    }

    [Fact]
    public async Task Create_RncDuplicado_Falla()
    {
        await _customers.CreateAsync(Request(rnc: "001-1111111-1"));

        var result = await _customers.CreateAsync(Request(rnc: "001-1111111-1"));

        Assert.False(result.IsSuccess);
        Assert.Equal("RNC_DUPLICATED", result.ErrorCode);
    }

    [Fact]
    public async Task Update_Valido_ActualizaDatos()
    {
        var created = await _customers.CreateAsync(Request());
        var id = created.Value!.Id;

        var result = await _customers.UpdateAsync(new UpdateCustomerRequest(
            id, "María J. Pérez", "829-555-0102", "001-1234567-8", "mjp@ejemplo.com"));

        Assert.True(result.IsSuccess);
        Assert.Equal("María J. Pérez", result.Value!.Name);
        Assert.Equal("mjp@ejemplo.com", result.Value!.Email);
    }

    [Fact]
    public async Task Update_ConRncDeOtro_Falla()
    {
        var a = await _customers.CreateAsync(Request(name: "A", rnc: "001-1111111-1"));
        var b = await _customers.CreateAsync(Request(name: "B", rnc: "002-2222222-2"));

        var result = await _customers.UpdateAsync(new UpdateCustomerRequest(
            b.Value!.Id, "B", "809-555-0101", "001-1111111-1", "b@ejemplo.com"));

        Assert.False(result.IsSuccess);
        Assert.Equal("RNC_DUPLICATED", result.ErrorCode);
        _ = a.Value!.Id;
    }

    [Fact]
    public async Task Update_Inexistente_Falla()
    {
        var result = await _customers.UpdateAsync(new UpdateCustomerRequest(
            9999, "X", "", "", ""));

        Assert.False(result.IsSuccess);
        Assert.Equal("CUSTOMER_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task GetAll_DevuelveOrdenadoPorNombre()
    {
        await _customers.CreateAsync(Request(name: "Zulema", rnc: "001-1000000-1"));
        await _customers.CreateAsync(Request(name: "Ana", rnc: "001-2000000-2"));

        var all = await _customers.GetAllAsync();

        Assert.Equal(2, all.Count);
        Assert.Equal("Ana", all[0].Name);
        Assert.Equal("Zulema", all[1].Name);
    }

    [Fact]
    public async Task GetHistory_VentasAsociadas_MuestraCompras()
    {
        var customer = await _customers.CreateAsync(Request());
        var cashId = await OpenCashAsync();
        var product = new Product { Name = "P", Price = new Money(100m), Cost = new Money(30m), Stock = 100, IsActive = true };
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        var saleResult = await _saleService.CreateSaleAsync(new CreateSaleRequest
        {
            UserId = _userId,
            CustomerId = customer.Value!.Id,
            CashSessionId = cashId,
            Items = [new SaleItemRequest { ProductId = product.Id, Quantity = 2 }],
            Payments = [new PaymentRequest { Method = PaymentMethod.Cash, Amount = 200m }]
        });
        Assert.True(saleResult.IsSuccess);

        var history = await _customers.GetHistoryAsync(customer.Value!.Id);

        Assert.True(history.IsSuccess);
        var sale = Assert.Single(history.Value!);
        Assert.Equal(saleResult.Value!.Number, sale.Number);
        Assert.Equal(1, sale.ItemCount);    // 1 línea del carrito (cantidad 2)
        Assert.Equal(200m, sale.Total.Amount);
        Assert.Equal("Cajero", sale.UserName);
    }

    [Fact]
    public async Task GetHistory_VentasAnonimasNoAparecen()
    {
        var customer = await _customers.CreateAsync(Request());
        var cashId = await OpenCashAsync();
        var product = new Product { Name = "P", Price = new Money(50m), Cost = new Money(10m), Stock = 100, IsActive = true };
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        // Venta sin CustomerId (anónima)
        await _saleService.CreateSaleAsync(new CreateSaleRequest
        {
            UserId = _userId,
            CashSessionId = cashId,
            Items = [new SaleItemRequest { ProductId = product.Id, Quantity = 1 }],
            Payments = [new PaymentRequest { Method = PaymentMethod.Cash, Amount = 50m }]
        });

        var history = await _customers.GetHistoryAsync(customer.Value!.Id);

        Assert.True(history.IsSuccess);
        Assert.Empty(history.Value!);
    }

    [Fact]
    public async Task GetHistory_Inexistente_Falla()
    {
        var result = await _customers.GetHistoryAsync(9999);

        Assert.False(result.IsSuccess);
        Assert.Equal("CUSTOMER_NOT_FOUND", result.ErrorCode);
    }
}