using Microsoft.EntityFrameworkCore;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.ValueObjects;
using POS.Infrastructure.Services;

namespace POS.Infrastructure.Data;

/// <summary>
/// Aplica migraciones y siembra datos demo (solo si la base está vacía).
/// Los datos reales los crea el usuario desde el catálogo.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(PosDbContext db)
    {
        await db.Database.MigrateAsync();

        // Secuencia de numeración: siembra la fila única si no existe.
        if (!await db.Sequences.AnyAsync())
        {
            db.Sequences.Add(new Sequence { Id = 1, LastNumber = 0 });
            await db.SaveChangesAsync();
        }

        // Usuarios demo (P2.1a). Contraseñas por defecto documentadas en PLAN.md;
        // la gestión real (cambio/reset) llega con P2.1e (solo Admin).
        var hasher = new PasswordHasher();
        if (!await db.Users.AnyAsync())
        {
            db.Users.AddRange(
                new User
                {
                    Username = "admin",
                    DisplayName = "Administrador",
                    PasswordHash = hasher.Hash("admin123"),
                    Role = UserRole.Admin,
                    IsActive = true,
                    CreatedAt = DateTimeOffset.Now
                },
                new User
                {
                    Username = "supervisor",
                    DisplayName = "Supervisor",
                    PasswordHash = hasher.Hash("super123"),
                    Role = UserRole.Supervisor,
                    IsActive = true,
                    CreatedAt = DateTimeOffset.Now
                },
                new User
                {
                    Username = "cajero",
                    DisplayName = "Cajero",
                    PasswordHash = hasher.Hash("cajero123"),
                    Role = UserRole.Cajero,
                    IsActive = true,
                    CreatedAt = DateTimeOffset.Now
                });
            await db.SaveChangesAsync();
        }

        if (!await db.Categories.AnyAsync())
        {
            db.Categories.AddRange(
                new Category { Name = "Bebidas" },
                new Category { Name = "Panadería" },
                new Category { Name = "Snacks" });

            // Persistir ANTES de usarlas en el bloque de productos
            // (sin SaveChanges, FirstAsync no las encuentra → error en base limpia).
            await db.SaveChangesAsync();
        }

        if (!await db.Products.AnyAsync())
        {
            var bebidas = await db.Categories.FirstAsync(c => c.Name == "Bebidas");
            var panaderia = await db.Categories.FirstAsync(c => c.Name == "Panadería");
            var snacks = await db.Categories.FirstAsync(c => c.Name == "Snacks");

            db.Products.AddRange(
                new Product { Name = "Café con leche", Sku = "CAF-001", CategoryId = bebidas.Id, Price = new Money(100m), Cost = new Money(35m), Stock = 10, MinStock = 5 },
                new Product { Name = "Jugo de naranja", Sku = "JGO-001", Barcode = "8400000000017", CategoryId = bebidas.Id, Price = new Money(80m), Cost = new Money(30m), Stock = 20, MinStock = 8 },
                new Product { Name = "Refresco cola 400ml", Sku = "REF-001", Barcode = "8400000000024", CategoryId = bebidas.Id, Price = new Money(45m), Cost = new Money(20m), Stock = 40, MinStock = 12 },
                new Product { Name = "Pan de agua", Sku = "PAN-001", CategoryId = panaderia.Id, Price = new Money(25m), Cost = new Money(8m), Stock = 50, MinStock = 10 },
                new Product { Name = "Empanada de pollo", Sku = "EMP-001", CategoryId = panaderia.Id, Price = new Money(60m), Cost = new Money(25m), Stock = 30, MinStock = 10 },
                new Product { Name = "Dulce de leche", Sku = "DL-001", CategoryId = snacks.Id, Price = new Money(35m), Cost = new Money(12m), Stock = 15, MinStock = 5 });
        }

        await db.SaveChangesAsync();
    }
}
