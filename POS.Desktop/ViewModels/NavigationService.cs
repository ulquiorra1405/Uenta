using Microsoft.Extensions.DependencyInjection;

namespace POS.Desktop.ViewModels;

public interface INavigationService
{
    ViewModelBase? Current { get; }
    event Action<ViewModelBase?>? CurrentChanged;
    void NavigateTo<TViewModel>(Action<TViewModel>? configure = null) where TViewModel : ViewModelBase;
}

/// <summary>
/// Navegación simple entre ViewModels (inyectados por DI). El MainWindow bindea
/// su ContentControl a <see cref="Current"/>; los DataTemplates eligen la vista.
/// </summary>
public class NavigationService : INavigationService
{
    private readonly IServiceProvider _services;
    private ViewModelBase? _current;

    public NavigationService(IServiceProvider services) => _services = services;

    public ViewModelBase? Current => _current;

    public event Action<ViewModelBase?>? CurrentChanged;

    public async void NavigateTo<TViewModel>(Action<TViewModel>? configure = null) where TViewModel : ViewModelBase
    {
        var vm = _services.GetRequiredService<TViewModel>();
        configure?.Invoke(vm);

        _current = vm;
        CurrentChanged?.Invoke(vm);
        await vm.OnNavigatedToAsync();
    }
}
