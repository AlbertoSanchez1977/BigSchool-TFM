using BigSchool.Domain.SharedKernel.Entities;
using BigSchool.Domain.SharedKernel.Interfaces;

namespace BigSchool.Application.Interfaces;

public interface IRepository<T, in Y> where T : IAggregateRoot
{
    IUnitOfWork UnitOfWork { get; }

    Task<T?> GetByIdAsync(Y id, CancellationToken cancellationToken = default);
    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    Task AddRangeAsync(List<T> entities, CancellationToken cancellationToken = default);
}
