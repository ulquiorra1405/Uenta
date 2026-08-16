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

        // Captura excepciones no manejadas del hilo UI (diagnóstico P5.1): la app
        // se moría silenciosamente al navegar a Devoluciones.
        DispatcherUnhandledException += (_, args) =>
        {
            try
            {
                var dbDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Uenta");
                File.WriteAllText(Path.Combine(dbDir, "unhandled-error.log"),
                    $"[{DateTimeOffset.Now:O}] {args.Exception}\n\n{args.Exception.StackTrace}");
            }
            catch { /* best-effort */ }
        };

        var dbDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Uenta");
        Directory.CreateDirectory(dbDir);

        // Pooling=False: SQLite mantiene el archivo abierto entre conexiones y puede
        // bloquear el arranque de una segunda instancia (o dejar la DB "locked").
        var dbPath = Path.Combine(dbDir, "pos.db");
        var connectionString = $"Data Source={dbPath};Pooling=False";

        var services = new ServiceCollection();
        services.AddApplication();
        services.AddInfrastructure(connectionString);

        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<CashSessionTracker>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();
        services.AddTransient<ProductListViewModel>();
        services.AddTransient<ProductEditViewModel>();
        services.AddTransient<SaleViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<UsersViewModel>();
        services.AddTransient<CustomersViewModel>();
        services.AddTransient<ReportsViewModel>();
        services.AddTransient<RefundsViewModel>();
        services.AddTransient<PurchasesViewModel>();
        services.AddTransient<LoginViewModel>();
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
            // Registra el error completo en disco además del MessageBox (diagnóstico).
            try
            {
                await File.WriteAllTextAsync(Path.Combine(dbDir, "startup-error.log"),
                    $"[{DateTimeOffset.Now:O}] {ex}\n\n{ex.StackTrace}");
            }
            catch { /* el log es best-effort */ }

            MessageBox.Show($"Error al iniciar la base de datos:\n{ex.Message}",
                "Uenta", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        // Vista inicial: login (P2.1c). La venta solo se abre tras autenticar.
        var navigation = _services.GetRequiredService<INavigationService>();
        try
        {
            await navigation.NavigateToAsync<LoginViewModel>();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al iniciar la pantalla de login:\n{ex.Message}",
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
