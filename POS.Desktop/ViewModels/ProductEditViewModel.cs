using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Application.Products;

namespace POS.Desktop.ViewModels;

/// <summary>
/// Editor de producto (popup overlay, no pantalla de navegación).
/// El padre (ProductListViewModel) lo posee; al guardar/cancelar dispara
/// <see cref="CloseRequested"/> y el padre cierra el popup y refresca la lista.
/// </summary>
public partial class ProductEditViewModel : ViewModelBase
{
    private readonly ProductService _productService;
    private readonly CategoryService _categoryService;

    /// <summary>El padre lo escucha para cerrar el popup tras guardar o cancelar.</summary>
    public event EventHandler? CloseRequested;

    public ProductEditViewModel(ProductService productService, CategoryService categoryService)
    {
        _productService = productService;
        _categoryService = categoryService;
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

    // ── Nueva categoría rápida (inline en la ficha) ──
    [ObservableProperty]
    private bool _isNewCategoryOpen;

    [ObservableProperty]
    private string _newCategoryName = string.Empty;

    public ObservableCollection<CategoryDto> Categories { get; } = [];

    /// <summary>Null = crear; distinto de null = editar.</summary>
    private long? _editId;

    public bool IsEditing => _editId is not null;

    /// <summary>Margen en vivo (precio − costo), solo si el precio supera el costo.</summary>
    public decimal MarginAmount => Price - Cost;

    /// <summary>Margen % sobre el precio de venta (0 si precio ≤ 0).</summary>
    public decimal MarginPercent => Price > 0 ? (Price - Cost) / Price * 100m : 0;

    public bool HasMargin => Price > Cost;

    partial void OnPriceChanged(decimal value)
    {
        OnPropertyChanged(nameof(MarginAmount));
        OnPropertyChanged(nameof(MarginPercent));
        OnPropertyChanged(nameof(HasMargin));
    }

    partial void OnCostChanged(decimal value)
    {
        OnPropertyChanged(nameof(MarginAmount));
        OnPropertyChanged(nameof(MarginPercent));
        OnPropertyChanged(nameof(HasMargin));
    }

    /// <summary>Prepara el popup para CREAR un producto nuevo (formulario vacío).</summary>
    public void LoadForNew()
    {
        _editId = null;
        Title = "Nuevo producto";
        Name = string.Empty;
        Sku = null;
        Barcode = null;
        SelectedCategoryId = null;
        Price = 0;
        Cost = 0;
        Stock = 0;
        MinStock = 0;
        IsActive = true;
        ErrorMessage = null;
        IsNewCategoryOpen = false;
        NewCategoryName = string.Empty;
        OnPropertyChanged(nameof(IsEditing));
    }

    /// <summary>Prepara el popup para EDITAR un producto existente.</summary>
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
        ErrorMessage = null;
        IsNewCategoryOpen = false;
        NewCategoryName = string.Empty;
        OnPropertyChanged(nameof(IsEditing));
    }

    /// <summary>Carga una copia como NUEVO producto (regla P9, duplicar y editar).</summary>
    public void LoadAsNew(ProductDto copy)
    {
        _editId = null;
        Title = $"Nuevo producto — {copy.Name}";
        Name = copy.Name;
        Sku = copy.Sku;
        Barcode = copy.Barcode;
        SelectedCategoryId = copy.CategoryId;
        Price = copy.Price.Amount;
        Cost = copy.Cost.Amount;
        Stock = copy.Stock;
        MinStock = copy.MinStock;
        IsActive = copy.IsActive;
        ErrorMessage = null;
        IsNewCategoryOpen = false;
        NewCategoryName = string.Empty;
        OnPropertyChanged(nameof(IsEditing));
    }

    /// <summary>Carga las categorías del popup (activas + la ya asignada aunque esté inactiva).</summary>
    public async Task LoadCategoriesAsync()
    {
        try
        {
            var categories = await _categoryService.GetAllAsync();
            Categories.Clear();
            foreach (var c in categories)
            {
                if (c.IsActive || c.Id == SelectedCategoryId)
                    Categories.Add(c);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error al cargar categorías: {ex.Message}";
        }
    }

    private bool CanDuplicate() => IsEditing;

    [RelayCommand(CanExecute = nameof(CanDuplicate))]
    private void Duplicate()
    {
        // La copia refleja el formulario ACTUAL (lo que ve el usuario), sin persistir nada.
        var source = new ProductDto
        {
            Name = Name,
            Sku = Sku,
            Barcode = Barcode,
            CategoryId = SelectedCategoryId,
            Price = new POS.Domain.ValueObjects.Money(Price),
            Cost = new POS.Domain.ValueObjects.Money(Cost),
            Stock = Stock,
            MinStock = MinStock,
            IsActive = IsActive
        };

        var copy = _productService.CreateDuplicatePreview(source);
        LoadAsNew(copy); // reconfigura el mismo popup en modo "nuevo" con la copia
    }

    [RelayCommand]
    private void ToggleNewCategory() => IsNewCategoryOpen = !IsNewCategoryOpen;

    [RelayCommand]
    private async Task CreateCategoryAsync()
    {
        var result = await _categoryService.CreateAsync(NewCategoryName);
        if (result.IsFailure)
        {
            ErrorMessage = result.ErrorMessage;
            return;
        }

        NewCategoryName = string.Empty;
        IsNewCategoryOpen = false;
        ErrorMessage = null;

        // Refresca la lista y selecciona la categoría recién creada.
        await OnNavigatedToAsync();
        SelectedCategoryId = result.Value!.Id;
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

        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, EventArgs.Empty);
}