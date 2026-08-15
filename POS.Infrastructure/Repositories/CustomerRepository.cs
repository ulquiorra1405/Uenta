using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Domain.Entities;
using POS.Infrastructure.Data;

namespace POS.Infrastructure.Repositories;

public class CustomerRepository : ICustomerRepository
{
    private readonly PosDbContext _db;

    public CustomerRepository(PosDbContext db) => _db = db;

    public Task<List<Customer>> GetAllAsync(CancellationToken ct = default)
        => _db.Customers.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct);

    public Task<Customer?> GetByIdAsync(long id, CancellationToken ct = default)
        => _db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<bool> RncCedulaExistsAsync(string rncCedula, long? excludeId = null, CancellationToken ct = default)
    {
        var query = _db.Customers.AsNoTracking().Where(c => c.RncCedula == rncCedula && c.RncCedula != "");
        if (excludeId is { } id)
            query = query.Where(c => c.Id != id);
        return query.AnyAsync(ct);
    }

    public async Task<long> AddAsync(Customer customer, CancellationToken ct = default)
    {
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync(ct);
        return customer.Id;
    }

    public async Task UpdateAsync(Customer customer, CancellationToken ct = default)
    {
        _db.Customers.Update(customer);
        await _db.SaveChangesAsync(ct);
    }

    public Task<List<Sale>> GetSalesAsync(long customerId, CancellationToken ct = default)
        => _db.Sales
            .AsNoTracking()
            .Include(s => s.User)
            .Include(s => s.Items)
            .Where(s => s.CustomerId == customerId)
            .ToListAsync(ct);
}