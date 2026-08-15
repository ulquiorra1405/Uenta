using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Application.Reports;

namespace POS.Desktop.ViewModels;

/// <summary>Producto más vendido (fila del dashboard, P4.2).</summary>
public partial class TopProductListItem : ObservableObject
{
    public string ProductName { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal Total { get; init; }
    public string QuantityText => $"{Quantity:N0}";
    public string TotalText => $"RD$ {Total:N2}";
}

/// <summary>Ventas por vendedor (fila del dashboard, P4.2).</summary>
public partial class SalesByUserListItem : ObservableObject
{
    public string UserName { get; init; } = string.Empty;
    public int TicketCount { get; init; }
    public decimal Total { get; init; }
    public string TicketText => $"{TicketCount:N0}";
    public string TotalText => $"RD$ {Total:N2}";
}

/// <summary>
/// Dashboard de reportes (P4.2): KPIs del día (total, tickets, promedio, ítems)
/// + top productos más vendidos y ventas por vendedor del periodo seleccionado.
/// Solo lectura: nada aquí escribe en la base.
/// </summary>
public partial class ReportsViewModel : ViewModelBase
{
    private readonly ReportService _reports;
    private int _daysBack = 1;

    public ObservableCollection<TopProductListItem> TopProducts { get; } = [];
    public ObservableCollection<SalesByUserListItem> SalesByUser { get; } = [];

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _loadError;

    // ── KPIs del día (siempre "hoy") ──
    [ObservableProperty]
    private string _todayTotalText = "RD$ 0.00";

    [ObservableProperty]
    private string _todayTicketsText = "0";

    [ObservableProperty]
    private string _todayAverageText = "RD$ 0.00";

    [ObservableProperty]
    private string _todayItemsText = "0";

    // ── Selector de periodo (afecta las tablas) ──
    [ObservableProperty]
    private bool _isPeriodToday = true;

    [ObservableProperty]
    private bool _isPeriod7Days;

    [ObservableProperty]
    private bool _isPeriod30Days;

    [ObservableProperty]
    private string _periodLabel = "Hoy";

    public ReportsViewModel(ReportService reports) => _reports = reports;

    public override async Task OnNavigatedToAsync() => await LoadAsync();

    [RelayCommand]
    private async Task SelectPeriodAsync(string? period)
    {
        IsPeriodToday = period == "today";
        IsPeriod7Days = period == "7";
        IsPeriod30Days = period == "30";
        PeriodLabel = period switch
        {
            "7" => "Últimos 7 días",
            "30" => "Últimos 30 días",
            _ => "Hoy",
        };
        _daysBack = period switch
        {
            "7" => 7,
            "30" => 30,
            _ => 1,
        };
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        LoadError = null;
        try
        {
            var now = DateTimeOffset.Now;
            var todayStart = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset);

            // KPIs: siempre el día natural de hoy.
            var today = await _reports.GetDailySummaryAsync(todayStart);
            TodayTotalText = $"RD$ {today.Total.Amount:N2}";
            TodayTicketsText = today.TicketCount.ToString("N0");
            TodayAverageText = $"RD$ {today.AverageTicket.Amount:N2}";
            TodayItemsText = today.ItemCount.ToString("N0");

            // Tablas: periodo seleccionado (hoy / 7 días / 30 días).
            var from = todayStart.AddDays(-(_daysBack - 1));
            var to = todayStart.AddDays(1);

            var top = await _reports.GetTopProductsAsync(from, to, top: 5);
            TopProducts.Clear();
            foreach (var p in top)
            {
                TopProducts.Add(new TopProductListItem
                {
                    ProductName = p.ProductName,
                    Quantity = p.Quantity,
                    Total = p.Total.Amount,
                });
            }

            var byUser = await _reports.GetSalesByUserAsync(from, to);
            SalesByUser.Clear();
            foreach (var u in byUser)
            {
                SalesByUser.Add(new SalesByUserListItem
                {
                    UserName = u.UserName,
                    TicketCount = u.TicketCount,
                    Total = u.Total.Amount,
                });
            }
        }
        catch (Exception ex)
        {
            LoadError = $"No se pudieron cargar los reportes: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}