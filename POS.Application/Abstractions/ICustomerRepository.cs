using POS.Domain.Entities;

namespace POS.Application.Abstractions;

public interface ICustomerRepository
{
    Task<List<Customer>> GetAllAsync(CancellationToken ct = default);
    Task<Customer?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<bool> RncCedulaExistsAsync(string rncCedula, long? excludeId = null, CancellationToken ct = default);
    Task<long> AddAsync(Customer customer, CancellationToken ct = default);
    Task UpdateAsync(Customer customer, CancellationToken ct = default);
    Task<List<Sale>> GetSalesAsync(long customerId, CancellationToken ct = default);
}