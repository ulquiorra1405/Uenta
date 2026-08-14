using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Application.Products;

namespace POS.Desktop.ViewModels;

/// <summary>Fila editable del gestor de categorías (renombrado inline + desactivar/reactivar).</summary>
public partial class CategoryItemViewModel : ObservableObject
{
    public long Id { get; init; }

    public int ProductCount { get; init; }

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private bool _isActive = true;

    public CategoryItemViewModel(CategoryDto dto)
    {
        Id = dto.Id;
        _name = dto.Name;
        _isActive = dto.IsActive;
        ProductCount = dto.ProductCount;
    }
}

public partial class ProductListViewModel : ViewModelBase
{
    private readonly ProductService _productService;
    private readonly CategoryService _categoryService;
    private readonly INavigationService _navigation;

    public ProductListViewModel(
        ProductService productService,
        CategoryService categoryService,
        INavigationService navigation,
        ProductEditViewModel editor)
    {
        _productService = productService;
        _categoryService = categoryService;
        _navigation = navigation;
        Editor = editor;
        Editor.CloseRequested += OnEditorCloseRequested;
    }

    public ObservableCollection<ProductDto> Products { get; } = [];

    public ObservableCollection<CategoryItemViewModel> Categories { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeactivateCommand))]
    [NotifyCanExecuteChangedFor(nameof(ReactivateCommand))]
    private ProductDto? _selectedProduct;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isBusy;

    // ── Gestor de categorías (overlay modal) ──
    [ObservableProperty]
    private bool _isCategoryManagerOpen;

    [ObservableProperty]
    private string _newCategoryName = string.Empty;

    // ── Editor de producto (popup overlay, como el gestor de categorías) ──
    [ObservableProperty]
    private bool _isProductEditorOpen;

    /// <summary>Popup de creación/edición de producto. Lo posee esta vista; no navega.</summary>
    public ProductEditViewModel Editor { get; }

    public override async Task OnNavigatedToAsync() => await LoadAsync();

    [RelayCommand]
    private async Task SearchAsync()
    {
        await LoadAsync();
    }

    [RelayCommand]
    private async Task NewAsync()
    {
        Editor.LoadForNew();
        IsProductEditorOpen = true;
        await Editor.LoadCategoriesAsync();
    }

    private bool CanEditOrDeactivate(ProductDto? product) => product is not null || SelectedProduct is not null;

    private bool CanReactivate(ProductDto? product)
    {
        var target = product ?? SelectedProduct;
        return target is { IsActive: false };
    }

    [RelayCommand(CanExecute = nameof(CanEditOrDeactivate))]
    private async Task EditAsync(ProductDto? product)
    {
        var target = product ?? SelectedProduct;
        if (target is null) return;
        Editor.Load(target);
        IsProductEditorOpen = true;
        await Editor.LoadCategoriesAsync();
    }

    [RelayCommand(CanExecute = nameof(CanEditOrDeactivate))]
    private async Task DeactivateAsync(ProductDto? product)
    {
        var target = product ?? SelectedProduct;
        if (target is null) return;

        var confirm = MessageBox.Show(
            $"¿Desactivar '{target.Name}'?\nSe ocultará del catálogo pero se conservará su historial.",
            "Uenta", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        var result = await _productService.DeactivateAsync(target.Id);
        StatusMessage = result.IsSuccess ? "Producto desactivado." : $"Error: {result.ErrorMessage}";
        await LoadAsync();
    }

    [RelayCommand(CanExecute = nameof(CanReactivate))]
    private async Task ReactivateAsync(ProductDto? product)
    {
        var target = product ?? SelectedProduct;
        if (target is null) return;

        var result = await _productService.ReactivateAsync(target.Id);
        StatusMessage = result.IsSuccess ? "Producto reactivado." : $"Error: {result.ErrorMessage}";
        await LoadAsync();
    }

    [RelayCommand]
    private void OpenCategoryManager()
    {
        IsCategoryManagerOpen = true;
        NewCategoryName = string.Empty;
        _ = LoadCategoriesAsync();
    }

    [RelayCommand]
    private void CloseCategoryManager() => IsCategoryManagerOpen = false;

    /// <summary>El editor avisó que terminó (guardó o canceló): cierra el popup y refresca la lista.</summary>
    private async void OnEditorCloseRequested(object? sender, EventArgs e)
    {
        IsProductEditorOpen = false;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task CreateCategoryAsync()
    {
        var result = await _categoryService.CreateAsync(NewCategoryName);
        if (result.IsFailure)
        {
            StatusMessage = $"Error: {result.ErrorMessage}";
            return;
        }

        NewCategoryName = string.Empty;
        StatusMessage = $"Categoría '{result.Value!.Name}' creada.";
        await LoadCategoriesAsync();
    }

    [RelayCommand]
    private async Task RenameCategoryAsync(CategoryItemViewModel? item)
    {
        if (item is null) return;

        var result = await _categoryService.RenameAsync(item.Id, item.Name);
        StatusMessage = result.IsSuccess ? "Categoría renombrada." : $"Error: {result.ErrorMessage}";
        if (result.IsFailure)
            await LoadCategoriesAsync(); // restaura el nombre original
    }

    [RelayCommand]
    private async Task DeactivateCategoryAsync(CategoryItemViewModel? item)
    {
        if (item is null) return;

        var confirm = MessageBox.Show(
            $"¿Desactivar la categoría '{item.Name}'?\n" +
            (item.ProductCount > 0
                ? $"{item.ProductCount} producto(s) se ocultarán de la navegación por categoría en venta.\n"
                : string.Empty) +
            "Los productos siguen activos y el historial se conserva.",
            "Uenta", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        var result = await _categoryService.DeactivateAsync(item.Id);
        StatusMessage = result.IsSuccess ? "Categoría desactivada." : $"Error: {result.ErrorMessage}";
        await LoadCategoriesAsync();
    }

    [RelayCommand]
    private async Task ReactivateCategoryAsync(CategoryItemViewModel? item)
    {
        if (item is null) return;

        var result = await _categoryService.ReactivateAsync(item.Id);
        StatusMessage = result.IsSuccess ? "Categoría reactivada." : $"Error: {result.ErrorMessage}";
        await LoadCategoriesAsync();
    }

    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var products = await _productService.SearchAllAsync(SearchText);
            Products.Clear();
            foreach (var p in products)
                Products.Add(p);

            StatusMessage = $"{products.Count} producto(s)";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al cargar productos: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadCategoriesAsync()
    {
        try
        {
            var categories = await _categoryService.GetAllAsync();
            Categories.Clear();
            foreach (var c in categories)
                Categories.Add(new CategoryItemViewModel(c));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al cargar categorías: {ex.Message}";
        }
    }
}