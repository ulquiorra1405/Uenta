using POS.Application.Receipts;
using POS.Application.Sales;
using POS.Application.Settings;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace POS.Infrastructure.Services;

/// <summary>
/// Genera el recibo como PDF imprimible/guardable (Fase 1, paso 4).
/// Mismo contenido que la térmica y la consola: delega en
/// <see cref="ReceiptContentBuilder"/> (cerebro único de layout) y lo renderiza
/// con fuente monospace (Courier New) para que la alineación de columnas del
/// recibo de 42 chars se conserve exacta.
/// </summary>
public class ReceiptPdfGenerator
{
    private readonly SettingsService? _settings;

    /// <summary>Constructor con DI (app): inyecta datos de negocio al recibo.</summary>
    public ReceiptPdfGenerator(SettingsService settings) => _settings = settings;

    /// <summary>Constructor sin DI (tests/consola): recibo sin datos de negocio.</summary>
    public ReceiptPdfGenerator() { }

    static ReceiptPdfGenerator()
    {
        // Licencia Community: gratis para empresas < 1M USD de ingresos anuales.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>Genera el PDF del recibo de una venta (bytes listos para guardar).</summary>
    public byte[] Generate(SaleDto sale)
    {
        var settings = _settings?.GetReceiptSettingsAsync().GetAwaiter().GetResult();
        var receipt = ReceiptContentBuilder.Build(sale, settings);
        var lines = receipt.TrimEnd('\n').Split('\n');

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(40);
                page.DefaultTextStyle(TextStyle.Default
                    .FontFamily("Courier New")
                    .FontSize(11)
                    .LineHeight(1.2f));

                page.Content()
                    .Border(1)
                    .BorderColor(Colors.Grey.Lighten3)
                    .Background(Colors.White)
                    .Padding(20)
                    .Column(col =>
                    {
                        foreach (var line in lines)
                        {
                            if (line.Length == 0)
                            {
                                col.Item().Height(6); // línea en blanco = separador
                                continue;
                            }

                            var isCentered = line == ReceiptContentBuilder.Center(line.Trim());
                            col.Item().Text(text =>
                            {
                                text.DefaultTextStyle(TextStyle.Default
                                    .FontFamily("Courier New")
                                    .FontSize(11)
                                    .LineHeight(1.2f));
                                if (isCentered)
                                {
                                    text.AlignCenter();
                                    text.Span(line.Trim());
                                }
                                else
                                {
                                    text.AlignLeft();
                                    text.Span(line);
                                }
                            });
                        }
                    });
            });
        });

        return document.GeneratePdf();
    }

    /// <summary>Genera el PDF y lo guarda en <paramref name="path"/> (sobrescribe).</summary>
    public void GenerateToFile(SaleDto sale, string path)
    {
        var bytes = Generate(sale);
        File.WriteAllBytes(path, bytes);
    }
}