namespace BigSchool.Domain.SharedKernel.Interfaces;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(bool dispatchEvents = true);
}
