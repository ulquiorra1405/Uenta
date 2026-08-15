using Microsoft.Extensions.DependencyInjection;
using POS.Application;
using POS.Application.Settings;
using POS.Infrastructure;
using POS.Infrastructure.Data;

namespace POS.Tests;

/// <summary>
/// SettingsService (Fase 1A, P1.3): lectura tipada con defaults, persistencia
/// entre instancias (misma DB) y clamp de copias.
/// </summary>
public class SettingsServiceTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"pos-settings-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _services;
    private readonly PosDbContext _db;

    public SettingsServiceTests()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddInfrastructure($"Data Source={_dbPath};Pooling=False");
        _services = services.BuildServiceProvider();

        _db = _services.GetRequiredService<PosDbContext>();
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _services.Dispose();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private SettingsService CreateService()
        => _services.GetRequiredService<SettingsService>();

    [Fact]
    public async Task Get_SinValor_DevuelveDefault()
    {
        var svc = CreateService();

        var name = await svc.GetAsync(SettingKeys.BusinessName, "Mi Negocio");
        var autoPrint = await svc.GetBoolAsync(SettingKeys.AutoPrint, true);
        var copies = await svc.GetIntAsync(SettingKeys.Copies, 1);

        Assert.Equal("Mi Negocio", name);
        Assert.True(autoPrint);
        Assert.Equal(1, copies);
    }

    [Fact]
    public async Task SetYGet_ValorPersistidoEntreInstancias()
    {
        var svc1 = CreateService();
        await svc1.SetAsync(SettingKeys.PrinterName, "EPSON TM-T20");
        await svc1.SetBoolAsync(SettingKeys.AutoPrint, false);
        await svc1.SetIntAsync(SettingKeys.Copies, 2);

        // Instancia nueva sobre la MISMA DB → lee lo guardado.
        var svc2 = CreateService();
        Assert.Equal("EPSON TM-T20", await svc2.GetAsync(SettingKeys.PrinterName));
        Assert.False(await svc2.GetBoolAsync(SettingKeys.AutoPrint, true));
        Assert.Equal(2, await svc2.GetIntAsync(SettingKeys.Copies, 1));
    }

    [Fact]
    public async Task Set_ActualizaClaveExistente_NoDuplica()
    {
        var svc = CreateService();
        await svc.SetAsync(SettingKeys.BusinessName, "Primero");
        await svc.SetAsync(SettingKeys.BusinessName, "Segundo");

        Assert.Equal("Segundo", await svc.GetAsync(SettingKeys.BusinessName));
        Assert.Single(await _services.GetRequiredService<POS.Application.Abstractions.ISettingRepository>()
            .GetAllAsync());
    }

    [Fact]
    public async Task GetReceiptSettings_ComponeDtoConClampDeCopias()
    {
        var svc = CreateService();
        await svc.SetIntAsync(SettingKeys.Copies, 99); // fuera de rango 1-9
        await svc.SetAsync(SettingKeys.BusinessName, "Colmado La Esquina");

        var dto = await svc.GetReceiptSettingsAsync();

        Assert.Equal(9, dto.Copies); // clamp
        Assert.Equal("Colmado La Esquina", dto.BusinessName);
        Assert.True(dto.AutoPrint); // default
    }
}