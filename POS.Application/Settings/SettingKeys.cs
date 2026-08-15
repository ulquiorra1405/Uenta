namespace POS.Application.Settings;

/// <summary>Claves de los ajustes persistentes (tabla <c>Setting</c>, clave/valor).</summary>
public static class SettingKeys
{
    /// <summary>Nombre de la impresora térmica seleccionada (Fase 1A).</summary>
    public const string PrinterName = "Printer.Name";

    /// <summary>Imprimir el recibo automáticamente al completar la venta ("1"/"0").</summary>
    public const string AutoPrint = "Printer.AutoPrint";

    /// <summary>Nº de copias del recibo (1-9).</summary>
    public const string Copies = "Printer.Copies";

    /// <summary>Nombre del negocio que aparece en el encabezado del recibo.</summary>
    public const string BusinessName = "Business.Name";

    /// <summary>RNC del negocio (si aplica).</summary>
    public const string BusinessRnc = "Business.Rnc";

    /// <summary>Dirección del negocio.</summary>
    public const string BusinessAddress = "Business.Address";

    /// <summary>Pie de recibo (mensaje de agradecimiento/personalizado).</summary>
    public const string ReceiptFooter = "Receipt.Footer";

    /// <summary>Tope de descuento global (%) para el rol Cajero (Fase 1B, P2.1d).</summary>
    public const string DiscountLimitCajero = "Discount.Limit.Cajero";

    /// <summary>Tope de descuento global (%) para el rol Supervisor (Fase 1B, P2.1d).</summary>
    public const string DiscountLimitSupervisor = "Discount.Limit.Supervisor";
}

/// <summary>Datos de negocio + preferencias de impresión listos para el recibo.</summary>
public sealed record ReceiptSettingsDto
{
    public string PrinterName { get; init; } = string.Empty;
    public bool AutoPrint { get; init; } = true;
    public int Copies { get; init; } = 1;
    public string BusinessName { get; init; } = string.Empty;
    public string BusinessRnc { get; init; } = string.Empty;
    public string BusinessAddress { get; init; } = string.Empty;
    public string ReceiptFooter { get; init; } = string.Empty;
}