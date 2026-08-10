using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace POS.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public INavigationService Navigation { get; }

    public MainWindowViewModel(INavigationService navigation)
    {
        Navigation = navigation;
        GoCatalogCommand = new RelayCommand(() => Navigation.NavigateTo<ProductListViewModel>());
        GoSalesCommand = new RelayCommand(() => Navigation.NavigateTo<SaleViewModel>());
        ToggleSidebarCommand = new RelayCommand(ToggleSidebar);

        Navigation.CurrentChanged += _ =>
        {
            // Importante: notificar TODOS los flags, no solo Current. Si no,
            // el sidebar nunca se entera de que cambió la pantalla activa.
            OnPropertyChanged(nameof(Current));
            OnPropertyChanged(nameof(IsCatalogActive));
            OnPropertyChanged(nameof(IsSalesActive));
        };
    }

    /// <summary>Vista actual; el ContentControl del MainWindow bindea aquí.</summary>
    public ViewModelBase? Current => Navigation.Current;

    public RelayCommand GoCatalogCommand { get; }
    public RelayCommand GoSalesCommand { get; }
    public RelayCommand ToggleSidebarCommand { get; }

    /// <summary>Sidebar colapsado a solo iconos (64px) o expandido (210px).</summary>
    [ObservableProperty]
    private bool _isSidebarCollapsed;

    /// <summary>Ancho del sidebar en DIPs: 210 expandido / 64 solo iconos.</summary>
    public double SidebarWidth => IsSidebarCollapsed ? 64 : 210;

    /// <summary>Item de navegación activo según la vista actual.</summary>
    /// <remarks>Editar producto pertenece a Catálogo (se mantiene el item activo).</remarks>
    public bool IsCatalogActive => Current is ProductListViewModel or ProductEditViewModel;
    public bool IsSalesActive => Current is SaleViewModel;

    partial void OnIsSidebarCollapsedChanged(bool value)
    {
        OnPropertyChanged(nameof(SidebarWidth));
        OnPropertyChanged(nameof(IsCatalogActive));
        OnPropertyChanged(nameof(IsSalesActive));
    }

    private void ToggleSidebar() => IsSidebarCollapsed = !IsSidebarCollapsed;
}
