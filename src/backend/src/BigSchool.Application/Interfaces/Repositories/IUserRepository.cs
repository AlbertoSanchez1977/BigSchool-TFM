using BigSchool.Domain.Entities;

namespace BigSchool.Application.Interfaces.Repositories;

public interface IUserRepository : IRepository<User, int>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> ExistsWithEmailAsync(string email, CancellationToken cancellationToken = default);
}
