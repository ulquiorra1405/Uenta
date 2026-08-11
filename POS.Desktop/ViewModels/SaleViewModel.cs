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

/// <summary>Item del dropdown de sugerencias de la línea de entrada (modelo B).</summary>
public partial class EntrySuggestionItem : ObservableObject
{
    public ProductDto Product { get; init; } = null!;
    public string Name => Product.Name;
    public decimal Price => Product.Price.Amount;
    public bool LowStock => Product.LowStock;

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

    // ─────────────────────────── Línea de entrada (modelo B) ───────────────────────────

    /// <summary>Texto de la línea de entrada del ticket (código o nombre).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CobrarCommand))]
    private string _searchText = string.Empty;

    /// <summary>Sugerencias del dropdown (búsqueda por nombre; ambiguo → elige el cajero).</summary>
    public ObservableCollection<EntrySuggestionItem> EntrySuggestions { get; } = [];

    [ObservableProperty]
    private bool _isSuggestionsOpen;

    [ObservableProperty]
    private int _selectedSuggestionIndex = -1;

    /// <summary>Línea con texto sin resolver: COBRAR bloqueado + borde warning (regla 3.2).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CobrarCommand), nameof(SetMethodCommand), nameof(OpenMixedCommand))]
    private bool _hasPendingEntry;

    private CancellationTokenSource? _entryDebounceCts;

    partial void OnSearchTextChanged(string value) => ScheduleEntrySearch(value);

    /// <summary>
    /// Debounce 250ms de la línea de entrada. Corre en el hilo UI (async/await captura
    /// el SynchronizationContext) para que las colecciones y comandos sean seguros.
    /// </summary>
    private async void ScheduleEntrySearch(string value)
    {
        _entryDebounceCts?.Cancel();
        var cts = new CancellationTokenSource();
        _entryDebounceCts = cts;
        try
        {
            await Task.Delay(250, cts.Token);
            if (cts.Token.IsCancellationRequested) return;
            await RunEntrySearchAsync(value.Trim(), cts.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            // Nunca dejar la línea en silencio: si la búsqueda falla, queda pendiente y visible.
            IsSuggestionsOpen = false;
            HasPendingEntry = true;
            System.Diagnostics.Debug.WriteLine($"[SaleViewModel] EntrySearch: {ex}");
        }
    }

    private async Task RunEntrySearchAsync(string term, CancellationToken token)
    {
        if (term.Length == 0)
        {
            HasPendingEntry = false;
            IsSuggestionsOpen = false;
            return;
        }

        var results = await _productService.SearchAsync(term, token);
        if (token.IsCancellationRequested) return;

        // Match EXACTO de código (SKU/barcode) → se rellena sola (loop de escáner).
        var exact = results.FirstOrDefault(p =>
            (!string.IsNullOrEmpty(p.Sku) && string.Equals(p.Sku, term, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrEmpty(p.Barcode) && string.Equals(p.Barcode, term, StringComparison.OrdinalIgnoreCase)));

        if (exact is not null && results.Count(r =>
                (!string.IsNullOrEmpty(r.Sku) && string.Equals(r.Sku, term, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(r.Barcode) && string.Equals(r.Barcode, term, StringComparison.OrdinalIgnoreCase))) == 1)
        {
            AddProduct(exact);
            SearchText = string.Empty;
            HasPendingEntry = false;
            IsSuggestionsOpen = false;
            FocusSearchRequested?.Invoke();
            return;
        }

        // Búsqueda por nombre / ambiguo → dropdown de sugerencias (máx 8).
        EntrySuggestions.Clear();
        foreach (var p in results.Take(8))
            EntrySuggestions.Add(new EntrySuggestionItem { Product = p });

        IsSuggestionsOpen = EntrySuggestions.Count > 0;
        SelectedSuggestionIndex = EntrySuggestions.Count > 0 ? 0 : -1;
        SyncSelectedSuggestion();
        HasPendingEntry = true;
    }

    partial void OnSelectedSuggestionIndexChanged(int value) => SyncSelectedSuggestion();

    private void SyncSelectedSuggestion()
    {
        for (var i = 0; i < EntrySuggestions.Count; i++)
            EntrySuggestions[i].IsSelected = i == SelectedSuggestionIndex;
    }

    /// <summary>↓: siguiente sugerencia.</summary>
    [RelayCommand]
    private void SelectNextSuggestion()
    {
        if (EntrySuggestions.Count == 0) return;
        SelectedSuggestionIndex = (SelectedSuggestionIndex + 1) % EntrySuggestions.Count;
    }

    /// <summary>↑: anterior sugerencia.</summary>
    [RelayCommand]
    private void SelectPreviousSuggestion()
    {
        if (EntrySuggestions.Count == 0) return;
        SelectedSuggestionIndex = SelectedSuggestionIndex <= 0 ? EntrySuggestions.Count - 1 : SelectedSuggestionIndex - 1;
    }

    /// <summary>Esc: cierra el dropdown sin seleccionar (la línea queda pendiente).</summary>
    [RelayCommand]
    private void DismissSuggestions() => IsSuggestionsOpen = false;

    /// <summary>
    /// Enter en la línea de entrada: agrega la sugerencia seleccionada, o el match
    /// exacto de código; si no hay match, la línea queda pendiente (bloquea COBRAR).
    /// </summary>
    [RelayCommand]
    private void AddFromSearch()
    {
        var term = SearchText.Trim();
        if (term.Length == 0) return;

        if (IsSuggestionsOpen && SelectedSuggestionIndex >= 0 && SelectedSuggestionIndex < EntrySuggestions.Count)
        {
            var selected = EntrySuggestions[SelectedSuggestionIndex];
            AddProduct(selected.Product);
            ClearEntry();
            return;
        }

        var exact = EntrySuggestions
            .Select(s => s.Product)
            .FirstOrDefault(p =>
                (!string.IsNullOrEmpty(p.Sku) && string.Equals(p.Sku, term, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(p.Barcode) && string.Equals(p.Barcode, term, StringComparison.OrdinalIgnoreCase)));

        if (exact is not null)
        {
            AddProduct(exact);
            ClearEntry();
        }
        // Sin match: no se agrega nada; el texto queda y HasPendingEntry bloquea COBRAR.
    }

    /// <summary>Agrega desde el popup catálogo (Enter con código exacto contra los cargados).</summary>
    [RelayCommand]
    private void AddFromCatalogSearch()
    {
        var term = CatalogSearchText.Trim();
        if (term.Length == 0) return;

        var match = Products.FirstOrDefault(p =>
            (!string.IsNullOrEmpty(p.Barcode) && string.Equals(p.Barcode, term, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrEmpty(p.Sku) && string.Equals(p.Sku, term, StringComparison.OrdinalIgnoreCase)));

        if (match is not null)
        {
            AddProduct(match);
            CatalogSearchText = string.Empty;
            CatalogFocusRequested?.Invoke();
        }
    }

    /// <summary>Click en una sugerencia del dropdown: agrega el producto.</summary>
    [RelayCommand]
    private void AddSuggestion(EntrySuggestionItem? item)
    {
        if (item is null) return;
        AddProduct(item.Product);
        ClearEntry();
    }

    private void ClearEntry()
    {
        SearchText = string.Empty;
        HasPendingEntry = false;
        IsSuggestionsOpen = false;
        FocusSearchRequested?.Invoke();
    }

    // ─────────────────────────── Catálogo (a demanda, popup F2) ───────────────────────────

    public ObservableCollection<ProductDto> Products { get; } = [];
    public ObservableCollection<CategoryFilterItem> CategoryFilters { get; } = [];

    /// <summary>Texto del buscador DENTRO del popup catálogo (independiente de la línea de entrada).</summary>
    [ObservableProperty]
    private string _catalogSearchText = string.Empty;

    [ObservableProperty]
    private bool _isCatalogBusy;

    [ObservableProperty]
    private string _catalogStatus = string.Empty;

    /// <summary>Popup catálogo visual (modelo B): se abre con F2 y agrega sin cerrarse.</summary>
    [ObservableProperty]
    private bool _isCatalogOpen;

    /// <summary>Productos agregados al ticket desde que se abrió el popup (feedback 3.4).</summary>
    [ObservableProperty]
    private int _catalogAdds;

    [RelayCommand]
    private void OpenCatalog()
    {
        CatalogAdds = 0;
        IsCatalogOpen = true;
        CatalogFocusRequested?.Invoke();
    }

    [RelayCommand]
    private void CloseCatalog()
    {
        IsCatalogOpen = false;
        FocusSearchRequested?.Invoke();
    }

    /// <summary>La vista se suscribe para enfocar el buscador del popup catálogo.</summary>
    public event Action? CatalogFocusRequested;

    partial void OnCatalogSearchTextChanged(string value) => ScheduleSearch();

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
        var cts = new CancellationTokenSource();
        _debounceCts = cts;

        _ = RefreshCatalogAsync(cts);
    }

    /// <summary>Debounce del buscador del catálogo; corre en el hilo UI por las colecciones.</summary>
    private async Task RefreshCatalogAsync(CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(250, cts.Token);
            if (cts.Token.IsCancellationRequested) return;
            await LoadProductsAsync();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SaleViewModel] CatalogSearch: {ex}");
        }
    }

    private async Task LoadProductsAsync()
    {
        try
        {
            IsCatalogBusy = true;
            var all = await _productService.SearchAsync(CatalogSearchText.Trim());
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

    // ─────────────────────────── Carrito ───────────────────────────

    public ObservableCollection<CartLineViewModel> CartLines { get; } = [];

    [ObservableProperty]
    private decimal _subtotal;

    [ObservableProperty]
    private decimal _itbis;

    [ObservableProperty]
    private decimal _globalDiscount;

    // % del subtotal que el monto escrito representaba al momento de capturarlo.
    // Permite escalar el descuento global a la baja cuando el ticket se encoge
    // (quitar líneas, con o sin descuento de línea) sin que el monto quede clavado.
    private decimal _globalDiscountPct;

    [ObservableProperty]
    private decimal _total;

    [ObservableProperty]
    private bool _hasLowStockWarnings;

    partial void OnGlobalDiscountChanged(decimal value)
    {
        if (!_isRecalculating)
        {
            // Capturar el % que el monto representa del subtotal actual.
            // Si no hay ticket (Subtotal 0) o el monto es 0, no hay % que conservar.
            _globalDiscountPct = Subtotal > 0 && value > 0
                ? Math.Min(value / Subtotal, 1m)
                : 0m;
        }
        RecalculateTotals();
    }

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

        // Feedback del popup catálogo (3.4): cuenta los agregados de esta apertura.
        if (IsCatalogOpen)
            CatalogAdds++;
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

    private bool _isRecalculating;

    private void RecalculateTotals()
    {
        if (_isRecalculating) return;
        _isRecalculating = true;
        try
        {
            var newSubtotal = CartLines.Sum(l => l.LineTotal);

            // Descuento global: el monto que escribió el cajero captura un % del subtotal
            // (pct = monto / subtotal al momento de escribirlo). Si el ticket SE ENCOGE
            // (se quitan líneas, con o sin descuento de línea), el monto baja proporcional
            // a ese % — el descuento global nunca queda clavado ni se come el total.
            // Nunca sube solo al agregar líneas: el control del monto lo tiene el cajero.
            if (_globalDiscountPct > 0 && newSubtotal < Subtotal)
            {
                var scaled = Math.Round(newSubtotal * _globalDiscountPct, 2, MidpointRounding.AwayFromZero);
                if (scaled < GlobalDiscount)
                    GlobalDiscount = scaled;
            }
            else if (newSubtotal == 0)
            {
                GlobalDiscount = 0;
            }

            Subtotal = newSubtotal;

            // Red de seguridad: nunca descontar más del subtotal.
            if (GlobalDiscount > Subtotal)
                GlobalDiscount = Subtotal;

            var total = Math.Round(Subtotal - GlobalDiscount, 2, MidpointRounding.AwayFromZero);
            if (total < 0) total = 0;
            Total = total;

            // ITBIS 18% incluido en el precio (retail RD): total → base imponible + ITBIS.
            Itbis = Math.Round(total * SaleService.ItbisRate / (1 + SaleService.ItbisRate), 2, MidpointRounding.AwayFromZero);

            HasLowStockWarnings = CartLines.Any(l => l.LowStock);

            // Los comandos de cobro dependen de CartLines/Total: refrescar su CanExecute
            // (agregar desde el catálogo no toca SearchText, así que la notificación
            // automática por propiedad no alcanza).
            CobrarCommand.NotifyCanExecuteChanged();
            SetMethodCommand.NotifyCanExecuteChanged();
            OpenMixedCommand.NotifyCanExecuteChanged();
        }
        finally
        {
            _isRecalculating = false;
        }
    }

    /// <summary>Se puede iniciar el cobro (COBRAR / EFECTIVO / F8): ticket con líneas, total > 0,
    /// sin modal abierto y sin línea pendiente sin resolver (regla 3.2).</summary>
    private bool CanStartPayment() => CartLines.Count > 0 && Total > 0 && !IsPaymentOpen && !HasPendingEntry;

    /// <summary>Elegir método de pago (chips TARJETA/TRANSFERENCIA/MIXTO): mismo bloqueo que cobrar,
    /// pero permitido dentro del modal (ahí IsPaymentOpen ya es true).</summary>
    private bool CanChooseMethod() => CartLines.Count > 0 && Total > 0 && !HasPendingEntry;

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

    [RelayCommand(CanExecute = nameof(CanStartPayment))]
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

    [RelayCommand(CanExecute = nameof(CanChooseMethod))]
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

    [RelayCommand(CanExecute = nameof(CanChooseMethod))]
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
        HasPendingEntry = false;
        IsSuggestionsOpen = false;
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

    /// <summary>Esc: cierra el dropdown de sugerencias; si no, catálogo; si no, modal de cobro.</summary>
    [RelayCommand]
    private void CancelOverlay()
    {
        if (IsSuggestionsOpen)
        {
            DismissSuggestions();
            return;
        }

        if (IsCatalogOpen)
        {
            CloseCatalog();
            return;
        }

        if (IsPaymentOpen && !IsProcessing)
            IsPaymentOpen = false;
    }
}
