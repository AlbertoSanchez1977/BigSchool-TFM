using BigSchool.Domain.Investments.Entities;
using BigSchool.Application.SharedKernel.Interfaces;

namespace BigSchool.Application.Investments.Interfaces.Repositories;
public interface ICompanyRepository : IRepository<Company, int>
{
    Task<Company?> GetByTickerAsync(string ticker, CancellationToken cancellationToken = default);
    Task<Company?> GetByIdWithValuationsAsync(int id, CancellationToken cancellationToken = default);
}
