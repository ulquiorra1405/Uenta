namespace POS.Domain.Entities;

/// <summary>
/// Contador de secuencia del negocio (una sola fila, Id=1). La numeración de
/// ventas se incrementa de forma atómica (UPSERT con RETURNING) en la MISMA
/// transacción que inserta la venta: números consecutivos sin condición de
/// carrera ni huecos por fallos parciales.
/// </summary>
public class Sequence
{
    public long Id { get; set; }
    public long LastNumber { get; set; }
}