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

        Navigation.CurrentChanged += _ => OnPropertyChanged(nameof(Current));
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
