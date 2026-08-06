using Microsoft.EntityFrameworkCore;
using POS.Domain.Entities;
using POS.Domain.ValueObjects;

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
