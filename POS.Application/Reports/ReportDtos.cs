using POS.Domain.ValueObjects;

namespace POS.Application.Reports;

/// <summary>Resumen de ventas de un día (P4.2).</summary>
public record DailySalesDto
{
    public DateTimeOffset Date { get; init; }
    public int TicketCount { get; init; }
    public Money Total { get; init; }
    public Money AverageTicket { get; init; }
    public int ItemCount { get; init; }
}

/// <summary>Producto más vendido en un periodo (cantidad y RD$ vendidos).</summary>
public record TopProductDto
{
    public string ProductName { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public Money Total { get; init; }
}

/// <summary>Ventas agrupadas por vendedor en un periodo.</summary>
public record SalesByUserDto
{
    public string UserName { get; init; } = string.Empty;
    public int TicketCount { get; init; }
    public Money Total { get; init; }
}

/// <summary>Ventas totales de un periodo (para el dashboard).</summary>
public record PeriodSalesDto
{
    public DateTimeOffset From { get; init; }
    public DateTimeOffset To { get; init; }
    public int TicketCount { get; init; }
    public Money Total { get; init; }
    public Money AverageTicket { get; init; }
}