using POS.Domain.ValueObjects;

namespace POS.Application.Sales;

/// <summary>
/// Cálculo de precios de una venta — ÚNICA fuente de verdad. Tanto
/// <see cref="SaleService"/> (persistencia) como el ViewModel del ticket (preview)
/// delegan aquí, para que lo que ve el cajero coincida POR CONSTRUCCIÓN con lo que
/// se graba. Orden estable (venta.md): línea (bruto → descuento → total) →
/// subtotal neto → descuento global → total → desglose ITBIS.
/// Todo dinero se redondea con <see cref="Money.Round"/> (2 decimales, AwayFromZero).
/// </summary>
public static class CartCalculator
{
    /// <summary>ITBIS 18% incluido en el precio de venta (retail RD, decisión P2).</summary>
    public const decimal ItbisRate = 0.18m;

    /// <summary>Total bruto de la línea: precio unitario × cantidad.</summary>
    public static decimal LineGross(decimal unitPrice, decimal quantity) =>
        Money.Round(unitPrice * quantity);

    /// <summary>
    /// Descuento por porcentaje: dinámico (sigue a la cantidad), con tope en el bruto.
    /// El % se limita a [0,100].
    /// </summary>
    public static decimal LineDiscountByPercent(decimal gross, decimal percent) =>
        Math.Min(Money.Round(gross * Math.Clamp(percent, 0, 100) / 100m), gross);

    /// <summary>
    /// Descuento por monto fijo: promesa literal del cajero, con tope en el bruto.
    /// Negativos se tratan como 0.
    /// </summary>
    public static decimal LineDiscountByAmount(decimal gross, decimal fixedAmount) =>
        Math.Min(Math.Max(fixedAmount, 0), gross);

    /// <summary>Total de línea = bruto − descuento efectivo (el llamador valida que no sea negativo).</summary>
    public static decimal LineTotal(decimal unitPrice, decimal quantity, decimal discount) =>
        Money.Round(unitPrice * quantity - discount);

    /// <summary>Totales del ticket agregados sobre el subtotal neto (Σ totales de línea).</summary>
    public static CartTotals ComputeTotals(decimal subtotalNet, decimal globalDiscount)
    {
        var exceeds = globalDiscount > subtotalNet;
        // Si el descuento global supera el subtotal, el total visible se fija en 0
        // y el flag permite al llamador bloquear (la venta real lo rechaza).
        var total = Math.Max(Money.Round(subtotalNet - globalDiscount), 0m);
        var itbis = Money.Round(total * ItbisRate / (1 + ItbisRate));
        var baseImponible = Money.Round(total - itbis);
        return new CartTotals(subtotalNet, exceeds, total, itbis, baseImponible);
    }

    /// <summary>ITBIS contenido en un total que YA lo incluye (precio retail RD).</summary>
    public static Money ItbisFromTotalIncluded(Money total) =>
        Money.Round(total.Amount * ItbisRate / (1 + ItbisRate));
}

/// <summary>Resultado del cálculo de totales de un ticket (subtotal ya neto de descuentos de línea).</summary>
public readonly record struct CartTotals(
    decimal SubtotalNet,
    bool DiscountExceedsSubtotal,
    decimal Total,
    decimal Itbis,
    decimal BaseImponible);
