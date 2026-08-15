using POS.Domain.Entities;

namespace POS.Application.Abstractions;

/// <summary>Repositorio de usuarios (P2.1).</summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default);
    Task<List<User>> GetAllAsync(CancellationToken ct = default);
    Task<bool> UsernameExistsAsync(string username, CancellationToken ct = default);
    Task<long> AddAsync(User user, CancellationToken ct = default);
    Task UpdateAsync(User user, CancellationToken ct = default);
}