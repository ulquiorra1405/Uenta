using POS.Application.Abstractions;

namespace POS.Infrastructure.Services;

public class SystemClock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.Now;
}
