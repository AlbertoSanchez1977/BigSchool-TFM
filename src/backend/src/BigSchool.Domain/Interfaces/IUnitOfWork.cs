namespace BigSchool.Domain.Interfaces;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(bool dispatchEvents = true, CancellationToken cancellationToken = default);
}
