namespace POS.Domain.Entities;

/// <summary>
/// Par de clave/valor de configuración de la aplicación (Ajustes).
/// Toda preferencia persistente (impresora, datos del negocio, copias, etc.)
/// vive aquí: sin tablas nuevas por cada opción (Fase 1A, P1.3).
/// </summary>
public class Setting
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}