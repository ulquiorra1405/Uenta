using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.Application;
using POS.Infrastructure;
using POS.Infrastructure.Data;

namespace POS.Tests;

/// <summary>
/// Reproduce el arranque EXACTO de la app Desktop (App.OnStartup):
/// AddApplication + AddInfrastructure + DbSeeder.SeedAsync (MigrateAsync + seeding).
/// </summary>
public class AppStartupTests
{
    [Fact]
    public async Task Startup_DbLimpia_MigraYSiembra()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"pos-startup-{Guid.NewGuid():N}.db");
        try
        {
            var services = new ServiceCollection();
            services.AddApplication();
            services.AddInfrastructure($"Data Source={dbPath};Pooling=False");
            var sp = services.BuildServiceProvider();

            using (var scope = sp.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();
                await DbSeeder.SeedAsync(db); // ← aquí ocurriría el error de la app

                Assert.True(await db.Categories.AnyAsync());
                Assert.True(await db.Products.AnyAsync());
                Assert.True(await db.Sales.AnyAsync() == false); // sin ventas
            }
            sp.Dispose();
        }
        finally
        {
            // Espera a que SQLite libere el archivo (pooling) antes de borrar.
            GC.Collect(); GC.WaitForPendingFinalizers();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task Startup_DbExistenteDeUsuario_MigraYSiembra()
    {
        // Diagnóstico: copia la base REAL que creó la app del usuario y la migra igual que ella.
        var realDb = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Uenta", "pos.db");

        if (!File.Exists(realDb))
            return; // no hay base del usuario que diagnosticar

        var copyPath = Path.Combine(Path.GetTempPath(), $"pos-real-copy-{Guid.NewGuid():N}.db");
        File.Copy(realDb, copyPath);

        try
        {
            var services = new ServiceCollection();
            services.AddApplication();
            services.AddInfrastructure($"Data Source={copyPath};Pooling=False");
            var sp = services.BuildServiceProvider();

            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();
            await DbSeeder.SeedAsync(db); // ← si esto lanza, la base del usuario está corrupta/incompleta

            Assert.True(await db.Products.AnyAsync());
            sp.Dispose();
        }
        finally
        {
            GC.Collect(); GC.WaitForPendingFinalizers();
            if (File.Exists(copyPath)) File.Delete(copyPath);
        }
    }
}
