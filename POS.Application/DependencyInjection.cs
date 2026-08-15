using Microsoft.Extensions.DependencyInjection;
using POS.Application.Products;
using POS.Application.Sales;

namespace POS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<SaleService>();
        services.AddScoped<ProductService>();
        services.AddScoped<CategoryService>();
        services.AddScoped<InventoryService>();
        return services;
    }
}
