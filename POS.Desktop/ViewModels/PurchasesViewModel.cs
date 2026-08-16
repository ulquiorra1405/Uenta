using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Application.Abstractions;
using POS.Application.Auth;
using POS.Application.Products;
using POS.Application.Purchases;

namespace POS.Desktop.ViewModels;

/// <summary>Línea de compra editable (cantidad y costo unitario).</summary>
public partial class PurchaseLineItem : ObservableObject
{
    public long ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public decimal CurrentStock { get; init; }

    [ObservableProperty]
    private decimal _quantity = 1;

    [ObservableProperty]
    private decimal _unitCost;

    public event EventHandler? LineChanged;

    partial void OnQuantityChanged(decimal value) => Notify();
    partial void OnUnitCostChanged(decimal value) => Notify();

    private void Notify()
    {
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(TotalText));
        LineChanged?.Invoke(this, EventArgs.Empty);
    }

    public decimal Total => Quantity * UnitCost;
    public string TotalText => $"RD$ {Total:N2}";

    /// <summary>Notifica totales (se llama al cambiar desde fuera).</summary>
    public void NotifyTotals() => Notify();
}

/// <summary>Fila del historial de compras (P5.2).</summary>
public partial class PurchaseHistoryItem : ObservableObject
{
    public long Number { get; init; }
    public string CreatedAtText { get; init; } = string.Empty;
    public string? UserName { get; init; }
    public string? SupplierName { get; init; }
    public int LineCount { get; init; }
    public decimal Total { get; init; }
    public string TotalText => $"RD$ {Total:N2}";
    public string SupplierText => SupplierName ?? "Sin proveedor";
    public string LinesText => LineCount == 1 ? "1 línea" : $"{LineCount} líneas";
}

/// <summary>
/// Compras y proveedores (P5.2): registrar compras que reponen stock y
/// registran el costo real (promedio ponderado). Solo contado en v1.
/// Accesible a Admin/Supervisor (permiso ManagePurchases).
/// </summary>
public partial class PurchasesViewModel : ViewModelBase
{
    private readonly PurchaseService _purchases;
    private readonly ProductService _products;
    private readonly ICurrentSession _session;

    public ObservableCollection<PurchaseLineItem> PurchaseLines { get; } = [];
    public ObservableCollection<PurchaseHistoryItem> History { get; } = [];
    public ObservableCollection<SupplierDto> Suppliers { get; } = [];

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _formError;

    [ObservableProperty]
    private string? _loadError;

    // ── Proveedor seleccionado ──
    [ObservableProperty]
    private SupplierDto? _selectedSupplier;

    // ── Línea nueva: buscador de producto ──
    [ObservableProperty]
    private string _productSearchText = string.Empty;

    [ObservableProperty]
    private bool _isProductSearchOpen;

    [ObservableProperty]
    private ProductDto? _selectedProduct;

    [ObservableProperty]
    private string _manualQuantityText = "1";

    [ObservableProperty]
    private string _manualCostText = string.Empty;

    // ── Modal nuevo proveedor ──
    [ObservableProperty]
    private bool _isSupplierModalOpen;

    [ObservableProperty]
    private string _newSupplierName = string.Empty;

    [ObservableProperty]
    private string _newSupplierRnc = string.Empty;

    [ObservableProperty]
    private string _newSupplierPhone = string.Empty;

    [ObservableProperty]
    private string? _supplierError;

    [ObservableProperty]
    private string? _supplierMessage;

    [ObservableProperty]
    private bool _isSavingSupplier;

    // ── Resultado ──
    [ObservableProperty]
    private bool _isResultOpen;

    [ObservableProperty]
    private PurchaseDto? _lastPurchase;

    public string ResultSummaryText => LastPurchase is { } p
        ? $"Compra #{p.Number} · {p.CreatedAt:HH:mm} · {p.SupplierName ?? "Sin proveedor"} · {p.Items.Count} líneas"
        : string.Empty;

    public string ResultTotalText => LastPurchase is { } p ? $"RD$ {p.Total.Amount:N2}" : string.Empty;

    public bool CanManagePurchases => _session.CurrentUser is { } u && Permissions.Has(u.Role, Permissions.ManagePurchases);

    public string TotalText => $"RD$ {PurchaseLines.Sum(l => l.Total):N2}";

    public string SelectedProductCostText => SelectedProduct is { } p ? $"Costo actual RD$ {p.Cost.Amount:N2}" : string.Empty;

    public PurchasesViewModel(
        PurchaseService purchases,
        ProductService products,
        ICurrentSession session)
    {
        _purchases = purchases;
        _products = products;
        _session = session;
    }

    public override async Task OnNavigatedToAsync()
    {
        await LoadSuppliersAsync();
        await LoadHistoryAsync();
    }

    private async Task LoadSuppliersAsync()
    {
        try
        {
            var all = await _purchases.GetSuppliersAsync();
            var previous = SelectedSupplier?.Id;
            Suppliers.Clear();
            foreach (var s in all)
                Suppliers.Add(s);
            SelectedSupplier = previous is { } pid ? Suppliers.FirstOrDefault(s => s.Id == pid) : null;
        }
        catch (Exception ex)
        {
            LoadError = $"No se pudieron cargar los proveedores: {ex.Message}";
        }
    }

    private async Task LoadHistoryAsync()
    {
        try
        {
            var recent = await _purchases.GetRecentAsync(20);
            History.Clear();
            foreach (var p in recent)
            {
                History.Add(new PurchaseHistoryItem
                {
                    Number = p.Number,
                    CreatedAtText = p.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                    UserName = p.UserName,
                    SupplierName = p.SupplierName,
                    LineCount = p.Items.Count,
                    Total = p.Total.Amount,
                });
            }
        }
        catch (Exception ex)
        {
            LoadError = $"No se pudo cargar el historial: {ex.Message}";
        }
    }

    // ── Buscar producto del catálogo ──

    private async void ScheduleProductSearch(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            IsProductSearchOpen = false;
            SelectedProduct = null;
            OnPropertyChanged(nameof(SelectedProductCostText));
            return;
        }
        try
        {
            var results = await _products.SearchActiveAsync(value.Trim());
            SelectedProduct = results.FirstOrDefault();
            IsProductSearchOpen = results.Count > 0;
            OnPropertyChanged(nameof(SelectedProductCostText));
            if (SelectedProduct is not null && string.IsNullOrWhiteSpace(ManualCostText))
                ManualCostText = SelectedProduct.Cost.Amount.ToString("0.##");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PurchasesViewModel] ProductSearch: {ex}");
        }
    }

    partial void OnProductSearchTextChanged(string value) => ScheduleProductSearch(value);

    partial void OnSelectedProductChanged(ProductDto? value)
    {
        OnPropertyChanged(nameof(SelectedProductCostText));
        if (value is not null && string.IsNullOrWhiteSpace(ManualCostText))
            ManualCostText = value.Cost.Amount.ToString("0.##");
    }

    /// <summary>Enter en el buscador: agrega el primer match como línea de compra.</summary>
    [RelayCommand]
    private void AddLine()
    {
        if (SelectedProduct is null)
        {
            FormError = "Busque un producto del catálogo para agregarlo.";
            return;
        }

        var qty = ParseQuantity(ManualQuantityText);
        if (qty <= 0)
        {
            FormError = "Ingrese una cantidad válida.";
            return;
        }

        var cost = ParseQuantity(ManualCostText);
        if (cost < 0)
        {
            FormError = "Ingrese un costo unitario válido.";
            return;
        }

        var existing = PurchaseLines.FirstOrDefault(l => l.ProductId == SelectedProduct.Id);
        if (existing is not null)
        {
            existing.Quantity += qty;
            existing.UnitCost = cost;
            existing.NotifyTotals();
        }
        else
        {
            var item = new PurchaseLineItem
            {
                ProductId = SelectedProduct.Id,
                ProductName = SelectedProduct.Name,
                CurrentStock = SelectedProduct.Stock,
                Quantity = qty,
                UnitCost = cost,
            };
            item.LineChanged += (_, _) => OnPropertyChanged(nameof(TotalText));
            PurchaseLines.Add(item);
        }

        ProductSearchText = string.Empty;
        IsProductSearchOpen = false;
        SelectedProduct = null;
        ManualQuantityText = "1";
        ManualCostText = string.Empty;
        FormError = null;
        OnPropertyChanged(nameof(TotalText));
    }

    [RelayCommand]
    private void RemoveLine(PurchaseLineItem? line)
    {
        if (line is null) return;
        PurchaseLines.Remove(line);
        OnPropertyChanged(nameof(TotalText));
    }

    // ── Modal nuevo proveedor ──

    [RelayCommand]
    private void OpenSupplierModal()
    {
        SupplierError = null;
        SupplierMessage = null;
        NewSupplierName = string.Empty;
        NewSupplierRnc = string.Empty;
        NewSupplierPhone = string.Empty;
        IsSupplierModalOpen = true;
    }

    [RelayCommand]
    private void CloseSupplierModal() => IsSupplierModalOpen = false;

    [RelayCommand]
    private async Task SaveSupplierAsync()
    {
        if (IsSavingSupplier) return;
        SupplierError = null;
        SupplierMessage = null;

        if (string.IsNullOrWhiteSpace(NewSupplierName))
        {
            SupplierError = "El nombre del proveedor es obligatorio.";
            return;
        }

        IsSavingSupplier = true;
        try
        {
            var result = await _purchases.CreateSupplierAsync(
                new CreateSupplierRequest(NewSupplierName, NewSupplierRnc, NewSupplierPhone));
            if (!result.IsSuccess)
            {
                SupplierError = result.ErrorMessage;
                return;
            }

            SupplierMessage = $"Proveedor '{result.Value!.Name}' registrado.";
            NewSupplierName = string.Empty;
            NewSupplierRnc = string.Empty;
            NewSupplierPhone = string.Empty;
            await LoadSuppliersAsync();
        }
        finally
        {
            IsSavingSupplier = false;
        }
    }

    // ── Registrar compra ──

    [RelayCommand]
    private async Task ProcessAsync()
    {
        if (IsBusy) return;
        FormError = null;

        if (PurchaseLines.Count == 0)
        {
            FormError = "Agregue al menos un producto a la compra.";
            return;
        }

        if (!CanManagePurchases)
        {
            FormError = "Su rol no permite registrar compras.";
            return;
        }

        var request = new CreatePurchaseRequest
        {
            UserId = _session.CurrentUserId,
            SupplierId = SelectedSupplier?.Id,
            Items = PurchaseLines.Select(l => new CreatePurchaseLineRequest(l.ProductId, l.Quantity, l.UnitCost)).ToList(),
        };

        IsBusy = true;
        try
        {
            var result = await _purchases.CreateAsync(request);
            if (!result.IsSuccess)
            {
                FormError = result.ErrorMessage;
                return;
            }

            LastPurchase = result.Value;
            IsResultOpen = true;
            await LoadHistoryAsync();
        }
        catch (Exception ex)
        {
            FormError = $"No se pudo registrar la compra: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void NewPurchase()
    {
        IsResultOpen = false;
        LastPurchase = null;
        PurchaseLines.Clear();
        ProductSearchText = string.Empty;
        IsProductSearchOpen = false;
        SelectedProduct = null;
        ManualQuantityText = "1";
        ManualCostText = string.Empty;
        FormError = null;
        OnPropertyChanged(nameof(TotalText));
    }

    private static decimal ParseQuantity(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        var normalized = text.Replace(',', '.').Trim();
        return decimal.TryParse(normalized, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
    }
}