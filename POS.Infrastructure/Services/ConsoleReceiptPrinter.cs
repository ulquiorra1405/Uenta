using POS.Application.Abstractions;
using POS.Application.Receipts;
using POS.Application.Sales;

namespace POS.Infrastructure.Services;

/// <summary>
/// Fase 0 (solo devs): imprime el recibo en consola para validar el flujo.
/// El layout ya no vive aquí: delega en <see cref="ReceiptContentBuilder"/>,
/// el motor compartido con la térmica ESC/POS y el PDF (Fase 1).
/// </summary>
public class ConsoleReceiptPrinter : IReceiptPrinter
{
    public Task PrintReceiptAsync(SaleDto sale, CancellationToken ct = default)
    {
        var receipt = ReceiptContentBuilder.Build(sale);
        foreach (var line in receipt.TrimEnd('\n').Split('\n'))
            Console.WriteLine(line);
        return Task.CompletedTask;
    }
}
