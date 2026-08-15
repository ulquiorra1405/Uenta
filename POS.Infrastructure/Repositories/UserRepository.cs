using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Domain.Entities;
using POS.Infrastructure.Data;

namespace POS.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly PosDbContext _db;

    public UserRepository(PosDbContext db) => _db = db;

    public Task<User?> GetByIdAsync(long id, CancellationToken ct = default)
        => _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
        => _db.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower(), ct);

    public async Task<List<User>> GetAllAsync(CancellationToken ct = default)
        => await _db.Users.AsNoTracking().OrderBy(u => u.Username).ToListAsync(ct);

    public Task<bool> UsernameExistsAsync(string username, CancellationToken ct = default)
        => _db.Users.AnyAsync(u => u.Username.ToLower() == username.ToLower(), ct);

    public async Task<long> AddAsync(User user, CancellationToken ct = default)
    {
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        return user.Id;
    }

    public async Task UpdateAsync(User user, CancellationToken ct = default)
    {
        _db.Users.Update(user);
        await _db.SaveChangesAsync(ct);
    }
}