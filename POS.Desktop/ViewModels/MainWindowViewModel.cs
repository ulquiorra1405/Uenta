using CommunityToolkit.Mvvm.Input;

namespace POS.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public INavigationService Navigation { get; }

    public MainWindowViewModel(INavigationService navigation)
    {
        Navigation = navigation;
        GoCatalogCommand = new RelayCommand(() => Navigation.NavigateTo<ProductListViewModel>());
        GoSalesCommand = new RelayCommand(() => Navigation.NavigateTo<PlaceholderViewModel>(vm => vm.Message = "El módulo de Ventas llega en el próximo incremento."));
    }

    public RelayCommand GoCatalogCommand { get; }
    public RelayCommand GoSalesCommand { get; }
}
