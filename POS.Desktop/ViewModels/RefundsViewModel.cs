using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Application.Abstractions;
using POS.Application.Auth;
using POS.Application.Cash;
using POS.Application.Products;
using POS.Application.Refunds;
using POS.Domain.Enums;

namespace POS.Desktop.ViewModels;

/// <summary>Línea de devolución editable (cantidad tope = disponible).</summary>
public partial class RefundLineItem : ObservableObject
{
    public long ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public decimal UnitPrice { get; init; }

    /// <summary>Máximo devolvible de esta línea (vendido − ya devuelto; o stock para sin recibo).</summary>
    public decimal MaxQuantity { get; init; }

    [ObservableProperty]
    private decimal _quantity = 1;

    public event EventHandler? QuantityChanged;

    partial void OnQuantityChanged(decimal value)
    {
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(TotalText));
        QuantityChanged?.Invoke(this, EventArgs.Empty);
    }

    public decimal Total => UnitPrice * Quantity;
    public string TotalText => $"RD$ {Total:N2}";

    /// <summary>Notifica totales (se llama al cambiar cantidad desde fuera).</summary>
    public void NotifyTotals()
    {
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(TotalText));
        QuantityChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>Fila del historial de devoluciones (P5.1).</summary>
public partial class RefundHistoryItem : ObservableObject
{
    public long Number { get; init; }
    public string CreatedAtText { get; init; } = string.Empty;
    public string? UserName { get; init; }
    public long? OriginalSaleNumber { get; init; }
    public string KindText => OriginalSaleNumber is { } n ? $"Recibo #{n}" : "Sin recibo";
    public string Reason { get; init; } = string.Empty;
    public decimal Total { get; init; }
    public string TotalText => $"RD$ {Total:N2}";
}

/// <summary>
/// Devoluciones / notas de crédito (P5.1). Dos caminos:
/// - Con recibo: se busca la venta por número y se devuelve hasta lo disponible
///   (venta menos devoluciones previas), con cantidades editables por línea.
/// - Sin recibo: solo Admin/Supervisor (permiso RefundNoReceipt) y motivo obligatorio;
///   se arma la devolución buscando productos del catálogo.
/// El reembolso sale de la caja actual: efectivo si la caja lo tiene, o tarjeta.
/// </summary>
public partial class RefundsViewModel : ViewModelBase
{
    private readonly RefundService _refunds;
    private readonly ProductService _products;
    private readonly CashSessionService _cashService;
    private readonly CashSessionTracker _cashTracker;
    private readonly ICurrentSession _session;

    private SalePreviewDto? _loadedSale;

    public ObservableCollection<RefundLineItem> RefundLines { get; } = [];
    public ObservableCollection<RefundHistoryItem> History { get; } = [];

    [ObservableProperty]
    private string _receiptNumberText = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _formError;

    [ObservableProperty]
    private string? _loadError;

    // ── Modo sin recibo (Admin/Supervisor) ──
    [ObservableProperty]
    private string _productSearchText = string.Empty;

    [ObservableProperty]
    private bool _isProductSearchOpen;

    [ObservableProperty]
    private ProductDto? _selectedProduct;

    [ObservableProperty]
    private string _manualQuantityText = "1";

    // ── Reembolso ──
    [ObservableProperty]
    private bool _isCashRefund = true;

    [ObservableProperty]
    private bool _isCardRefund;

    [ObservableProperty]
    private string _reason = string.Empty;

    // ── Resultado ──
    [ObservableProperty]
    private bool _isResultOpen;

    [ObservableProperty]
    private RefundDto? _lastRefund;

    /// <summary>Resumen del modal de resultado (P5.1): "Nota #X · 14:32 · Recibo #Y".</summary>
    public string ResultSummaryText => LastRefund is { } r
        ? $"Nota #{r.Number} · {r.CreatedAt:HH:mm}" +
          (r.OriginalSaleNumber is { } n ? $" · Recibo #{n}" : " · Sin recibo")
        : string.Empty;

    public string ResultTotalText => LastRefund is { } r ? $"RD$ {r.Total.Amount:N2}" : string.Empty;

    public bool CanRefundNoReceipt => _session.CurrentUser is { } u && Permissions.Has(u.Role, Permissions.RefundNoReceipt);

    /// <summary>Con recibo: todos los que venden. Sin recibo: solo quien tiene RefundNoReceipt.</summary>
    public bool CanRefund => _session.CurrentUser is { } u && Permissions.Has(u.Role, Permissions.Refund);

    public string TotalText => $"RD$ {RefundLines.Sum(l => l.Total):N2}";

    public bool HasLoadedSale => _loadedSale is not null;
    public string LoadedSaleText => _loadedSale is { } s
        ? $"Recibo #{s.Number} · {s.CreatedAt:dd/MM/yyyy HH:mm} · {s.CustomerName ?? "Cliente anónimo"}"
        : string.Empty;

    public string ReceiptHint => CanRefundNoReceipt
        ? "Deje el recibo vacío para devolver sin recibo (requiere motivo)"
        : string.Empty;

    public RefundsViewModel(
        RefundService refunds,
        ProductService products,
        CashSessionService cashService,
        CashSessionTracker cashTracker,
        ICurrentSession session)
    {
        _refunds = refunds;
        _products = products;
        _cashService = cashService;
        _cashTracker = cashTracker;
        _session = session;
    }

    public override async Task OnNavigatedToAsync()
    {
        await RefreshCashAsync();
        await LoadHistoryAsync();
    }

    private async Task RefreshCashAsync()
    {
        try
        {
            var session = await _cashService.GetOpenForUserAsync(_session.CurrentUserId);
            _cashTracker.Set(session);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RefundsViewModel] RefreshCash: {ex}");
        }
    }

    private async Task LoadHistoryAsync()
    {
        try
        {
            var recent = await _refunds.GetRecentAsync(20);
            History.Clear();
            foreach (var r in recent)
            {
                History.Add(new RefundHistoryItem
                {
                    Number = r.Number,
                    CreatedAtText = r.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                    UserName = r.UserName,
                    OriginalSaleNumber = r.OriginalSaleNumber,
                    Reason = r.Reason,
                    Total = r.Total.Amount,
                });
            }
        }
        catch (Exception ex)
        {
            LoadError = $"No se pudo cargar el historial: {ex.Message}";
        }
    }

    partial void OnReceiptNumberTextChanged(string value)
    {
        // Al tocar el recibo, si había una venta cargada se limpia (evita devolver
        // contra un recibo que ya no coincide con lo que se busca).
        if (_loadedSale is not null && !string.Equals(value.Trim(), _loadedSale.Number.ToString()))
        {
            ClearSale();
        }
        if (string.IsNullOrWhiteSpace(value))
            ClearSale();
        OnPropertyChanged(nameof(ReceiptHint));
    }

    private void ClearSale()
    {
        _loadedSale = null;
        RefundLines.Clear();
        OnPropertyChanged(nameof(HasLoadedSale));
        OnPropertyChanged(nameof(LoadedSaleText));
        OnPropertyChanged(nameof(TotalText));
    }

    [RelayCommand]
    private async Task SearchReceiptAsync()
    {
        if (IsBusy) return;
        var numberText = ReceiptNumberText.Trim();
        if (numberText.Length == 0 || !long.TryParse(numberText, out var number))
        {
            FormError = "Ingrese el número de recibo.";
            return;
        }

        FormError = null;
        IsBusy = true;
        try
        {
            var result = await _refunds.GetSalePreviewAsync(number);
            if (!result.IsSuccess)
            {
                FormError = result.ErrorMessage;
                return;
            }

            _loadedSale = result.Value;
            RefundLines.Clear();
            foreach (var line in _loadedSale.Lines.Where(l => l.AvailableQty > 0))
            {
                var item = new RefundLineItem
                {
                    ProductId = line.ProductId,
                    ProductName = line.ProductName,
                    UnitPrice = line.UnitPrice,
                    MaxQuantity = line.AvailableQty,
                    Quantity = line.AvailableQty,
                };
                item.QuantityChanged += OnLineQuantityChanged;
                RefundLines.Add(item);
            }
            OnPropertyChanged(nameof(HasLoadedSale));
            OnPropertyChanged(nameof(LoadedSaleText));
            OnPropertyChanged(nameof(TotalText));
        }
        catch (Exception ex)
        {
            FormError = $"No se pudo buscar el recibo: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ── Sin recibo: buscar producto del catálogo ──

    private async void ScheduleProductSearch(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            IsProductSearchOpen = false;
            return;
        }
        try
        {
            var results = await _products.SearchActiveAsync(value.Trim());
            SelectedProduct = results.FirstOrDefault();
            IsProductSearchOpen = results.Count > 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RefundsViewModel] ProductSearch: {ex}");
        }
    }

    partial void OnProductSearchTextChanged(string value) => ScheduleProductSearch(value);

    /// <summary>Enter en el buscador de producto: agrega el primer match como línea (sin recibo).</summary>
    [RelayCommand]
    private void AddManualProduct()
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

        var existing = RefundLines.FirstOrDefault(l => l.ProductId == SelectedProduct.Id);
        if (existing is not null)
        {
            existing.Quantity += qty;
            existing.NotifyTotals();
        }
        else
        {
            var item = new RefundLineItem
            {
                ProductId = SelectedProduct.Id,
                ProductName = SelectedProduct.Name,
                UnitPrice = SelectedProduct.Price,
                MaxQuantity = decimal.MaxValue,
                Quantity = qty,
            };
            item.QuantityChanged += OnLineQuantityChanged;
            RefundLines.Add(item);
        }

        ProductSearchText = string.Empty;
        IsProductSearchOpen = false;
        SelectedProduct = null;
        ManualQuantityText = "1";
        FormError = null;
        OnPropertyChanged(nameof(TotalText));
    }

    private void OnLineQuantityChanged(object? sender, EventArgs e) => OnPropertyChanged(nameof(TotalText));

    [RelayCommand]
    private void RemoveLine(RefundLineItem? line)
    {
        if (line is null) return;
        RefundLines.Remove(line);
        OnPropertyChanged(nameof(TotalText));
    }

    partial void OnIsCashRefundChanged(bool value)
    {
        if (value) IsCardRefund = false;
    }

    partial void OnIsCardRefundChanged(bool value)
    {
        if (value) IsCashRefund = false;
    }

    private static decimal ParseQuantity(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        var normalized = text.Replace(',', '.').Trim();
        return decimal.TryParse(normalized, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    // ── Procesar ──

    [RelayCommand]
    private async Task ProcessAsync()
    {
        if (IsBusy) return;
        FormError = null;

        // Defensa: caja abierta (la UI ya la bloquea vía candado del servicio).
        if (_cashTracker.Current is null)
        {
            FormError = "Abra la caja para procesar devoluciones.";
            return;
        }

        if (RefundLines.Count == 0)
        {
            FormError = "Agregue al menos una línea a devolver.";
            return;
        }

        // Permisos por camino.
        var withReceipt = _loadedSale is not null;
        if (withReceipt)
        {
            if (!CanRefund)
            {
                FormError = "Su rol no permite procesar devoluciones.";
                return;
            }
        }
        else
        {
            if (!CanRefundNoReceipt)
            {
                FormError = "La devolución sin recibo requiere un supervisor.";
                return;
            }
            if (string.IsNullOrWhiteSpace(Reason))
            {
                FormError = "El motivo es obligatorio en devoluciones sin recibo.";
                return;
            }
        }

        var request = new CreateRefundRequest
        {
            UserId = _session.CurrentUserId,
            CashSessionId = _cashTracker.Current.Id,
            OriginalSaleId = _loadedSale?.Id,
            Reason = withReceipt ? string.Empty : Reason.Trim(),
            Items = RefundLines.Select(l => new RefundItemRequest(l.ProductId, l.Quantity, l.UnitPrice)).ToList(),
            Payments =
            [
                new RefundPaymentRequest(IsCashRefund ? PaymentMethod.Cash : PaymentMethod.Card, RefundLines.Sum(l => l.Total))
            ],
        };

        IsBusy = true;
        try
        {
            var result = await _refunds.CreateAsync(request);
            if (!result.IsSuccess)
            {
                FormError = result.ErrorMessage;
                return;
            }

            LastRefund = result.Value;
            IsResultOpen = true;
            await RefreshCashAsync();   // el badge de caja baja el efectivo disponible
            await LoadHistoryAsync();
        }
        catch (Exception ex)
        {
            FormError = $"No se pudo procesar la devolución: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void NewRefund()
    {
        IsResultOpen = false;
        LastRefund = null;
        ReceiptNumberText = string.Empty;
        Reason = string.Empty;
        FormError = null;
        ClearSale();
        IsCashRefund = true;
        ProductSearchText = string.Empty;
        IsProductSearchOpen = false;
        SelectedProduct = null;
        ManualQuantityText = "1";
    }
}