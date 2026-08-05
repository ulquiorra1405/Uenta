using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Application.Products;

namespace POS.Desktop.ViewModels;

public partial class ProductListViewModel : ViewModelBase
{
    private readonly ProductService _productService;
    private readonly INavigationService _navigation;

    public ProductListViewModel(ProductService productService, INavigationService navigation)
    {
        _productService = productService;
        _navigation = navigation;
    }

    public ObservableCollection<ProductDto> Products { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeactivateCommand))]
    private ProductDto? _selectedProduct;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isBusy;

    public override async Task OnNavigatedToAsync() => await LoadAsync();

    [RelayCommand]
    private async Task SearchAsync()
    {
        await LoadAsync();
    }

    [RelayCommand]
    private void New()
    {
        _navigation.NavigateTo<ProductEditViewModel>();
    }

    private bool CanEditOrDeactivate() => SelectedProduct is not null;

    [RelayCommand(CanExecute = nameof(CanEditOrDeactivate))]
    private void Edit()
    {
        if (SelectedProduct is null) return;
        _navigation.NavigateTo<ProductEditViewModel>(vm => vm.Load(SelectedProduct));
    }

    [RelayCommand(CanExecute = nameof(CanEditOrDeactivate))]
    private async Task DeactivateAsync()
    {
        if (SelectedProduct is null) return;

        var confirm = MessageBox.Show(
            $"¿Desactivar '{SelectedProduct.Name}'?\nSe ocultará del catálogo pero se conservará su historial.",
            "Uenta", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        var result = await _productService.DeactivateAsync(SelectedProduct.Id);
        StatusMessage = result.IsSuccess ? "Producto desactivado." : $"Error: {result.ErrorMessage}";
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var products = await _productService.SearchAsync(SearchText);
            Products.Clear();
            foreach (var p in products)
                Products.Add(p);

            StatusMessage = $"{products.Count} producto(s)";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
