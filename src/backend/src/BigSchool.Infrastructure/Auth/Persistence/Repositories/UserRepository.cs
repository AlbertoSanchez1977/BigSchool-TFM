using BigSchool.Domain.Auth.Entities;
using Microsoft.EntityFrameworkCore;
using BigSchool.Application.Auth.Interfaces.Repositories;
using BigSchool.Infrastructure.SharedKernel.Persistence;
using BigSchool.Infrastructure.SharedKernel.Persistence.Repositories;

namespace BigSchool.Infrastructure.Auth.Persistence.Repositories;

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
