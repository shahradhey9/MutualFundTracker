using GWT.Application.Interfaces.Repositories;
using GWT.Domain.Entities;
using GWT.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GWT.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly GwtDbContext _db;

    public UserRepository(GwtDbContext db) => _db = db;

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Users.FindAsync([id], ct).AsTask();

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    public async Task<User> CreateAsync(User user, CancellationToken ct = default)
    {
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        return user;
    }
}
