using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using POS.Domain.Entities;
using POS.Domain.ValueObjects;

namespace POS.Infrastructure.Data;

public class PosDbContext : DbContext
{
    public PosDbContext(DbContextOptions<PosDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Sequence> Sequences => Set<Sequence>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var money = new ValueConverter<Money, decimal>(
            m => m.Amount,
            d => new Money(d));

        modelBuilder.Entity<Category>(e =>
        {
            e.Property(c => c.Name).HasMaxLength(120).IsRequired();
        });

        modelBuilder.Entity<Product>(e =>
        {
            e.Property(p => p.Name).HasMaxLength(200).IsRequired();
            e.Property(p => p.Sku).HasMaxLength(50);
            e.Property(p => p.Barcode).HasMaxLength(50);
            e.Property(p => p.Price).HasConversion(money).HasColumnType("decimal(18,2)");
            e.Property(p => p.Cost).HasConversion(money).HasColumnType("decimal(18,2)");
            e.Property(p => p.Stock).HasColumnType("decimal(18,2)");
            e.Property(p => p.MinStock).HasColumnType("decimal(18,2)");
            e.HasIndex(p => p.Barcode);
            e.HasIndex(p => p.Sku);
        });

        modelBuilder.Entity<Sale>(e =>
        {
            e.Property(s => s.Subtotal).HasConversion(money).HasColumnType("decimal(18,2)");
            e.Property(s => s.Itbis).HasConversion(money).HasColumnType("decimal(18,2)");
            e.Property(s => s.Discount).HasConversion(money).HasColumnType("decimal(18,2)");
            e.Property(s => s.Total).HasConversion(money).HasColumnType("decimal(18,2)");
            e.Property(s => s.Status).HasConversion<int>();
            e.HasIndex(s => s.Number).IsUnique();
        });

        modelBuilder.Entity<SaleItem>(e =>
        {
            e.Property(i => i.ProductName).HasMaxLength(200).IsRequired();
            e.Property(i => i.UnitPrice).HasConversion(money).HasColumnType("decimal(18,2)");
            e.Property(i => i.LineDiscount).HasConversion(money).HasColumnType("decimal(18,2)");
            e.Property(i => i.Total).HasConversion(money).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<Payment>(e =>
        {
            e.Property(p => p.Method).HasConversion<int>();
            e.Property(p => p.Amount).HasConversion(money).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<Sequence>(e =>
        {
            e.ToTable("Sequences");
            e.HasKey(s => s.Id);
            e.Property(s => s.LastNumber).IsRequired();
        });

        modelBuilder.Entity<StockMovement>(e =>
        {
            e.ToTable("StockMovements");
            e.Property(m => m.Reason).HasMaxLength(200).IsRequired();
            e.Property(m => m.Quantity).HasColumnType("decimal(18,2)");
            e.Property(m => m.StockAfter).HasColumnType("decimal(18,2)");
            e.Property(m => m.Type).HasConversion<int>();
            e.HasIndex(m => m.ProductId);
        });
    }
}
