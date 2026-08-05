using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Application.Products;

namespace POS.Desktop.ViewModels;

public partial class ProductEditViewModel : ViewModelBase
{
    private readonly ProductService _productService;
    private readonly CategoryService _categoryService;
    private readonly INavigationService _navigation;

    public ProductEditViewModel(ProductService productService, CategoryService categoryService, INavigationService navigation)
    {
        _productService = productService;
        _categoryService = categoryService;
        _navigation = navigation;
    }

    [ObservableProperty]
    private string _title = "Nuevo producto";

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string? _sku;

    [ObservableProperty]
    private string? _barcode;

    [ObservableProperty]
    private long? _selectedCategoryId;

    [ObservableProperty]
    private decimal _price;

    [ObservableProperty]
    private decimal _cost;

    [ObservableProperty]
    private decimal _stock;

    [ObservableProperty]
    private decimal _minStock;

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private string? _errorMessage;

    public ObservableCollection<CategoryDto> Categories { get; } = [];

    /// <summary>Null = crear; distinto de null = editar.</summary>
    private long? _editId;

    public bool IsEditing => _editId is not null;

    public override async Task OnNavigatedToAsync()
    {
        var categories = await _categoryService.GetAllAsync();
        Categories.Clear();
        foreach (var c in categories)
            Categories.Add(c);
    }

    /// <summary>Carga un producto existente para edición.</summary>
    public void Load(ProductDto product)
    {
        _editId = product.Id;
        Title = $"Editar — {product.Name}";
        Name = product.Name;
        Sku = product.Sku;
        Barcode = product.Barcode;
        SelectedCategoryId = product.CategoryId;
        Price = product.Price.Amount;
        Cost = product.Cost.Amount;
        Stock = product.Stock;
        MinStock = product.MinStock;
        IsActive = product.IsActive;
        OnPropertyChanged(nameof(IsEditing));
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;

        var result = IsEditing
            ? await _productService.UpdateAsync(new UpdateProductRequest
            {
                Id = _editId!.Value,
                Name = Name,
                Sku = Sku,
                Barcode = Barcode,
                CategoryId = SelectedCategoryId,
                Price = Price,
                Cost = Cost,
                Stock = Stock,
                MinStock = MinStock,
                IsActive = IsActive
            })
            : await _productService.CreateAsync(new CreateProductRequest
            {
                Name = Name,
                Sku = Sku,
                Barcode = Barcode,
                CategoryId = SelectedCategoryId,
                Price = Price,
                Cost = Cost,
                Stock = Stock,
                MinStock = MinStock
            });

        if (result.IsFailure)
        {
            ErrorMessage = result.ErrorMessage;
            return;
        }

        _navigation.NavigateTo<ProductListViewModel>();
    }

    [RelayCommand]
    private void Cancel() => _navigation.NavigateTo<ProductListViewModel>();
}
