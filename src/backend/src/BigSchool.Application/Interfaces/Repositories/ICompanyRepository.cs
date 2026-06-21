using BigSchool.Domain.Entities;

namespace BigSchool.Application.Interfaces.Repositories;
public interface ICompanyRepository : IRepository<Company, int>
{
    Task<Company?> GetByTickerAsync(string ticker, CancellationToken cancellationToken = default);
    Task<Company?> GetByIdWithValuationsAsync(int id, CancellationToken cancellationToken = default);
}
