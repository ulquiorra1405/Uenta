using POS.Application.Abstractions;
using POS.Application.Sales;

namespace POS.Infrastructure.Services;

/// <summary>
/// Implementación de Fase 0: imprime el recibo en consola (validación del flujo).
/// Fase 1: se reemplaza por la térmica ESC/POS real (80mm) vía P/Invoke a winspool.drv.
/// </summary>
public class ConsoleReceiptPrinter : IReceiptPrinter
{
    public Task PrintReceiptAsync(SaleDto sale, CancellationToken ct = default)
    {
        var line = new string('-', 42);

        Console.WriteLine(line);
        Console.WriteLine("            UENTA — RECIBO DE VENTA");
        Console.WriteLine(line);
        Console.WriteLine($"Recibo #: {sale.Number}");
        Console.WriteLine($"Fecha:    {sale.CreatedAt:dd/MM/yyyy HH:mm}");
        Console.WriteLine(line);
        foreach (var item in sale.Items)
        {
            Console.WriteLine(item.ProductName);
            Console.WriteLine($"  {item.Quantity:N0} x {item.UnitPrice.Amount:N2}   {item.Total.Amount,10:N2}");
        }
        Console.WriteLine(line);
        Console.WriteLine($"Subtotal:      {sale.Subtotal.Amount,10:N2}");
        Console.WriteLine($"ITBIS 18%:     {sale.Itbis.Amount,10:N2}");
        if (sale.Discount.Amount > 0)
            Console.WriteLine($"Descuento:     {sale.Discount.Amount,10:N2}");
        Console.WriteLine($"TOTAL:         {sale.Total.Amount,10:N2}");
        foreach (var p in sale.Payments)
            Console.WriteLine($"Pago ({p.Method}):  {p.Amount.Amount,10:N2}");
        Console.WriteLine(line);
        Console.WriteLine("         ¡Gracias por su compra!");
        Console.WriteLine(line);

        return Task.CompletedTask;
    }
}
