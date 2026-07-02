using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Domain.Auth.Entities;
using Microsoft.EntityFrameworkCore;

namespace BigSchool.Infrastructure.Persistence.Repositories;

public class UserRepository : EFRepository<User, int>, IUserRepository
{
    public UserRepository(BigSchoolDbContext context) : base(context) { }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return await Context.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);
    }

    public async Task<bool> ExistsWithEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return await Context.Users
            .AnyAsync(u => u.Email == normalizedEmail, cancellationToken);
    }
}
