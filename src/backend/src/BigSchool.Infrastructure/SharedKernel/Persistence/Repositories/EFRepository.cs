using BigSchool.Domain.SharedKernel.Entities;
using BigSchool.Domain.SharedKernel.Interfaces;
using BigSchool.Application.SharedKernel.Interfaces;

namespace BigSchool.Infrastructure.SharedKernel.Persistence.Repositories;

public abstract class EFRepository<T, Y> : IRepository<T, Y> where T : class, IAggregateRoot
{
    protected readonly BigSchoolDbContext Context;

    protected EFRepository(BigSchoolDbContext context)
    {
        Context = context;
    }

    public IUnitOfWork UnitOfWork => Context;

    public virtual async Task<T?> GetByIdAsync(Y id, CancellationToken cancellationToken = default)
    {
        return await Context.Set<T>().FindAsync([id], cancellationToken);
    }

    public virtual async Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await Context.Set<T>().AddAsync(entity, cancellationToken);
    }

    public virtual async Task AddRangeAsync(List<T> entities, CancellationToken cancellationToken = default)
    {
        await Context.Set<T>().AddRangeAsync(entities, cancellationToken);
    }
}
