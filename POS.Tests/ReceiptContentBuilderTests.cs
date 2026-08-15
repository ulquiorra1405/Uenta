using POS.Application.Receipts;
using POS.Application.Sales;
using POS.Application.Settings;
using POS.Domain.Enums;

namespace POS.Tests;

/// <summary>
/// Tests del motor de contenido del recibo (función pura, sin I/O).
/// El golden test fija el layout completo: ancho 42, alineación, centrado.
/// </summary>
public class ReceiptContentBuilderTests
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
    public void Build_ReciboCompleto_FormatoEsperado()
    {
        var receipt = ReceiptContentBuilder.Build(SampleSale());

        var expected = string.Join('\n',
            "------------------------------------------",
            "         UENTA — RECIBO DE VENTA          ",
            "------------------------------------------",
            "Recibo #: 5",
            "Fecha:    11/08/2026 09:30",
            "------------------------------------------",
            "Café con leche",
            "  2 x 100.00" + new string(' ', 7) + "200.00",
            "------------------------------------------",
            "Subtotal:" + new string(' ', 10) + "236.00",
            "ITBIS 18%:" + new string(' ', 10) + "36.00",
            "TOTAL:" + new string(' ', 13) + "236.00",
            "Pago Efectivo:" + new string(' ', 5) + "236.00",
            "------------------------------------------",
            "         ¡Gracias por su compra!          ",
            "------------------------------------------") + "\n";

        Assert.Equal(expected, receipt);
    }

    [Fact]
    public void Build_DescuentoGlobal_MuestraLineaNegativa()
    {
        var sale = SampleSale();
        sale.Discount = 50m;
        sale.Total = 186m;

        var receipt = ReceiptContentBuilder.Build(sale);

        Assert.Contains("Descuento:" + new string(' ', 9) + "-50.00", receipt);
    }

    [Fact]
    public void Build_DescuentoPorLinea_MuestraLinea()
    {
        var sale = SampleSale();
        sale.Items[0].LineDiscount = 20m;

        var receipt = ReceiptContentBuilder.Build(sale);

        Assert.Contains("Desc.:" + new string(' ', 13) + "-20.00", receipt);
    }

    [Fact]
    public void Build_MetodosDePago_NombresEnEspanol()
    {
        var sale = SampleSale();
        sale.Payments =
        [
            new PaymentDto { Method = PaymentMethod.Cash, Amount = 100m },
            new PaymentDto { Method = PaymentMethod.Card, Amount = 100m },
            new PaymentDto { Method = PaymentMethod.Transfer, Amount = 36m },
        ];

        var receipt = ReceiptContentBuilder.Build(sale);

        Assert.Contains("Pago Efectivo:", receipt);
        Assert.Contains("Pago Tarjeta:", receipt);
        Assert.Contains("Pago Transferencia:", receipt);
    }

    [Fact]
    public void Build_CantidadFraccionaria_FormatoLimpio()
    {
        var sale = SampleSale();
        sale.Items[0].Quantity = 0.5m;
        sale.Items[0].Total = 50m;

        var receipt = ReceiptContentBuilder.Build(sale);

        Assert.Contains("  0.5 x 100.00" + new string(' ', 8) + "50.00", receipt);
    }

    [Fact]
    public void Build_TodasLasLineas_RespetanAncho42()
    {
        var sale = SampleSale();
        sale.Discount = 50m;
        sale.Items[0].LineDiscount = 20m;
        sale.Payments =
        [
            new PaymentDto { Method = PaymentMethod.Cash, Amount = 100m },
            new PaymentDto { Method = PaymentMethod.Transfer, Amount = 36m },
        ];

        var receipt = ReceiptContentBuilder.Build(sale);
        var lines = receipt.TrimEnd('\n').Split('\n');

        Assert.All(lines, line => Assert.True(line.Length <= ReceiptContentBuilder.Width,
            $"Línea excede {ReceiptContentBuilder.Width} chars: '{line}' ({line.Length})"));
    }

    [Fact]
    public void Build_ConCaja_MuestraLineaCaja()
    {
        var sale = SampleSale();
        sale.CashSessionId = 3;

        var receipt = ReceiptContentBuilder.Build(sale);

        Assert.Contains("Caja #:   3", receipt);
    }

    [Fact]
    public void Build_SinCaja_NoMuestraLineaCaja()
    {
        var sale = SampleSale();
        sale.CashSessionId = null;

        var receipt = ReceiptContentBuilder.Build(sale);

        Assert.DoesNotContain("Caja #:", receipt);
    }

    [Fact]
    public void Build_ConDatosDeNegocio_EncabezadoYPiePersonalizado()
    {
        var sale = SampleSale();
        var settings = new ReceiptSettingsDto
        {
            BusinessName = "Colmado La Esquina",
            BusinessRnc = "130-12345-6",
            BusinessAddress = "Av. Duarte 45, Santo Domingo",
            ReceiptFooter = "¡Vuelva pronto!",
        };

        var receipt = ReceiptContentBuilder.Build(sale, settings);

        Assert.Contains("Colmado La Esquina", receipt);
        Assert.Contains("RNC: 130-12345-6", receipt);
        Assert.Contains("Av. Duarte 45, Santo Domingo", receipt);
        Assert.Contains("¡Vuelva pronto!", receipt);
        Assert.DoesNotContain("¡Gracias por su compra!", receipt); // pie personalizado reemplaza al default
    }

    [Fact]
    public void Build_SinDatosDeNegocio_NoCambiaLayout()
    {
        var sale = SampleSale();
        var withNull = ReceiptContentBuilder.Build(sale, null);
        var withoutOverload = ReceiptContentBuilder.Build(sale);

        Assert.Equal(withoutOverload, withNull);
        Assert.DoesNotContain("Colmado", withNull);
    }
}
