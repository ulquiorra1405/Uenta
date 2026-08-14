using POS.Application.Sales;
using POS.Domain.ValueObjects;

namespace POS.Tests;

/// <summary>
/// Golden tests del motor de precios único (CartCalculator). Estos son los valores
/// de referencia del orden estable: línea → neto → global → total → ITBIS.
/// Cualquier cambio en el cálculo se hace AQUÍ, no en el ViewModel ni en el service.
/// </summary>
public class CartCalculatorTests
{
    [Fact]
    public void Linea_Porcentaje_SigueALaCantidad()
    {
        // 100 × 1 → 100; 5% → 5.00
        var gross = CartCalculator.LineGross(100m, 1);
        Assert.Equal(100m, gross);

        var discount = CartCalculator.LineDiscountByPercent(gross, 5);
        Assert.Equal(5m, discount);
        Assert.Equal(95m, CartCalculator.LineTotal(100m, 1, discount));

        // Con cantidad 2 el % se re-calcula: 200 → 10
        var gross2 = CartCalculator.LineGross(100m, 2);
        Assert.Equal(200m, gross2);
        Assert.Equal(10m, CartCalculator.LineDiscountByPercent(gross2, 5));
    }

    [Fact]
    public void Linea_Porcentaje_FueraDeRango_SeLimita()
    {
        var gross = CartCalculator.LineGross(100m, 1);
        Assert.Equal(100m, CartCalculator.LineDiscountByPercent(gross, 150)); // tope en bruto
        Assert.Equal(0m, CartCalculator.LineDiscountByPercent(gross, -5));    // clamp a 0
    }

    [Fact]
    public void Linea_MontoFijo_EsPromesaLiteral()
    {
        var gross = CartCalculator.LineGross(60m, 3);
        Assert.Equal(180m, gross);
        Assert.Equal(30m, CartCalculator.LineDiscountByAmount(gross, 30m)); // no sigue a la cantidad
        Assert.Equal(180m, CartCalculator.LineDiscountByAmount(gross, 999m)); // tope en bruto
        Assert.Equal(0m, CartCalculator.LineDiscountByAmount(gross, -30m));  // negativo → 0
    }

    [Fact]
    public void Totales_SinDescuento_DesgloseRetail()
    {
        // 2 × 100 = 200 → ITBIS 18/118 = 30.51, base = 169.49
        var t = CartCalculator.ComputeTotals(subtotalNet: 200m, globalDiscount: 0m);

        Assert.Equal(200m, t.Total);
        Assert.False(t.DiscountExceedsSubtotal);
        Assert.Equal(30.51m, t.Itbis);
        Assert.Equal(169.49m, t.BaseImponible);
    }

    [Fact]
    public void Totales_DescuentoGlobal_ReduceBase()
    {
        // 200 − 50 = 150 → ITBIS 18/118 = 22.88, base = 127.12
        var t = CartCalculator.ComputeTotals(200m, 50m);

        Assert.Equal(150m, t.Total);
        Assert.False(t.DiscountExceedsSubtotal);
        Assert.Equal(22.88m, t.Itbis);
        Assert.Equal(127.12m, t.BaseImponible);
    }

    [Fact]
    public void Totales_DescuentoGlobalSuperaSubtotal_FlagYTotalCero()
    {
        var t = CartCalculator.ComputeTotals(50m, 80m);

        Assert.True(t.DiscountExceedsSubtotal);
        Assert.Equal(0m, t.Total); // visible: 0, pero la venta real se bloquea con el flag
        Assert.Equal(0m, t.Itbis);
    }

    [Fact]
    public void ItbisFromTotalIncluded_RedondeaComoLaVenta()
    {
        Assert.Equal(30.51m, CartCalculator.ItbisFromTotalIncluded(new Money(200m)).Amount);
    }
}