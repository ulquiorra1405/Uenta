using Microsoft.Extensions.DependencyInjection;

namespace POS.Desktop.ViewModels;

public interface INavigationService
{
    ViewModelBase? Current { get; }
    event Action<ViewModelBase?>? CurrentChanged;
    Task NavigateToAsync<TViewModel>(Action<TViewModel>? configure = null) where TViewModel : ViewModelBase;
}

/// <summary>
/// Navegación simple entre ViewModels (inyectados por DI). El MainWindow bindea
/// su ContentControl a <see cref="Current"/>; los DataTemplates eligen la vista.
/// Devuelve <see cref="Task"/> (nunca async void): los invocadores await-ean y
/// deciden cómo reportar un fallo de carga.
/// </summary>
public class NavigationService : INavigationService
{
    private readonly IServiceProvider _services;
    private ViewModelBase? _current;

    public NavigationService(IServiceProvider services) => _services = services;

    public ViewModelBase? Current => _current;

    public event Action<ViewModelBase?>? CurrentChanged;

    public async Task NavigateToAsync<TViewModel>(Action<TViewModel>? configure = null) where TViewModel : ViewModelBase
    {
        var vm = _services.GetRequiredService<TViewModel>();
        configure?.Invoke(vm);

        _current = vm;
        CurrentChanged?.Invoke(vm);
        await vm.OnNavigatedToAsync();
    }
}