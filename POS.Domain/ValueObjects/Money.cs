namespace POS.Domain.ValueObjects;

/// <summary>
/// Dinero en RD$ (Peso Dominicano). Precisión decimal de 2 dígitos, siempre.
/// Todas las operaciones redondean con MidpointRounding.AwayFromZero.
/// Regla dura: nunca usar double/float para dinero.
/// </summary>
public readonly record struct Money(decimal Amount)
{
    public static Money Zero => new(0m);

    public static Money operator +(Money a, Money b) => new(Round(a.Amount + b.Amount));
    public static Money operator -(Money a, Money b) => new(Round(a.Amount - b.Amount));
    public static Money operator *(Money a, decimal factor) => new(Round(a.Amount * factor));
    public static Money operator /(Money a, decimal divisor) => new(Round(a.Amount / divisor));

    public static implicit operator Money(decimal amount) => new(amount);
    public static implicit operator decimal(Money money) => money.Amount;

    public static Money Round(decimal amount) => new(Math.Round(amount, 2, MidpointRounding.AwayFromZero));

    public override string ToString() => Amount.ToString("N2");
}
