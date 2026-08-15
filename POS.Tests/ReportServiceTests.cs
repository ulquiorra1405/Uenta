using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.Application;
using POS.Application.Abstractions;
using POS.Application.Reports;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.ValueObjects;
using POS.Infrastructure;
using POS.Infrastructure.Data;

namespace POS.Tests;

/// <summary>
/// Fase 1D (P4.2): reportes y dashboard — agregados de ventas del día,
/// por periodo, top productos y ventas por vendedor.
/// </summary>
public class ReportServiceTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"pos-report-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _services;
    private readonly PosDbContext _db;
    private readonly ReportService _reports;
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private long _adminId;
    private long _cajeroId;

    public ReportServiceTests()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddInfrastructure($"Data Source={_dbPath};Pooling=False");
        _services = services.BuildServiceProvider();

        _db = _services.GetRequiredService<PosDbContext>();
        _db.Database.EnsureCreated();

        _reports = _services.GetRequiredService<ReportService>();
        _users = _services.GetRequiredService<IUserRepository>();
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
            DisplayName = "Cajero Uno",
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

    /// <summary>Inserta una venta completada con fecha fija (los reportes leen CreatedAt).</summary>
    private async Task AddSaleAsync(DateTimeOffset createdAt, long userId, string productName,
        decimal quantity, decimal total, long number)
    {
        _db.Sales.Add(new Sale
        {
            Number = number,
            CreatedAt = createdAt,
            UserId = userId,
            Subtotal = new Money(total / 1.18m),
            Itbis = new Money(total - total / 1.18m),
            Discount = Money.Zero,
            Total = new Money(total),
            Status = SaleStatus.Completed,
            Items = [new SaleItem { ProductName = productName, Quantity = quantity, UnitPrice = new Money(total / quantity), Total = new Money(total) }]
        });
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetDailySummary_VentasDelDia_CalculaTotalTicketsYPromedio()
    {
        var today = new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.FromHours(-4));
        await AddSaleAsync(today, _cajeroId, "Café", 1, 100m, 1);
        await AddSaleAsync(today, _cajeroId, "Pan", 2, 50m, 2);
        await AddSaleAsync(today.AddDays(-2), _cajeroId, "Jugo", 1, 80m, 3); // fuera del día

        var summary = await _reports.GetDailySummaryAsync(today);

        Assert.Equal(2, summary.TicketCount);
        Assert.Equal(150m, summary.Total.Amount);
        Assert.Equal(75m, summary.AverageTicket.Amount);
        Assert.Equal(2, summary.ItemCount);
    }

    [Fact]
    public async Task GetDailySummary_SinVentas_DevuelveCeros()
    {
        var summary = await _reports.GetDailySummaryAsync(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.FromHours(-4)));

        Assert.Equal(0, summary.TicketCount);
        Assert.Equal(0m, summary.Total.Amount);
        Assert.Equal(0m, summary.AverageTicket.Amount);
        Assert.Equal(0, summary.ItemCount);
    }

    [Fact]
    public async Task GetPeriodSummary_SumaSoloVentasDelRango()
    {
        var from = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.FromHours(-4));
        var to = new DateTimeOffset(2026, 8, 16, 0, 0, 0, TimeSpan.FromHours(-4));
        await AddSaleAsync(new DateTimeOffset(2026, 8, 5, 10, 0, 0, TimeSpan.FromHours(-4)), _cajeroId, "A", 1, 30m, 1);
        await AddSaleAsync(new DateTimeOffset(2026, 8, 10, 10, 0, 0, TimeSpan.FromHours(-4)), _cajeroId, "B", 1, 70m, 2);
        await AddSaleAsync(new DateTimeOffset(2026, 7, 31, 10, 0, 0, TimeSpan.FromHours(-4)), _cajeroId, "C", 1, 999m, 3); // antes

        var summary = await _reports.GetPeriodSummaryAsync(from, to);

        Assert.Equal(2, summary.TicketCount);
        Assert.Equal(100m, summary.Total.Amount);
        Assert.Equal(50m, summary.AverageTicket.Amount);
    }

    [Fact]
    public async Task GetTopProducts_AgrupaPorNombreYOrdenaPorCantidad()
    {
        var from = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.FromHours(-4));
        var to = new DateTimeOffset(2026, 8, 16, 0, 0, 0, TimeSpan.FromHours(-4));
        await AddSaleAsync(new DateTimeOffset(2026, 8, 5, 10, 0, 0, TimeSpan.FromHours(-4)), _cajeroId, "Café", 1, 100m, 1);
        await AddSaleAsync(new DateTimeOffset(2026, 8, 6, 10, 0, 0, TimeSpan.FromHours(-4)), _cajeroId, "Café", 2, 200m, 2);
        await AddSaleAsync(new DateTimeOffset(2026, 8, 7, 10, 0, 0, TimeSpan.FromHours(-4)), _cajeroId, "Pan", 5, 125m, 3);

        var top = await _reports.GetTopProductsAsync(from, to, top: 5);

        Assert.Equal(2, top.Count);
        Assert.Equal("Pan", top[0].ProductName);      // 5 un > 3 un
        Assert.Equal(5m, top[0].Quantity);
        Assert.Equal("Café", top[1].ProductName);
        Assert.Equal(3m, top[1].Quantity);
        Assert.Equal(300m, top[1].Total.Amount);
    }

    [Fact]
    public async Task GetTopProducts_RespetaTopN()
    {
        var from = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.FromHours(-4));
        var to = new DateTimeOffset(2026, 8, 16, 0, 0, 0, TimeSpan.FromHours(-4));
        await AddSaleAsync(from, _cajeroId, "A", 1, 10m, 1);
        await AddSaleAsync(from, _cajeroId, "B", 2, 20m, 2);
        await AddSaleAsync(from, _cajeroId, "C", 3, 30m, 3);

        var top = await _reports.GetTopProductsAsync(from, to, top: 2);

        Assert.Equal(2, top.Count);
        Assert.Equal("C", top[0].ProductName);
        Assert.Equal("B", top[1].ProductName);
    }

    [Fact]
    public async Task GetSalesByUser_AgrupaPorVendedor()
    {
        var from = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.FromHours(-4));
        var to = new DateTimeOffset(2026, 8, 16, 0, 0, 0, TimeSpan.FromHours(-4));
        await AddSaleAsync(from, _adminId, "A", 1, 40m, 1);
        await AddSaleAsync(from, _cajeroId, "B", 1, 60m, 2);
        await AddSaleAsync(from, _cajeroId, "C", 1, 20m, 3);

        var byUser = await _reports.GetSalesByUserAsync(from, to);

        Assert.Equal(2, byUser.Count);
        Assert.Equal("Cajero Uno", byUser[0].UserName);   // 80 > 40
        Assert.Equal(2, byUser[0].TicketCount);
        Assert.Equal(80m, byUser[0].Total.Amount);
        Assert.Equal("Admin", byUser[1].UserName);
        Assert.Equal(40m, byUser[1].Total.Amount);
    }

    [Fact]
    public async Task GetSalesByUser_UsuarioSinVentas_NoAparece()
    {
        var from = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.FromHours(-4));
        var to = new DateTimeOffset(2026, 8, 16, 0, 0, 0, TimeSpan.FromHours(-4));
        await AddSaleAsync(from, _adminId, "A", 1, 40m, 1);

        var byUser = await _reports.GetSalesByUserAsync(from, to);

        // Solo el vendedor con ventas aparece (Cajero Uno no vendió en el rango).
        var byUserSale = Assert.Single(byUser);
        Assert.Equal("Admin", byUserSale.UserName);
        Assert.Equal(40m, byUserSale.Total.Amount);
    }
}