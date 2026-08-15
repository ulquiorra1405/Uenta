using POS.Application.Abstractions;
using POS.Application.Receipts;
using POS.Application.Sales;
using POS.Application.Settings;
using POS.Infrastructure.Printing;

namespace POS.Infrastructure.Services;

/// <summary>
/// Impresora térmica ESC/POS real (Fase 1A, P1.1). Usa el mismo cerebro de
/// layout (<see cref="ReceiptContentBuilder"/>) + <see cref="EscPosEncoder"/>
/// y envía los bytes crudos a la impresora seleccionada en Ajustes
/// (P/Invoke winspool.drv, <see cref="RawPrinterHelper"/>).
/// Regla dura: el printer PUEDE lanzar; el llamador decide cómo mostrarlo
/// (el cobro ya está persistido — la impresión nunca bloquea la venta).
/// </summary>
public class ThermalReceiptPrinter : IReceiptPrinter
{
    private readonly SettingsService _settings;

    public ThermalReceiptPrinter(SettingsService settings) => _settings = settings;

    public async Task PrintReceiptAsync(SaleDto sale, CancellationToken ct = default)
    {
        var receiptSettings = await _settings.GetReceiptSettingsAsync(ct);

        var printerName = receiptSettings.PrinterName;
        if (string.IsNullOrWhiteSpace(printerName))
            throw new InvalidOperationException(
                "No hay impresora configurada. Ábrala en Ajustes.");

        var receiptText = ReceiptContentBuilder.Build(sale, receiptSettings);
        var bytes = EscPosEncoder.Encode(receiptText);

        // Envío síncrono (P/Invoke bloqueante) fuera del hilo de UI.
        await Task.Run(() =>
        {
            for (var i = 0; i < receiptSettings.Copies; i++)
                RawPrinterHelper.SendBytesToPrinter(printerName, bytes);
        }, ct);
    }
}