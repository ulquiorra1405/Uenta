using Microsoft.Extensions.DependencyInjection;
using POS.Application.Auth;
using POS.Application.Cash;
using POS.Application.Customers;
using POS.Application.Products;
using POS.Application.Reports;
using POS.Application.Refunds;
using POS.Application.Sales;
using POS.Application.Settings;

namespace POS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<SaleService>();
        services.AddScoped<ProductService>();
        services.AddScoped<CategoryService>();
        services.AddScoped<InventoryService>();
        services.AddScoped<SettingsService>();
        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        services.AddScoped<AuditService>();
        services.AddScoped<CashSessionService>();
        services.AddScoped<CustomerService>();
        services.AddScoped<ReportService>();
        services.AddScoped<RefundService>();
        return services;
    }
}
