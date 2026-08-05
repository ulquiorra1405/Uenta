namespace POS.Application.Abstractions;

/// <summary>
/// Puerta de salida para el tiempo. Se inyecta para que los casos de uso sean
/// testeables (reloj falso en tests) y consistentes en toda la app.
/// </summary>
public interface IClock
{
    DateTimeOffset Now { get; }
}
