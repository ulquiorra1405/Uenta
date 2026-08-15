namespace POS.Domain.Enums;

/// <summary>Tipo de movimiento de inventario (P3.2).</summary>
public enum StockMovementType
{
    /// <summary>Entrada de stock (compra, devolución, ingreso). Suma.</summary>
    Entry = 1,

    /// <summary>Salida de stock (venta, merma, uso interno). Resta.</summary>
    Exit = 2,

    /// <summary>Ajuste: fija el stock al valor declarado (conteo físico, corrección).</summary>
    Adjustment = 3
}