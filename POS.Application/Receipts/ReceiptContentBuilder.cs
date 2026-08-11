using System.Text;
using POS.Application.Sales;
using POS.Domain.Enums;

namespace POS.Application.Receipts;

/// <summary>
/// Motor de contenido del recibo — función pura: <see cref="SaleDto"/> → texto
/// (ancho 42, estándar térmica 80mm). Sin I/O, sin estado: 100% testeable.
/// Es el cerebro del layout: lo comparten el printer de consola (Fase 0),
/// la térmica ESC/POS y el PDF (Fase 1).
/// </summary>
public static class ReceiptContentBuilder
{
    /// <summary>Ancho del recibo en caracteres (42 = térmica 80mm estándar).</summary>
    public const int Width = 42;

    private const string Separator = "------------------------------------------"; // Width

    /// <summary>Genera el recibo completo terminado en '\n'.</summary>
    public static string Build(SaleDto sale)
    {
        var sb = new StringBuilder();

        void Add(string s) => sb.Append(s).Append('\n');

        Add(Separator);
        Add(Center("UENTA — RECIBO DE VENTA"));
        Add(Separator);
        Add($"Recibo #: {sale.Number}");
        Add($"Fecha:    {sale.CreatedAt:dd/MM/yyyy HH:mm}");
        Add(Separator);
        foreach (var item in sale.Items)
        {
            Add(item.ProductName);
            Add($"  {FormatQuantity(item.Quantity)} x {item.UnitPrice.Amount:N2}   {item.Total.Amount,10:N2}");
            if (item.LineDiscount.Amount > 0)
                Add($"{"Desc.:",-15}{-item.LineDiscount.Amount,10:N2}");
        }
        Add(Separator);
        Add($"{"Subtotal:",-15}{sale.Subtotal.Amount,10:N2}");
        Add($"{"ITBIS 18%:",-15}{sale.Itbis.Amount,10:N2}");
        if (sale.Discount.Amount > 0)
            Add($"{"Descuento:",-15}{-sale.Discount.Amount,10:N2}");
        Add($"{"TOTAL:",-15}{sale.Total.Amount,10:N2}");
        foreach (var p in sale.Payments)
            Add($"{"Pago " + MethodName(p.Method) + ":",-15}{p.Amount.Amount,10:N2}");
        Add(Separator);
        Add(Center("¡Gracias por su compra!"));
        Add(Separator);

        return sb.ToString();
    }

    /// <summary>
    /// Centra un texto en el ancho del recibo (resto repartido a ambos lados).
    /// Público: el encoder ESC/POS lo reutiliza para detectar líneas centradas.
    /// </summary>
    public static string Center(string text, int width = Width)
    {
        if (text.Length >= width) return text;
        var pad = (width - text.Length) / 2;
        return new string(' ', pad) + text + new string(' ', width - text.Length - pad);
    }

    /// <summary>Cantidad limpia: enteros sin decimales, fracciones hasta 3 dígitos.</summary>
    private static string FormatQuantity(decimal q)
        => q == decimal.Truncate(q) ? q.ToString("N0") : q.ToString("0.###");

    private static string MethodName(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Efectivo",
        PaymentMethod.Card => "Tarjeta",
        PaymentMethod.Transfer => "Transferencia",
        _ => method.ToString(),
    };
}
