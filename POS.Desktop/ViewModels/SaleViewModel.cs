using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using POS.Application.Abstractions;
using POS.Application.Cash;
using POS.Application.Customers;
using POS.Application.Products;
using POS.Application.Receipts;
using POS.Application.Sales;
using POS.Application.Settings;
using POS.Domain.Enums;
using POS.Infrastructure.Services;

namespace POS.Desktop.ViewModels;

/// <summary>Tipo de descuento de línea: porcentaje (dinámico, sigue a la cantidad) o monto fijo.</summary>
public enum LineDiscountMode { Percent, Amount }

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
    private LineDiscountMode _discountMode = LineDiscountMode.Percent;

    /// <summary>% persistido del descuento (0 = sin descuento por %). Dinámico: recalcula con la cantidad.</summary>
    [ObservableProperty]
    private decimal _discountPercent;

    /// <summary>Monto fijo en RD$ (0 = sin descuento por monto). Promesa literal: no cambia con la cantidad.</summary>
    [ObservableProperty]
    private decimal _fixedDiscount;

    [ObservableProperty]
    private bool _isDiscountOpen;

    /// <summary>Valor escrito en el panel de descuento (se interpreta según el modo).</summary>
    [ObservableProperty]
    private string _discountInputText = string.Empty;

    /// <summary>Total bruto de la línea (precio × cantidad, sin descuento).</summary>
    public decimal LineGross => CartCalculator.LineGross(UnitPrice, Quantity);

    /// <summary>
    /// Monto efectivo de descuento: en % sigue a la cantidad (10 items → 500, 5 → 250);
    /// en monto fijo es una promesa literal con tope en el total de la línea.
    /// </summary>
    public decimal LineDiscount
    {
        get
        {
            if (DiscountMode == LineDiscountMode.Percent)
                return CartCalculator.LineDiscountByPercent(LineGross, DiscountPercent);
            return CartCalculator.LineDiscountByAmount(LineGross, FixedDiscount);
        }
    }

    public decimal LineTotal => CartCalculator.LineTotal(UnitPrice, Quantity, LineDiscount);

    public bool HasDiscount => LineDiscount > 0;

    /// <summary>Badge del descuento: "-RD$ 250.00 (5%)" en %, "-RD$ 30.00" en fijo.</summary>
    public string DiscountBadgeText => HasDiscount
        ? DiscountMode == LineDiscountMode.Percent
            ? $"-RD$ {LineDiscount:N2} ({DiscountPercent:0.##}%)"
            : $"-RD$ {LineDiscount:N2}"
        : string.Empty;

    /// <summary>Preview en vivo del panel: "-RD$ 4.00" según el input actual (sin aplicar).</summary>
    public string DiscountPreviewText { get; private set; } = string.Empty;

    /// <summary>Aviso no bloqueante: se vende más de lo que hay (decisión P3).</summary>
    public bool LowStock => Quantity > Stock;

    /// <summary>Se dispara cuando cambia cantidad o descuento (para recalcular totales).</summary>
    public event Action? Changed;

    [RelayCommand]
    private void SetDiscountMode(LineDiscountMode mode)
    {
        if (DiscountMode != mode)
        {
            DiscountMode = mode;
            // Al cambiar el modo, el input se re-interpreta: el preview se actualiza solo.
        }
    }

    partial void OnQuantityChanged(decimal value)
    {
        UpdateDiscountDerived();
        OnPropertyChanged(nameof(LowStock));
        UpdatePreview();
    }

    partial void OnDiscountModeChanged(LineDiscountMode value)
    {
        UpdateDiscountDerived();
        UpdatePreview();
    }

    partial void OnDiscountPercentChanged(decimal value) => UpdateDiscountDerived();

    partial void OnFixedDiscountChanged(decimal value) => UpdateDiscountDerived();

    partial void OnDiscountInputTextChanged(string value) => UpdatePreview();

    partial void OnIsDiscountOpenChanged(bool value)
    {
        if (value)
        {
            // Pre-cargar el estado real al reabrir: modo + valor aplicados (nada oculto).
            DiscountInputText = DiscountMode == LineDiscountMode.Percent
                ? DiscountPercent > 0
                    ? DiscountPercent.ToString("0.##", CultureInfo.InvariantCulture)
                    : string.Empty
                : FixedDiscount > 0
                    ? FixedDiscount.ToString("0.##", CultureInfo.InvariantCulture)
                    : string.Empty;
        }
        UpdatePreview();
    }

    private void UpdateDiscountDerived()
    {
        OnPropertyChanged(nameof(LineDiscount));
        OnPropertyChanged(nameof(LineTotal));
        OnPropertyChanged(nameof(HasDiscount));
        OnPropertyChanged(nameof(DiscountBadgeText));
        Changed?.Invoke();
    }

    private void UpdatePreview()
    {
        if (decimal.TryParse(DiscountInputText, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) && v > 0)
        {
            if (DiscountMode == LineDiscountMode.Percent)
            {
                var d = CartCalculator.LineDiscountByPercent(LineGross, v);
                DiscountPreviewText = $"-RD$ {d:N2}";
            }
            else
            {
                var d = CartCalculator.LineDiscountByAmount(LineGross, v);
                DiscountPreviewText = $"-RD$ {d:N2}";
            }
        }
        else
        {
            DiscountPreviewText = string.Empty;
        }
        OnPropertyChanged(nameof(DiscountPreviewText));
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

/// <summary>Opción de cliente del selector de venta (null = anónimo).</summary>
public record CustomerOption(long? Id, string Label)
{
    public static CustomerOption Anonymous { get; } = new(null, "Anónimo");

    /// <summary>El ToString alimenta el Name UIA del ComboBox (accesibilidad/tests).</summary>
    public override string ToString() => Label;
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
    private readonly IReceiptPrinter _receiptPrinter;
    private readonly ReceiptPdfGenerator _pdfGenerator;
    private readonly SettingsService _settingsService;
    private readonly ICurrentSession _session;
    private readonly CashSessionService _cashService;
    private readonly CashSessionTracker _cashTracker;
    private readonly CustomerService _customerService;
    private CancellationTokenSource? _debounceCts;

    public SaleViewModel(
        ProductService productService,
        CategoryService categoryService,
        SaleService saleService,
        IReceiptPrinter receiptPrinter,
        ReceiptPdfGenerator pdfGenerator,
        SettingsService settingsService,
        ICurrentSession session,
        CashSessionService cashService,
        CashSessionTracker cashTracker,
        CustomerService customerService)
    {
        _productService = productService;
        _categoryService = categoryService;
        _saleService = saleService;
        _receiptPrinter = receiptPrinter;
        _pdfGenerator = pdfGenerator;
        _settingsService = settingsService;
        _session = session;
        _cashService = cashService;
        _cashTracker = cashTracker;
        _customerService = customerService;
        _cashTracker.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(CashSessionTracker.HasOpen) or nameof(CashSessionTracker.Current))
            {
                OnPropertyChanged(nameof(CanStartPayment));
                CobrarCommand.NotifyCanExecuteChanged();
                SetMethodCommand.NotifyCanExecuteChanged();
                OpenMixedCommand.NotifyCanExecuteChanged();
            }
        };
    }

    /// <summary>La vista se suscribe para devolver el foco al buscador (loop de escaneo).</summary>
    public event Action? FocusSearchRequested;

    // ─────────────────────────── Cliente de la venta (P4.1) ───────────────────────────

    /// <summary>Clientes disponibles para asociar a la venta (primero: Anónimo).</summary>
    public ObservableCollection<CustomerOption> CustomerOptions { get; } = [CustomerOption.Anonymous];

    /// <summary>Cliente seleccionado (null = Anónimo).</summary>
    [ObservableProperty]
    private CustomerOption? _selectedCustomer;

    /// <summary>Etiqueta del cliente actual para el encabezado del ticket.</summary>
    public string CustomerLabel => SelectedCustomer?.Label ?? "Anónimo";

    partial void OnSelectedCustomerChanged(CustomerOption? value)
    {
        OnPropertyChanged(nameof(CustomerLabel));
    }

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

        var results = await _productService.SearchActiveAsync(term, token);
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
            var all = await _productService.SearchActiveAsync(CatalogSearchText.Trim());
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

    /// <summary>True cuando el descuento global (monto fijo) supera el subtotal: aviso + bloquear COBRAR.</summary>
    [ObservableProperty]
    private bool _globalDiscountExceedsSubtotal;

    [ObservableProperty]
    private decimal _total;

    [ObservableProperty]
    private bool _hasLowStockWarnings;

    partial void OnGlobalDiscountChanged(decimal value)
    {
        // Descuento global = monto fijo prometido por el cajero. Nunca se muta solo;
        // si es negativo, se bloquea (un -5 inflaría el total como "recargo" accidental).
        if (value < 0)
        {
            GlobalDiscount = 0; // re-set con 0 (no negativo) → recalcula normal
            return;
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

        if (decimal.TryParse(line.DiscountInputText, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            if (line.DiscountMode == LineDiscountMode.Percent)
            {
                // % persistente: el descuento sigue a la cantidad (10 items → 500, 5 → 250).
                line.DiscountPercent = Math.Clamp(value, 0, 100);
            }
            else
            {
                // Monto fijo: promesa literal (el clamp al total de la línea vive en LineDiscount).
                line.FixedDiscount = Math.Max(value, 0);
            }
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
            Subtotal = CartLines.Sum(l => l.LineTotal);

            // Totales y desglose ITBIS: misma fuente de verdad que SaleService (CartCalculator),
            // así el preview del ticket coincide por construcción con la venta persistida.
            var totals = CartCalculator.ComputeTotals(Subtotal, GlobalDiscount);
            GlobalDiscountExceedsSubtotal = totals.DiscountExceedsSubtotal;
            Total = totals.Total;
            Itbis = totals.Itbis;

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
    /// sin modal abierto, sin línea pendiente sin resolver (regla 3.2) y con CAJA ABIERTA (P2.2).</summary>
    private bool CanStartPayment() => CartLines.Count > 0 && Total > 0 && !IsPaymentOpen && !HasPendingEntry
        && !GlobalDiscountExceedsSubtotal && _cashTracker.HasOpen;

    /// <summary>Elegir método de pago (chips TARJETA/TRANSFERENCIA/MIXTO): mismo bloqueo que cobrar,
    /// pero permitido dentro del modal (ahí IsPaymentOpen ya es true).</summary>
    private bool CanChooseMethod() => CartLines.Count > 0 && Total > 0 && !HasPendingEntry
        && !GlobalDiscountExceedsSubtotal && _cashTracker.HasOpen;

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

        // Regla P2.2: sin caja abierta no se cobra (defensa en profundidad; la UI
        // ya bloquea COBRAR vía CanExecute, esto cubre el race con el cierre).
        if (_cashTracker.Current is null)
        {
            PaymentError = "Abra la caja para cobrar.";
            return;
        }

        var request = new CreateSaleRequest
        {
            UserId = _session.CurrentUserId,
            CustomerId = SelectedCustomer?.Id,
            CashSessionId = _cashTracker.Current?.Id,
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
            await RefreshCashAsync();   // el badge de caja sube el efectivo acumulado (P2.2)
            await AutoPrintAfterSaleAsync();
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

    /// <summary>Impresión en curso (bloquea solo el botón Imprimir, nunca la venta).</summary>
    [ObservableProperty]
    private bool _isPrinting;

    /// <summary>Error de impresión no bloqueante (la venta ya quedó persistida).</summary>
    [ObservableProperty]
    private string? _printError;

    /// <summary>Mensaje informativo de impresión (ej.: "Recibo enviado a X").</summary>
    [ObservableProperty]
    private string? _printStatus;

    /// <summary>¿Imprimir recibo automáticamente al completar la venta? (Ajustes, P1.3).</summary>
    [ObservableProperty]
    private bool _autoPrint = true;

    partial void OnIsPrintingChanged(bool value)
    {
        PrintReceiptCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanPrintReceipt))]
    private async Task PrintReceiptAsync()
    {
        if (LastSale is null) return;
        IsPrinting = true;
        PrintError = null;
        PrintStatus = null;
        try
        {
            await _receiptPrinter.PrintReceiptAsync(LastSale);
            PrintStatus = "Recibo enviado a la impresora.";
        }
        catch (Exception ex)
        {
            // Regla dura: la impresión NUNCA falla la venta. Aviso no bloqueante.
            PrintError = ex.Message;
        }
        finally
        {
            IsPrinting = false;
        }
    }

    private bool CanPrintReceipt() => LastSale is not null && !IsPrinting;

    [RelayCommand]
    private async Task SavePdfAsync()
    {
        if (LastSale is null) return;
        PrintError = null;
        PrintStatus = null;

        var dialog = new SaveFileDialog
        {
            Title = "Guardar recibo como PDF",
            Filter = "PDF (*.pdf)|*.pdf",
            FileName = $"Uenta-recibo-{LastSale.Number}.pdf",
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            var bytes = _pdfGenerator.Generate(LastSale);
            await File.WriteAllBytesAsync(dialog.FileName, bytes);
            PrintStatus = $"Recibo guardado en {dialog.FileName}";
        }
        catch (Exception ex)
        {
            PrintError = ex.Message;
        }
    }

    /// <summary>Impresión automática tras cobrar (Ajustes). Nunca bloquea el flujo.</summary>
    private async Task AutoPrintAfterSaleAsync()
    {
        if (!AutoPrint || LastSale is null) return;
        await PrintReceiptAsync();
    }

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
        PrintError = null;
        PrintStatus = null;
        RecalculateTotals();
        FocusSearchRequested?.Invoke();
    }

    public override async Task OnNavigatedToAsync()
    {
        await RefreshCashAsync();
        await LoadCustomersAsync();
        await LoadCategoriesAsync();
        await LoadProductsAsync();
        try
        {
            AutoPrint = await _settingsService.GetBoolAsync(SettingKeys.AutoPrint, true);
        }
        catch { /* default AutoPrint = true */ }
    }

    /// <summary>Carga los clientes del selector (Anónimo queda primero, P4.1).</summary>
    private async Task LoadCustomersAsync()
    {
        try
        {
            var customers = await _customerService.GetAllAsync();
            CustomerOptions.Clear();
            CustomerOptions.Add(CustomerOption.Anonymous);
            foreach (var c in customers.OrderBy(c => c.Name))
                CustomerOptions.Add(new CustomerOption(c.Id, c.Name));
            SelectedCustomer = CustomerOption.Anonymous;
        }
        catch
        {
            // Sin clientes el selector solo ofrece Anónimo: la venta no se bloquea.
            CustomerOptions.Clear();
            CustomerOptions.Add(CustomerOption.Anonymous);
            SelectedCustomer = CustomerOption.Anonymous;
        }
    }

    /// <summary>Refresca la caja abierta del usuario desde la DB (entrar a venta, abrir/cerrar/retiro).</summary>
    private async Task RefreshCashAsync()
    {
        try
        {
            var session = await _cashService.GetOpenForUserAsync(_session.CurrentUserId);
            _cashTracker.Set(session);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SaleViewModel] RefreshCash: {ex}");
        }
    }

    private async Task LoadCategoriesAsync()
    {
        try
        {
            var categories = await _categoryService.GetAllActiveAsync();
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

    // ─────────────────────────── Caja (P2.2) ───────────────────────────

    // Los comandos de apertura/retiro/cierre viven en MainWindowViewModel (el header
    // es global). Este VM solo refresca el tracker al entrar a venta y bloquea COBRAR
    // cuando no hay caja abierta (CanStartPayment + guard en ConfirmPayment).

    /// <summary>True si el usuario activo tiene permiso para cerrar caja (regla P2.1).</summary>
    public bool CanCloseCash => _session.CurrentUser is { } u && POS.Application.Auth.Permissions.Has(u.Role, POS.Application.Auth.Permissions.CloseCash);
}
