namespace POS.Domain.Entities;

/// <summary>
/// Proveedor (P5.2): dato maestro reutilizable para registrar compras que
/// reponen stock y registran el costo real del producto.
/// </summary>
public class Supplier
{
    public long Id { get; set; }

    /// <summary>Nombre o razón social (obligatorio).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>RNC (opcional, único si se registra).</summary>
    public string Rnc { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public List<Purchase> Purchases { get; set; } = [];
}