using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.Abstractions;
using POS.Infrastructure.Data;
using POS.Infrastructure.Repositories;
using POS.Infrastructure.Services;

namespace POS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<PosDbContext>(o => o.UseSqlite(connectionString));

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ISaleRepository, SaleRepository>();
        services.AddScoped<IStockMovementRepository, StockMovementRepository>();
        services.AddScoped<ISettingRepository, SettingRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<ICashSessionRepository, CashSessionRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IClock, SystemClock>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ICurrentSession, CurrentSession>();
        services.AddScoped<IReceiptPrinter, ThermalReceiptPrinter>();
        services.AddScoped<ReceiptPdfGenerator>();
        services.AddScoped<IDatabaseBackupService, DatabaseBackupService>();

        return services;
    }
}
