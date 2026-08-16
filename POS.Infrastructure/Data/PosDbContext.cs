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
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<User> Users => Set<User>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<CashSession> CashSessions => Set<CashSession>();
    public DbSet<CashWithdrawal> CashWithdrawals => Set<CashWithdrawal>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<RefundItem> RefundItems => Set<RefundItem>();
    public DbSet<RefundPayment> RefundPayments => Set<RefundPayment>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();

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
            e.HasOne(p => p.Sale).WithMany(s => s.Payments).HasForeignKey(p => p.SaleId);
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

        modelBuilder.Entity<Setting>(e =>
        {
            e.ToTable("Settings");
            e.HasKey(s => s.Key);
            e.Property(s => s.Key).HasMaxLength(100).IsRequired();
            e.Property(s => s.Value).HasMaxLength(500);
        });

        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.Property(u => u.Username).HasMaxLength(50).IsRequired();
            e.Property(u => u.DisplayName).HasMaxLength(120).IsRequired();
            e.Property(u => u.PasswordHash).HasMaxLength(200).IsRequired();
            e.Property(u => u.Role).HasConversion<int>();
            e.HasIndex(u => u.Username).IsUnique();
        });

        modelBuilder.Entity<AuditLog>(e =>
        {
            e.ToTable("AuditLogs");
            e.Property(a => a.Username).HasMaxLength(50);
            e.Property(a => a.Detail).HasMaxLength(500);
            e.Property(a => a.Action).HasConversion<int>();
            e.HasIndex(a => a.CreatedAt);
        });

        modelBuilder.Entity<CashSession>(e =>
        {
            e.ToTable("CashSessions");
            e.Property(s => s.InitialCash).HasColumnType("decimal(18,2)");
            e.Property(s => s.FinalCount).HasColumnType("decimal(18,2)");
            e.Property(s => s.Difference).HasColumnType("decimal(18,2)");
            e.Property(s => s.Status).HasConversion<int>();
            e.HasIndex(s => new { s.UserId, s.Status });
            e.HasOne(s => s.User).WithMany().HasForeignKey(s => s.UserId);
        });

        modelBuilder.Entity<CashWithdrawal>(e =>
        {
            e.ToTable("CashWithdrawals");
            e.Property(w => w.Amount).HasColumnType("decimal(18,2)");
            e.Property(w => w.Reason).HasMaxLength(200).IsRequired();
            e.HasOne(w => w.CashSession).WithMany(s => s.Withdrawals).HasForeignKey(w => w.CashSessionId);
        });

        modelBuilder.Entity<Customer>(e =>
        {
            e.ToTable("Customers");
            e.Property(c => c.Name).HasMaxLength(200).IsRequired();
            e.Property(c => c.Phone).HasMaxLength(30);
            e.Property(c => c.RncCedula).HasMaxLength(20);
            e.Property(c => c.Email).HasMaxLength(150);
            e.HasIndex(c => c.RncCedula);
        });

        modelBuilder.Entity<Sale>()
            .HasOne(s => s.Customer)
            .WithMany(c => c.Sales)
            .HasForeignKey(s => s.CustomerId)
            .IsRequired(false);

        modelBuilder.Entity<Sale>()
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .IsRequired();

        modelBuilder.Entity<Refund>(e =>
        {
            e.ToTable("Refunds");
            e.Property(r => r.Reason).HasMaxLength(200);
            e.Property(r => r.Total).HasConversion(money).HasColumnType("decimal(18,2)");
            e.Property(r => r.Status).HasConversion<int>();
            e.HasIndex(r => r.Number).IsUnique();
            e.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId).IsRequired();
            e.HasOne(r => r.OriginalSale).WithMany().HasForeignKey(r => r.OriginalSaleId).IsRequired(false);
        });

        modelBuilder.Entity<RefundItem>(e =>
        {
            e.ToTable("RefundItems");
            e.Property(i => i.ProductName).HasMaxLength(200).IsRequired();
            e.Property(i => i.Quantity).HasColumnType("decimal(18,2)");
            e.Property(i => i.UnitPrice).HasConversion(money).HasColumnType("decimal(18,2)");
            e.Property(i => i.Total).HasConversion(money).HasColumnType("decimal(18,2)");
            e.HasOne(i => i.Refund).WithMany(r => r.Items).HasForeignKey(i => i.RefundId);
        });

        modelBuilder.Entity<RefundPayment>(e =>
        {
            e.ToTable("RefundPayments");
            e.Property(p => p.Method).HasConversion<int>();
            e.Property(p => p.Amount).HasConversion(money).HasColumnType("decimal(18,2)");
            e.HasOne(p => p.Refund).WithMany(r => r.Payments).HasForeignKey(p => p.RefundId);
        });

        modelBuilder.Entity<Supplier>(e =>
        {
            e.ToTable("Suppliers");
            e.Property(s => s.Name).HasMaxLength(200).IsRequired();
            e.Property(s => s.Rnc).HasMaxLength(20);
            e.Property(s => s.Phone).HasMaxLength(30);
            e.HasIndex(s => s.Rnc);
        });

        modelBuilder.Entity<Purchase>(e =>
        {
            e.ToTable("Purchases");
            e.Property(p => p.Total).HasConversion(money).HasColumnType("decimal(18,2)");
            e.HasIndex(p => p.Number).IsUnique();
            e.HasOne(p => p.User).WithMany().HasForeignKey(p => p.UserId).IsRequired();
            e.HasOne(p => p.Supplier).WithMany(s => s.Purchases).HasForeignKey(p => p.SupplierId).IsRequired(false);
        });

        modelBuilder.Entity<PurchaseItem>(e =>
        {
            e.ToTable("PurchaseItems");
            e.Property(i => i.ProductName).HasMaxLength(200).IsRequired();
            e.Property(i => i.Quantity).HasColumnType("decimal(18,2)");
            e.Property(i => i.UnitCost).HasConversion(money).HasColumnType("decimal(18,2)");
            e.Property(i => i.Total).HasConversion(money).HasColumnType("decimal(18,2)");
            e.HasOne(i => i.Purchase).WithMany(p => p.Items).HasForeignKey(i => i.PurchaseId);
        });
    }
}
