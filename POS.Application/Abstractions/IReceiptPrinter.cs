using POS.Application.Sales;

namespace POS.Application.Abstractions;

/// <summary>
/// Puerta de salida para impresión de recibos.
/// Fase 0: implementación que imprime en consola (validación del flujo).
/// Fase 1: térmica ESC/POS real (80mm) vía P/Invoke a winspool.drv.
/// </summary>
public interface IReceiptPrinter
{
    Task PrintReceiptAsync(SaleDto sale, CancellationToken ct = default);
}
