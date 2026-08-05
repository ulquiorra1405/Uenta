using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.Application;
using POS.Desktop.ViewModels;
using POS.Infrastructure;
using POS.Infrastructure.Data;

namespace POS.Desktop;

/// <summary>
/// Composition Root: único lugar que conoce todas las capas.
/// Arma el contenedor, aplica migraciones, siembra datos demo y muestra la ventana.
/// </summary>
public partial class App : System.Windows.Application
{
    private ServiceProvider? _services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var dbDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Uenta");
        Directory.CreateDirectory(dbDir);

        var services = new ServiceCollection();
        services.AddApplication();
        services.AddInfrastructure($"Data Source={Path.Combine(dbDir, "pos.db")}");

        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();
        services.AddTransient<ProductListViewModel>();
        services.AddTransient<ProductEditViewModel>();
        services.AddTransient<PlaceholderViewModel>();

        _services = services.BuildServiceProvider();

        try
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();
            await DbSeeder.SeedAsync(db);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al iniciar la base de datos:\n{ex.Message}",
                "Uenta", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        var mainWindow = _services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }
}
