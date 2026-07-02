using BigSchool.Domain.Investments.Entities;
using Microsoft.EntityFrameworkCore;
using BigSchool.Application.Investments.Interfaces.Repositories;
using BigSchool.Infrastructure.SharedKernel.Persistence;
using BigSchool.Infrastructure.SharedKernel.Persistence.Repositories;

namespace BigSchool.Infrastructure.Investments.Persistence.Repositories;

public class PortfolioRepository : EFRepository<Portfolio, int>, IPortfolioRepository
{
    public PortfolioRepository(BigSchoolDbContext context) : base(context)
    {
    }

    public async Task<Portfolio?> GetByIdWithHoldingsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await Context.Portfolios
            .Include(p => p.Holdings)
                .ThenInclude(h => h.Disposals)
            .FirstOrDefaultAsync(p => p.IdPortfolio == id, cancellationToken);
    }
}
