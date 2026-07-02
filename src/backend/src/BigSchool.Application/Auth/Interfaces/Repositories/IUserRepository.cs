using BigSchool.Domain.Auth.Entities;
using BigSchool.Application.SharedKernel.Interfaces;

namespace BigSchool.Application.Auth.Interfaces.Repositories;

public interface IUserRepository : IRepository<User, int>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> ExistsWithEmailAsync(string email, CancellationToken cancellationToken = default);
}
