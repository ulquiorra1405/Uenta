using Microsoft.Extensions.DependencyInjection;
using POS.Application;
using POS.Application.Abstractions;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.ValueObjects;
using POS.Infrastructure;
using POS.Infrastructure.Data;
using POS.Infrastructure.Services;

namespace POS.Tests;

/// <summary>
/// Fase 1D (P4.3): backup/restore de la base SQLite.
/// Export produce un archivo consistente; restore valida ANTES de tocar la DB actual
/// (archivo inválido → error claro y datos intactos).
/// </summary>
public class DatabaseBackupServiceTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"pos-backup-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _services;
    private readonly PosDbContext _db;
    private readonly IDatabaseBackupService _backup;

    public DatabaseBackupServiceTests()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddInfrastructure($"Data Source={_dbPath};Pooling=False");
        _services = services.BuildServiceProvider();

        _db = _services.GetRequiredService<PosDbContext>();
        _db.Database.EnsureCreated();
        _backup = _services.GetRequiredService<IDatabaseBackupService>();

        SeedProduct();
    }

    private void SeedProduct()
    {
        var category = new Category { Name = "Bebidas" };
        _db.Categories.Add(category);
        _db.Products.Add(new Product
        {
            Name = "Café con leche",
            Sku = "CAF-001",
            Barcode = null,
            Price = new Money(100),
            Cost = new Money(60),
            Stock = 10,
            Category = category,
            IsActive = true,
        });
        _db.SaveChanges();
    }

    [Fact]
    public async Task Export_CreaArchivoQueSePuedeAbrirComoSqlite()
    {
        var dest = Path.Combine(Path.GetTempPath(), $"pos-export-{Guid.NewGuid():N}.db");
        try
        {
            var result = await _backup.ExportAsync(dest);

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.True(File.Exists(dest));
            Assert.True(new FileInfo(dest).Length > 0);

            // El archivo exportado es una DB SQLite legible con el mismo contenido.
            await using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dest};Mode=ReadOnly;Pooling=False");
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Products";
            var count = Convert.ToInt64(await cmd.ExecuteScalarAsync());
            Assert.True(count >= 1, "El backup exportado no contiene los productos.");
        }
        finally
        {
            File.Delete(dest);
        }
    }

    [Fact]
    public async Task Restore_ArchivoInexistente_FallaConErrorClaro()
    {
        var result = await _backup.RestoreAsync(Path.Combine(Path.GetTempPath(), "no-existe-xyz.db"));

        Assert.True(result.IsFailure);
        Assert.Equal("RESTORE_NOT_FOUND", result.ErrorCode);
        Assert.Contains("no existe", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        // La DB actual sigue intacta.
        Assert.True(_db.Products.Count() >= 1);
    }

    [Fact]
    public async Task Restore_ArchivoNoSqlite_FallaConErrorYDbIntacta()
    {
        var fake = Path.Combine(Path.GetTempPath(), $"fake-{Guid.NewGuid():N}.db");
        await File.WriteAllTextAsync(fake, "esto no es una base de datos sqlite, solo texto de relleno");

        try
        {
            var result = await _backup.RestoreAsync(fake);

            Assert.True(result.IsFailure);
            Assert.Equal("RESTORE_INVALID", result.ErrorCode);
            Assert.Contains("no es una base de datos", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);

            // La DB actual sigue intacta.
            Assert.True(_db.Products.Count() >= 1);
        }
        finally
        {
            File.Delete(fake);
        }
    }

    [Fact]
    public async Task Restore_SqliteDeOtraApp_FallaPorTablasFaltantes()
    {
        var other = Path.Combine(Path.GetTempPath(), $"other-{Guid.NewGuid():N}.db");
        try
        {
            // Una DB SQLite válida pero sin el esquema de Uenta.
            await using (var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={other};Pooling=False"))
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "CREATE TABLE OtraApp (Id INTEGER); INSERT INTO OtraApp VALUES (1);";
                await cmd.ExecuteNonQueryAsync();
            }

            var result = await _backup.RestoreAsync(other);

            Assert.True(result.IsFailure);
            Assert.Equal("RESTORE_INVALID", result.ErrorCode);
            Assert.Contains("faltan tablas", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.True(_db.Products.Count() >= 1);
        }
        finally
        {
            File.Delete(other);
        }
    }

    [Fact]
    public async Task Restore_BackupValido_ReemplazaDatos()
    {
        // 1. Exportar la DB actual (tiene el producto sembrado).
        var backupFile = Path.Combine(Path.GetTempPath(), $"pos-restore-src-{Guid.NewGuid():N}.db");
        try
        {
            var export = await _backup.ExportAsync(backupFile);
            Assert.True(export.IsSuccess, export.ErrorMessage);

            // 2. Modificar la DB actual (agregar otro producto).
            _db.Products.Add(new Product
            {
                Name = "Jugo de naranja",
                Sku = "JGO-001",
                Barcode = null,
                Price = new Money(80),
                Cost = new Money(40),
                Stock = 5,
                CategoryId = _db.Categories.First().Id,
                IsActive = true,
            });
            _db.SaveChanges();
            Assert.Equal(2, _db.Products.Count());

            // 3. Restaurar desde el backup → vuelve a 1 producto.
            var restore = await _backup.RestoreAsync(backupFile);
            Assert.True(restore.IsSuccess, restore.ErrorMessage);

            // Nuevo contexto (misma DB) para verificar el estado restaurado.
            using var scope = _services.CreateScope();
            var db2 = scope.ServiceProvider.GetRequiredService<PosDbContext>();
            Assert.Equal(1, db2.Products.Count());
            Assert.Equal("Café con leche", db2.Products.Single().Name);
        }
        finally
        {
            File.Delete(backupFile);
        }
    }

    public void Dispose()
    {
        _db.Dispose();
        _services.Dispose();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }
}