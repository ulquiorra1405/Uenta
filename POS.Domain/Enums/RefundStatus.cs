namespace POS.Domain.Enums;

/// <summary>Estado de una devolución (P5.1). Por ahora solo se usa Completed.</summary>
public enum RefundStatus
{
    Completed = 1,
    Cancelled = 2
}