using System.Text;
using POS.Application.Receipts;
using POS.Application.Sales;
using POS.Domain.Enums;

namespace POS.Tests;

/// <summary>
/// Tests del encoder ESC/POS (función pura, sin I/O). Golden test fija la
/// secuencia de bytes completa: init, alineación, negrita, avance y corte.
/// </summary>
public class EscPosEncoderTests
{
    // ─── Helpers ────────────────────────────────────────────────────────────

    private static bool Contains(byte[] haystack, params byte[] needle)
    {
        if (needle.Length == 0) return true;
        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
                if (haystack[i + j] != needle[j]) { match = false; break; }
            if (match) return true;
        }
        return false;
    }

    /// <summary>Bytes entre un marcador de inicio y el primer byte fin (exclusivo).</summary>
    private static byte[] SliceBetween(byte[] bytes, byte[] startMarker, byte endByte)
    {
        for (var i = 0; i <= bytes.Length - startMarker.Length; i++)
        {
            var match = true;
            for (var j = 0; j < startMarker.Length; j++)
                if (bytes[i + j] != startMarker[j]) { match = false; break; }
            if (match)
            {
                var end = Array.IndexOf(bytes, endByte, i + startMarker.Length);
                return bytes[(i + startMarker.Length)..end];
            }
        }
        return [];
    }

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

    // ─── Golden byte sequence ───────────────────────────────────────────────

    [Fact]
    public void Encode_ReciboMinimo_SecuenciaBytesExacta()
    {
        var text = "12345\n"
                 + ReceiptContentBuilder.Center("Centro") + "\n"
                 + "TOTAL: 100.00\n";

        var expected = new byte[]
        {
            // Init: ESC @, ESC t 2 (CP850), ESC 2
            0x1B, 0x40, 0x1B, 0x74, 0x02, 0x1B, 0x32,
            // "12345" → izquierda (ESC a 0) + texto + LF
            0x1B, 0x61, 0x00, 0x31, 0x32, 0x33, 0x34, 0x35, 0x0A,
            // Centro → centro (ESC a 1) + texto recortado + LF
            0x1B, 0x61, 0x01, 0x43, 0x65, 0x6E, 0x74, 0x72, 0x6F, 0x0A,
            // TOTAL → izquierda + negrita (ESC E 1) + texto + negrita off + LF
            0x1B, 0x61, 0x00, 0x1B, 0x45, 0x01,
            0x54, 0x4F, 0x54, 0x41, 0x4C, 0x3A, 0x20, 0x31, 0x30, 0x30, 0x2E, 0x30, 0x30,
            0x1B, 0x45, 0x00, 0x0A,
            // Avance 3 líneas + corte parcial
            0x1B, 0x64, 0x03, 0x1D, 0x56, 0x41,
        };

        Assert.Equal(expected, EscPosEncoder.Encode(text));
    }

    // ─── Alineación ─────────────────────────────────────────────────────────

    [Fact]
    public void Encode_LineaCentrada_RecortaRellenoYUsaAlineacion()
    {
        var bytes = EscPosEncoder.Encode("x\n" + ReceiptContentBuilder.Center("Hola") + "\n");

        var slice = SliceBetween(bytes, [0x1B, 0x61, 0x01], 0x0A);
        Assert.Equal(Encoding.ASCII.GetBytes("Hola"), slice);
    }

    [Fact]
    public void Encode_LineaConCantidad_ConservaEspaciosInternos()
    {
        var sale = SampleSale();
        var receipt = ReceiptContentBuilder.Build(sale);
        var bytes = EscPosEncoder.Encode(receipt);

        // "  2 x 100.00      200.00" — espacios internos intactos (alinea montos)
        Assert.True(Contains(bytes, 0x32, 0x20, 0x78, 0x20, 0x31, 0x30, 0x30, 0x2E, 0x30, 0x30,
            0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20,
            0x32, 0x30, 0x30, 0x2E, 0x30, 0x30));
    }

    [Fact]
    public void Encode_LineaVacia_EmiteSoloLfSinReemitirAlineacion()
    {
        var bytes = EscPosEncoder.Encode("a\n\nb\n");

        Assert.True(Contains(bytes, 0x61, 0x0A, 0x0A, 0x62)); // a LF LF b
        // Solo un ESC a 0 (el estado de alineación se reutiliza para "b")
        Assert.Equal(1, Count(bytes, 0x1B, 0x61));
    }

    // ─── Negrita ────────────────────────────────────────────────────────────

    [Fact]
    public void Encode_LineaTotal_SeEmiteEnNegrita()
    {
        var bytes = EscPosEncoder.Encode("TOTAL: 100.00\n");

        Assert.True(Contains(bytes, 0x1B, 0x45, 0x01, 0x54, 0x4F, 0x54, 0x41, 0x4C));
        Assert.True(Contains(bytes, 0x30, 0x30, 0x1B, 0x45, 0x00));
    }

    [Fact]
    public void Encode_SinTotal_NoEmiteNegrita()
    {
        var bytes = EscPosEncoder.Encode("Subtotal: 100.00\n");

        Assert.False(Contains(bytes, 0x1B, 0x45));
    }

    // ─── Página de códigos ──────────────────────────────────────────────────

    [Fact]
    public void Encode_TextoEspanol_UsaCP850()
    {
        var bytes = EscPosEncoder.Encode("áéíóúñ¡¿\n");

        Assert.True(Contains(bytes, 0xA0, 0x82, 0xA1, 0xA2, 0xA3, 0xA4, 0xAD, 0xA8));
    }

    [Fact]
    public void Encode_EmDash_BestFitAGuion()
    {
        var bytes = EscPosEncoder.Encode("A — B\n");

        Assert.True(Contains(bytes, 0x41, 0x20, 0x2D, 0x20, 0x42));
    }

    [Fact]
    public void Encode_CaracterFueraDeTabla_Interrogacion()
    {
        var bytes = EscPosEncoder.Encode("€\n");

        Assert.True(Contains(bytes, 0x3F));
    }

    [Fact]
    public void Encode_Cp437_UsaEscT0YBestFitUppercase()
    {
        var bytes = EscPosEncoder.Encode("Á", new EscPosOptions { CodePage = EscPosCodePage.Cp437 });

        Assert.True(Contains(bytes, 0x1B, 0x74, 0x00)); // ESC t 0
        Assert.True(Contains(bytes, 0x41));            // Á → 'A' (CP437 no lo tiene)
    }

    [Fact]
    public void Encode_Cp850_UpperAcentosEnTabla()
    {
        var bytes = EscPosEncoder.Encode("Á\n");

        Assert.True(Contains(bytes, 0xB5)); // Á → 0xB5 en CP850
    }

    // ─── Corte y avance ─────────────────────────────────────────────────────

    [Fact]
    public void Encode_CorteCompleto_UsaGsV66()
    {
        var bytes = EscPosEncoder.Encode("x\n", new EscPosOptions { Cut = EscPosCut.Full });

        Assert.Equal((byte)0x1D, bytes[^3]);
        Assert.Equal((byte)0x56, bytes[^2]);
        Assert.Equal((byte)0x42, bytes[^1]);
    }

    [Fact]
    public void Encode_FeedCero_OmiteEscD()
    {
        var bytes = EscPosEncoder.Encode("x\n", new EscPosOptions { FeedLinesBeforeCut = 0 });

        Assert.False(Contains(bytes, 0x1B, 0x64));
        Assert.True(Contains(bytes, 0x1D, 0x56, 0x41));
    }

    // ─── Integración con el builder ─────────────────────────────────────────

    [Fact]
    public void Encode_ReciboRealDelBuilder_FormatoCompleto()
    {
        var receipt = ReceiptContentBuilder.Build(SampleSale());
        var bytes = EscPosEncoder.Encode(receipt);

        Assert.True(Contains(bytes, 0x1B, 0x40));                        // init
        Assert.True(Contains(bytes, 0x1B, 0x61, 0x01));                  // algún bloque centrado
        Assert.True(Contains(bytes, 0x1B, 0x45, 0x01, 0x54, 0x4F, 0x54, 0x41, 0x4C)); // TOTAL en negrita
        Assert.True(Contains(bytes, 0x43, 0x61, 0x66, 0x82));            // "Café" con é→0x82 (CP850)
        Assert.Equal((byte)0x1D, bytes[^3]);                             // termina con corte
        Assert.Equal((byte)0x56, bytes[^2]);
        Assert.Equal((byte)0x41, bytes[^1]);
    }

    // ─── Helpers internos ───────────────────────────────────────────────────

    private static int Count(byte[] bytes, params byte[] needle)
    {
        var n = 0;
        if (needle.Length == 0) return 0;
        for (var i = 0; i <= bytes.Length - needle.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
                if (bytes[i + j] != needle[j]) { match = false; break; }
            if (match) n++;
        }
        return n;
    }
}
