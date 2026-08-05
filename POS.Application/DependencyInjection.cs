using Microsoft.Extensions.DependencyInjection;
using POS.Application.Sales;

namespace POS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<SaleService>();
        return services;
    }
}
