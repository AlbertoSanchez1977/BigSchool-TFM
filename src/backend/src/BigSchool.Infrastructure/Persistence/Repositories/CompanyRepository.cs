using BigSchool.Domain.Investments.Entities;
using Microsoft.EntityFrameworkCore;
using BigSchool.Application.Investments.Interfaces.Repositories;

namespace BigSchool.Infrastructure.Persistence.Repositories;
public class CompanyRepository : EFRepository<Company, int>, ICompanyRepository
{
    public CompanyRepository(BigSchoolDbContext context) : base(context)
    {
    }

    public async Task<Company?> GetByIdWithValuationsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await Context.Companies
            .Include(c => c.Valuations)
            .FirstOrDefaultAsync(c => c.IdCompany == id, cancellationToken);
    }

    public async Task<Company?> GetByTickerAsync(string ticker, CancellationToken cancellationToken = default)
    {
        var normalized = ticker.Trim().ToUpperInvariant();
        return await Context.Companies.FirstOrDefaultAsync(c => c.Ticker == normalized, cancellationToken);
    }
}
