using System.Text;
using POS.Application.Sales;
using POS.Domain.Enums;
using POS.Infrastructure.Services;

namespace POS.Tests;

/// <summary>
/// Tests del generador PDF del recibo (Fase 1, paso 4).
/// Verifican estructura válida y que el contenido del recibo viaja en el PDF.
/// </summary>
public class ReceiptPdfGeneratorTests
{
    private static SaleDto SampleSale() => new()
    {
        Number = 5,
        CreatedAt = new DateTimeOffset(2026, 8, 11, 9, 30, 0, TimeSpan.FromHours(-4)),
        Subtotal = 236.00m,
        Itbis = 36.00m,
        Discount = 0m,
        Total = 236.00m,
        Items =
        [
            new SaleItemDto
            {
                ProductName = "Café con leche",
                Quantity = 2,
                UnitPrice = 100.00m,
                LineDiscount = 0m,
                Total = 200.00m,
            },
        ],
        Payments = [new PaymentDto { Method = PaymentMethod.Cash, Amount = 236.00m }],
    };

    [Fact]
    public void Generate_Recibo_EstructuraPdfValida()
    {
        var bytes = new ReceiptPdfGenerator().Generate(SampleSale());

        Assert.True(bytes.Length > 1000, $"PDF demasiado pequeño: {bytes.Length} bytes");

        // Encabezado %PDF
        Assert.Equal((byte)'%', bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'D', bytes[2]);
        Assert.Equal((byte)'F', bytes[3]);

        // Cola %%EOF en los últimos 2 KB
        var tail = Encoding.ASCII.GetString(bytes[^2048..]);
        Assert.Contains("%%EOF", tail);

        // Una sola página (el recibo cabe en una hoja)
        var ascii = Encoding.ASCII.GetString(bytes);
        Assert.Contains("/Count 1", ascii);
    }

    [Fact]
    public void GenerateToFile_CreaArchivoValido()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pos-recibo-{Guid.NewGuid():N}.pdf");
        try
        {
            new ReceiptPdfGenerator().GenerateToFile(SampleSale(), path);

            Assert.True(File.Exists(path));
            var bytes = File.ReadAllBytes(path);
            Assert.True(bytes.Length > 1000);
            Assert.Equal((byte)'%', bytes[0]);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
