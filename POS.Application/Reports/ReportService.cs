using POS.Application.Abstractions;
using POS.Domain.Entities;
using POS.Domain.ValueObjects;

namespace POS.Application.Reports;

/// <summary>
/// Caso de uso: reportes y dashboard (P4.2). Todos los agregados se calculan
/// sobre ventas COMPLETADAS dentro del rango solicitado. Es solo lectura:
/// nada de esto escribe en la base.
/// </summary>
public class ReportService
{
    private readonly IReportRepository _reports;

    public ReportService(IReportRepository reports) => _reports = reports;

    /// <summary>Resumen de ventas de un día natural (local), total + promedio + tickets.</summary>
    public async Task<DailySalesDto> GetDailySummaryAsync(DateTimeOffset date, CancellationToken ct = default)
    {
        var from = new DateTimeOffset(date.Year, date.Month, date.Day, 0, 0, 0, date.Offset);
        var to = from.AddDays(1);
        var sales = await _reports.GetSalesAsync(from, to, ct);
        return BuildDaily(from, sales);
    }

    /// <summary>Resumen de ventas entre dos fechas.</summary>
    public async Task<PeriodSalesDto> GetPeriodSummaryAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var sales = await _reports.GetSalesAsync(from, to, ct);
        return BuildPeriod(from, to, sales);
    }

    /// <summary>Top N productos más vendidos por cantidad, en un periodo.</summary>
    public async Task<List<TopProductDto>> GetTopProductsAsync(DateTimeOffset from, DateTimeOffset to, int top = 5, CancellationToken ct = default)
    {
        var sales = await _reports.GetSalesAsync(from, to, ct);
        return sales
            .SelectMany(s => s.Items)
            .GroupBy(i => i.ProductName)
            .Select(g => new TopProductDto
            {
                ProductName = g.Key,
                Quantity = g.Sum(i => i.Quantity),
                Total = g.Sum(i => i.Total)
            })
            .OrderByDescending(p => p.Quantity)
            .ThenByDescending(p => p.Total.Amount)
            .Take(top)
            .ToList();
    }

    /// <summary>Ventas agrupadas por vendedor, en un periodo.</summary>
    public async Task<List<SalesByUserDto>> GetSalesByUserAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var sales = await _reports.GetSalesAsync(from, to, ct);
        return sales
            .GroupBy(s => s.User?.DisplayName ?? "—")
            .Select(g => new SalesByUserDto
            {
                UserName = g.Key,
                TicketCount = g.Count(),
                Total = g.Sum(s => s.Total)
            })
            .OrderByDescending(u => u.Total.Amount)
            .ToList();
    }

    private static DailySalesDto BuildDaily(DateTimeOffset date, List<Sale> sales)
    {
        var total = sales.Aggregate(Money.Zero, (acc, s) => acc + s.Total);
        var items = sales.Sum(s => s.Items.Count);
        return new DailySalesDto
        {
            Date = date,
            TicketCount = sales.Count,
            Total = total,
            AverageTicket = sales.Count > 0 ? total / sales.Count : Money.Zero,
            ItemCount = items,
        };
    }

    private static PeriodSalesDto BuildPeriod(DateTimeOffset from, DateTimeOffset to, List<Sale> sales)
    {
        var total = sales.Aggregate(Money.Zero, (acc, s) => acc + s.Total);
        return new PeriodSalesDto
        {
            From = from,
            To = to,
            TicketCount = sales.Count,
            Total = total,
            AverageTicket = sales.Count > 0 ? total / sales.Count : Money.Zero,
        };
    }
}