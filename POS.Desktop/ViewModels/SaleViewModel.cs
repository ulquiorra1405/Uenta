using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Application.Products;
using POS.Application.Sales;
using POS.Domain.Enums;

namespace POS.Desktop.ViewModels;

/// <summary>
/// Línea del carrito. Notifica cambios de cantidad/descuento para que el
/// ViewModel principal recalcule totales.
/// </summary>
public partial class CartLineViewModel : ObservableObject
{
    public long ProductId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Sku { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal Stock { get; init; }

    [ObservableProperty]
    private decimal _quantity = 1;

    [ObservableProperty]
    private decimal _lineDiscount;

    [ObservableProperty]
    private bool _isDiscountOpen;

    [ObservableProperty]
    private string _discountPercentText = string.Empty;

    /// <summary>Total de la línea: (precio × cantidad) − descuento.</summary>
    public decimal LineTotal => Math.Round(UnitPrice * Quantity - LineDiscount, 2, MidpointRounding.AwayFromZero);

    /// <summary>Aviso no bloqueante: se vende más de lo que hay (decisión P3).</summary>
    public bool LowStock => Quantity > Stock;

    /// <summary>Se dispara cuando cambia cantidad o descuento (para recalcular totales).</summary>
    public event Action? Changed;

    partial void OnQuantityChanged(decimal value)
    {
        OnPropertyChanged(nameof(LineTotal));
        OnPropertyChanged(nameof(LowStock));
        Changed?.Invoke();
    }

    partial void OnLineDiscountChanged(decimal value)
    {
        OnPropertyChanged(nameof(LineTotal));
        Changed?.Invoke();
    }

    partial void OnIsDiscountOpenChanged(bool value)
    {
        if (value && string.IsNullOrEmpty(DiscountPercentText))
            DiscountPercentText = "0";
    }
}

/// <summary>Chip de filtro de categoría ("TODOS" + una por categoría).</summary>
public partial class CategoryFilterItem : ObservableObject
{
    public CategoryDto? Category { get; init; }
    public string Name { get; init; } = string.Empty;

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>
/// Pantalla de venta (Fase 1). Catálogo a la izquierda, carrito + cobro a la derecha.
/// Flujo escáner: el foco vive en el buscador; Enter agrega y vuelve el foco.
/// </summary>
public partial class SaleViewModel : ViewModelBase
{
    private readonly ProductService _productService;
    private readonly CategoryService _categoryService;
    private readonly SaleService _saleService;
    private CancellationTokenSource? _debounceCts;

    /// <summary>Usuario temporal: aún no hay login (Fase 1). Se reemplaza con el usuario real.</summary>
    private const long DemoUserId = 1;

    public SaleViewModel(ProductService productService, CategoryService categoryService, SaleService saleService)
    {
        _productService = productService;
        _categoryService = categoryService;
        _saleService = saleService;
    }

    /// <summary>La vista se suscribe para devolver el foco al buscador (loop de escaneo).</summary>
    public event Action? FocusSearchRequested;

    // ─────────────────────────── Catálogo ───────────────────────────

    public ObservableCollection<ProductDto> Products { get; } = [];
    public ObservableCollection<CategoryFilterItem> CategoryFilters { get; } = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isCatalogBusy;

    [ObservableProperty]
    private string _catalogStatus = string.Empty;

    partial void OnSearchTextChanged(string value) => ScheduleSearch();

    [RelayCommand]
    private void SelectCategory(CategoryFilterItem? filter)
    {
        if (filter is null) return;

        foreach (var f in CategoryFilters)
            f.IsSelected = f == filter;

        _ = LoadProductsAsync();
    }

    private void ScheduleSearch()
    {
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        _ = Task.Run(async () =>
        {
            await Task.Delay(250, token);
            if (!token.IsCancellationRequested)
                await LoadProductsAsync();
        });
    }

    private async Task LoadProductsAsync()
    {
        try
        {
            IsCatalogBusy = true;
            var all = await _productService.SearchAsync(SearchText.Trim());
            Products.Clear();

            var selected = CategoryFilters.FirstOrDefault(f => f.IsSelected);
            foreach (var p in all)
            {
                if (selected?.Category is null || p.CategoryId == selected.Category.Id)
                    Products.Add(p);
            }

            CatalogStatus = $"{Products.Count} producto(s)";
        }
        catch (Exception ex)
        {
            CatalogStatus = $"Error: {ex.Message}";
        }
        finally
        {
            IsCatalogBusy = false;
        }
    }

    /// <summary>Flujo escáner/búsqueda: Enter agrega si el texto es SKU o código de barras exacto.</summary>
    [RelayCommand]
    private void AddFromSearch()
    {
        var term = SearchText.Trim();
        if (term.Length == 0) return;

        var match = Products.FirstOrDefault(p =>
            (!string.IsNullOrEmpty(p.Barcode) && string.Equals(p.Barcode, term, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrEmpty(p.Sku) && string.Equals(p.Sku, term, StringComparison.OrdinalIgnoreCase)));

        if (match is not null)
        {
            AddProduct(match);
            SearchText = string.Empty;
            FocusSearchRequested?.Invoke();
        }
    }

    // ─────────────────────────── Carrito ───────────────────────────

    public ObservableCollection<CartLineViewModel> CartLines { get; } = [];

    [ObservableProperty]
    private decimal _subtotal;

    [ObservableProperty]
    private decimal _itbis;

    [ObservableProperty]
    private decimal _globalDiscount;

    [ObservableProperty]
    private decimal _total;

    [ObservableProperty]
    private bool _hasLowStockWarnings;

    partial void OnGlobalDiscountChanged(decimal value) => RecalculateTotals();

    [RelayCommand]
    private void AddProduct(ProductDto? product)
    {
        if (product is null) return;

        var existing = CartLines.FirstOrDefault(l => l.ProductId == product.Id);
        if (existing is not null)
        {
            existing.Quantity += 1;
        }
        else
        {
            var line = new CartLineViewModel
            {
                ProductId = product.Id,
                Name = product.Name,
                Sku = product.Sku,
                UnitPrice = product.Price.Amount,
                Stock = product.Stock
            };
            line.Changed += RecalculateTotals;
            CartLines.Add(line);
        }

        RecalculateTotals();
    }

    [RelayCommand]
    private void IncreaseQuantity(CartLineViewModel? line)
    {
        if (line is null) return;
        line.Quantity += 1;
    }

    [RelayCommand]
    private void DecreaseQuantity(CartLineViewModel? line)
    {
        if (line is null || line.Quantity <= 1) return;
        line.Quantity -= 1;
    }

    [RelayCommand]
    private void RemoveLine(CartLineViewModel? line)
    {
        if (line is null) return;
        line.Changed -= RecalculateTotals;
        CartLines.Remove(line);
        RecalculateTotals();
    }

    [RelayCommand]
    private void ToggleLineDiscount(CartLineViewModel? line)
    {
        if (line is null) return;
        line.IsDiscountOpen = !line.IsDiscountOpen;
    }

    [RelayCommand]
    private void ApplyLineDiscount(CartLineViewModel? line)
    {
        if (line is null) return;

        if (decimal.TryParse(line.DiscountPercentText, NumberStyles.Number, CultureInfo.InvariantCulture, out var pct))
        {
            pct = Math.Clamp(pct, 0, 100);
            var discount = Math.Round(line.UnitPrice * line.Quantity * pct / 100m, 2, MidpointRounding.AwayFromZero);

            // El descuento no puede superar el total de la línea.
            line.LineDiscount = Math.Min(discount, line.UnitPrice * line.Quantity);
        }

        line.IsDiscountOpen = false;
        RecalculateTotals();
    }

    private void RecalculateTotals()
    {
        Subtotal = CartLines.Sum(l => l.LineTotal);
        var total = Math.Round(Subtotal - GlobalDiscount, 2, MidpointRounding.AwayFromZero);
        if (total < 0) total = 0;
        Total = total;

        // ITBIS 18% incluido en el precio (retail RD): total → base imponible + ITBIS.
        Itbis = Math.Round(total * SaleService.ItbisRate / (1 + SaleService.ItbisRate), 2, MidpointRounding.AwayFromZero);

        HasLowStockWarnings = CartLines.Any(l => l.LowStock);
    }

    private bool CanCobrar() => CartLines.Count > 0 && Total > 0 && !IsPaymentOpen;

    // ─────────────────────────── Cobro ───────────────────────────

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CobrarCommand))]
    private bool _isPaymentOpen;

    [ObservableProperty]
    private PaymentMethod _selectedMethod = PaymentMethod.Cash;

    [ObservableProperty]
    private string _receivedText = string.Empty;

    [ObservableProperty]
    private decimal _changeAmount;

    [ObservableProperty]
    private bool _isMixed;

    [ObservableProperty]
    private string _mixedCashText = string.Empty;

    [ObservableProperty]
    private PaymentMethod _mixedRemainderMethod = PaymentMethod.Card;

    [ObservableProperty]
    private string? _paymentError;

    [ObservableProperty]
    private bool _isProcessing;

    private PaymentMethod _lastMethod = PaymentMethod.Cash;

    partial void OnReceivedTextChanged(string value) => UpdateChange();

    partial void OnMixedCashTextChanged(string value) => UpdateChange();

    partial void OnIsMixedChanged(bool value) => UpdateChange();

    private decimal ParseAmount(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        var normalized = text.Replace(',', '.').Trim();
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    private void UpdateChange()
    {
        var received = IsMixed ? ParseAmount(MixedCashText) : ParseAmount(ReceivedText);
        ChangeAmount = received > Total ? Math.Round(received - Total, 2, MidpointRounding.AwayFromZero) : 0;
    }

    [RelayCommand]
    private void Cobrar()
    {
        PaymentError = null;
        SelectedMethod = _lastMethod;
        IsMixed = false;
        ReceivedText = _lastMethod == PaymentMethod.Cash ? Total.ToString("N2") : string.Empty;
        MixedCashText = string.Empty;
        ChangeAmount = 0;
        IsPaymentOpen = true;
    }

    [RelayCommand]
    private void ClosePayment() => IsPaymentOpen = false;

    [RelayCommand]
    private void SetMethod(PaymentMethod method)
    {
        PaymentError = null;
        SelectedMethod = method;
        IsMixed = false;
        ReceivedText = method == PaymentMethod.Cash ? Total.ToString("N2") : string.Empty;
        ChangeAmount = 0;
        IsPaymentOpen = true;
        UpdateChange();
    }

    [RelayCommand]
    private void OpenMixed()
    {
        IsMixed = true;
        SelectedMethod = PaymentMethod.Cash;
        MixedCashText = string.Empty;
        UpdateChange();
    }

    [RelayCommand]
    private void SetExactAmount()
    {
        ReceivedText = Total.ToString("N2");
        UpdateChange();
    }

    [RelayCommand]
    private async Task ConfirmPaymentAsync()
    {
        if (IsProcessing) return;
        PaymentError = null;

        var request = new CreateSaleRequest
        {
            UserId = DemoUserId,
            CustomerId = null,
            CashSessionId = null,
            GlobalDiscount = GlobalDiscount,
            Items = CartLines.Select(l => new SaleItemRequest
            {
                ProductId = l.ProductId,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                LineDiscount = l.LineDiscount
            }).ToList()
        };

        if (IsMixed)
        {
            var cash = ParseAmount(MixedCashText);
            var remainder = Math.Round(Total - cash, 2, MidpointRounding.AwayFromZero);
            if (cash <= 0 || remainder < 0)
            {
                PaymentError = "Ingrese un monto en efectivo válido.";
                return;
            }
            if (remainder > 0)
            {
                request.Payments.Add(new PaymentRequest { Method = PaymentMethod.Cash, Amount = cash });
                request.Payments.Add(new PaymentRequest { Method = MixedRemainderMethod, Amount = remainder });
            }
            else
            {
                request.Payments.Add(new PaymentRequest { Method = PaymentMethod.Cash, Amount = cash });
            }
        }
        else if (SelectedMethod == PaymentMethod.Cash)
        {
            var received = ParseAmount(ReceivedText);
            if (received < Total)
            {
                PaymentError = $"El efectivo recibido es insuficiente. Faltan RD$ {(Total - received):N2}.";
                return;
            }
            request.Payments.Add(new PaymentRequest { Method = PaymentMethod.Cash, Amount = received });
        }
        else
        {
            request.Payments.Add(new PaymentRequest { Method = SelectedMethod, Amount = Total });
        }

        IsProcessing = true;
        try
        {
            var result = await _saleService.CreateSaleAsync(request);
            if (!result.IsSuccess)
            {
                PaymentError = result.ErrorMessage;
                return;
            }

            _lastMethod = SelectedMethod;
            LastSale = result.Value;
            IsPaymentOpen = false;
            IsResultOpen = true;
        }
        finally
        {
            IsProcessing = false;
        }
    }

    // ─────────────────────────── Resultado ───────────────────────────

    [ObservableProperty]
    private SaleDto? _lastSale;

    [ObservableProperty]
    private bool _isResultOpen;

    [RelayCommand]
    private void NewSale()
    {
        foreach (var line in CartLines)
            line.Changed -= RecalculateTotals;
        CartLines.Clear();

        GlobalDiscount = 0;
        SearchText = string.Empty;
        LastSale = null;
        IsResultOpen = false;
        RecalculateTotals();
        FocusSearchRequested?.Invoke();
    }

    public override async Task OnNavigatedToAsync()
    {
        await LoadCategoriesAsync();
        await LoadProductsAsync();
    }

    private async Task LoadCategoriesAsync()
    {
        try
        {
            var categories = await _categoryService.GetAllAsync();
            CategoryFilters.Clear();
            CategoryFilters.Add(new CategoryFilterItem { Name = "TODOS", IsSelected = true });
            foreach (var c in categories)
                CategoryFilters.Add(new CategoryFilterItem { Category = c, Name = c.Name });
        }
        catch { /* el status del catálogo ya avisa */ }
    }

    /// <summary>F2: devuelve el foco al buscador (también se dispara tras agregar).</summary>
    [RelayCommand]
    private void FocusSearch() => FocusSearchRequested?.Invoke();

    /// <summary>Esc: cierra el modal de cobro si está abierto.</summary>
    [RelayCommand]
    private void CancelOverlay()
    {
        if (IsPaymentOpen && !IsProcessing)
            IsPaymentOpen = false;
    }
}
