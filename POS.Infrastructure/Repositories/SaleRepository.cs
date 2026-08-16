using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Domain.Entities;
using POS.Infrastructure.Data;

namespace POS.Infrastructure.Repositories;

public class SaleRepository : ISaleRepository
{
    private readonly PosDbContext _db;

    public SaleRepository(PosDbContext db) => _db = db;

    /// <summary>
    /// Persiste la venta en una transacción junto con la numeración atómica
    /// (UPSERT+RETURNING sobre la tabla Sequences) y TODOS los cambios pendientes
    /// del contexto (incluye el stock descontado en los productos). Si algo falla,
    /// se revierte todo y NO se quema un número de recibo.
    /// </summary>
    public async Task<long> AddAsync(Sale sale, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // Incremento atómico: SQLite serializa los escritores; RETURNING devuelve
        // el nuevo valor sin condición de carrera (a diferencia de MaxAsync + 1).
        var sql = """
            INSERT INTO Sequences(Id, LastNumber) VALUES (1, 1)
            ON CONFLICT(Id) DO UPDATE SET LastNumber = LastNumber + 1
            RETURNING LastNumber;
            """;
        var numbers = await _db.Database
            .SqlQueryRaw<long>(sql)
            .ToListAsync(ct);

        sale.Number = numbers.Single();

        _db.Sales.Add(sale);
        await _db.SaveChangesAsync(ct); // venta + stock pendiente, dentro de la transacción
        await tx.CommitAsync(ct);

        return sale.Id;
    }

    /// <summary>Venta con items y pagos cargados (para devoluciones, P5.1).</summary>
    public async Task<Sale?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return await _db.Sales
            .Include(s => s.Items)
            .Include(s => s.Payments)
            .Include(s => s.User)
            .Include(s => s.Customer)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    /// <summary>Busca la venta por su número de recibo (devolución con recibo, P5.1).</summary>
    public async Task<Sale?> GetByNumberAsync(long number, CancellationToken ct = default)
    {
        return await _db.Sales
            .Include(s => s.Items)
            .Include(s => s.Payments)
            .Include(s => s.User)
            .Include(s => s.Customer)
            .FirstOrDefaultAsync(s => s.Number == number, ct);
    }
}