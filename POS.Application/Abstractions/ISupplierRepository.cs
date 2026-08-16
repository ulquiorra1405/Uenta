using POS.Domain.Entities;

namespace POS.Application.Abstractions;

public interface ISupplierRepository
{
    Task<List<Supplier>> GetAllAsync(CancellationToken ct = default);
    Task<Supplier?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<bool> RncExistsAsync(string rnc, long? excludeId = null, CancellationToken ct = default);
    Task<long> AddAsync(Supplier supplier, CancellationToken ct = default);
    Task UpdateAsync(Supplier supplier, CancellationToken ct = default);
}