namespace POS.Domain.Entities;

/// <summary>
/// Cliente (CRM básico, P4.1). La venta puede asociarse a un cliente
/// (<see cref="Sale.CustomerId"/>); sin cliente queda como "Anónimo".
/// </summary>
public class Customer
{
    public long Id { get; set; }

    /// <summary>Nombre o razón social (obligatorio).</summary>
    public string Name { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    /// <summary>RNC o cédula (opcional, único si se registra).</summary>
    public string RncCedula { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public List<Sale> Sales { get; set; } = [];
}