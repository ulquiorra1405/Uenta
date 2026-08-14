using System.ComponentModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace POS.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly DispatcherTimer _clockTimer;
    private ViewModelBase? _observedVm;

    public INavigationService Navigation { get; }

    public MainWindowViewModel(INavigationService navigation)
    {
        Navigation = navigation;
        GoCatalogCommand = new AsyncRelayCommand(GoCatalogAsync);
        GoSalesCommand = new AsyncRelayCommand(GoSalesAsync);
        ToggleSidebarCommand = new RelayCommand(ToggleSidebar);

        Navigation.CurrentChanged += _ =>
        {
            // Importante: notificar TODOS los flags, no solo Current. Si no,
            // el sidebar nunca se entera de que cambió la pantalla activa.
            OnPropertyChanged(nameof(Current));
            OnPropertyChanged(nameof(IsCatalogActive));
            OnPropertyChanged(nameof(IsSalesActive));
            ObserveOverlayFlags();
        };

        // Reloj de la barra de título (siempre visible).
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _clockTimer.Tick += (_, _) => OnPropertyChanged(nameof(ClockText));
        _clockTimer.Start();
        OnPropertyChanged(nameof(ClockText));

        // IMPORTANTE: suscribirse al VM inicial. CurrentChanged solo se dispara al
        // NAVEGAR; si el VM inicial ya está activo, sin esto el sidebar nunca
        // reacciona al primer overlay (bug reportado: "no funciona siempre").
        ObserveOverlayFlags();
    }

    /// <summary>Escucha los flags de overlay del VM activo para que el sidebar
    /// "se funda" con el scrim (mismo fondo, sin borde) cuando hay un popup.</summary>
    private void ObserveOverlayFlags()
    {
        if (_observedVm is not null)
            _observedVm.PropertyChanged -= OnVmPropertyChanged;
        _observedVm = Navigation.Current;
        if (_observedVm is not null)
            _observedVm.PropertyChanged += OnVmPropertyChanged;
        UpdateIsAnyOverlayOpen();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SaleViewModel.IsCatalogOpen):
            case nameof(SaleViewModel.IsPaymentOpen):
            case nameof(SaleViewModel.IsResultOpen):
            case nameof(ProductListViewModel.IsCategoryManagerOpen):
            case nameof(ProductListViewModel.IsProductEditorOpen):
                UpdateIsAnyOverlayOpen();
                break;
        }
    }

    /// <summary>True si hay un popup abierto. Con esto el sidebar deja de ser blanco
    /// y se vuelve del mismo color que la vista → el scrim (un solo Grid sobre todo)
    /// se ve perfectamente continuo, sin línea en la frontera.</summary>
    [ObservableProperty]
    private bool _isAnyOverlayOpen;

    private void UpdateIsAnyOverlayOpen()
    {
        IsAnyOverlayOpen = Navigation.Current switch
        {
            SaleViewModel s => s.IsCatalogOpen || s.IsPaymentOpen || s.IsResultOpen,
            ProductListViewModel p => p.IsCategoryManagerOpen || p.IsProductEditorOpen,
            _ => false,
        };
    }

    /// <summary>Hora actual para la barra de título (formato 24h).</summary>
    public string ClockText => DateTime.Now.ToString("HH:mm");

    private async Task GoCatalogAsync()
    {
        try { await Navigation.NavigateToAsync<ProductListViewModel>(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MainWindow] Catálogo: {ex}"); }
    }

    private async Task GoSalesAsync()
    {
        try { await Navigation.NavigateToAsync<SaleViewModel>(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MainWindow] Ventas: {ex}"); }
    }

    /// <summary>Vista actual; el ContentControl del MainWindow bindea aquí.</summary>
    public ViewModelBase? Current => Navigation.Current;

    public AsyncRelayCommand GoCatalogCommand { get; }
    public AsyncRelayCommand GoSalesCommand { get; }
    public RelayCommand ToggleSidebarCommand { get; }

    /// <summary>Sidebar colapsado a solo iconos (64px) o expandido (210px).</summary>
    [ObservableProperty]
    private bool _isSidebarCollapsed;

    /// <summary>Ancho del sidebar en DIPs: 210 expandido / 64 solo iconos.</summary>
    public double SidebarWidth => IsSidebarCollapsed ? 64 : 210;

    /// <summary>Item de navegación activo según la vista actual.</summary>
    /// <remarks>El editor de producto es un popup dentro de la lista; no cambia la sección.</remarks>
    public bool IsCatalogActive => Current is ProductListViewModel;
    public bool IsSalesActive => Current is SaleViewModel;

    partial void OnIsSidebarCollapsedChanged(bool value)
    {
        OnPropertyChanged(nameof(SidebarWidth));
        OnPropertyChanged(nameof(IsCatalogActive));
        OnPropertyChanged(nameof(IsSalesActive));
    }

    private void ToggleSidebar() => IsSidebarCollapsed = !IsSidebarCollapsed;
}
