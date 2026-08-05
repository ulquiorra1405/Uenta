using POS.Domain.Enums;
using POS.Domain.ValueObjects;

namespace POS.Domain.Entities;

public class Payment
{
    public long Id { get; set; }
    public long SaleId { get; set; }
    public Sale Sale { get; set; } = null!;

    public PaymentMethod Method { get; set; }
    public Money Amount { get; set; }
}
