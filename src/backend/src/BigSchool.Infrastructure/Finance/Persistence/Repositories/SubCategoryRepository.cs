using BigSchool.Application.Finance.Interfaces.Repositories;
using BigSchool.Domain.Finance.Entities;
using BigSchool.Domain.Finance.Enums;
using BigSchool.Infrastructure.SharedKernel.Persistence;
using BigSchool.Infrastructure.SharedKernel.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BigSchool.Infrastructure.Finance.Persistence.Repositories;

public class SubCategoryRepository : EFRepository<SubCategory, int>, ISubCategoryRepository
{
    public SubCategoryRepository(BigSchoolDbContext context) : base(context) { }

    public async Task<bool> ExistsActiveAsync(int? idUser, MainCategory mainCategory, string name, CancellationToken cancellationToken = default)
    {
        var trimmed = name.Trim().ToLower();
        // El global query filter ya excluye IdStatus=Deleted → "activa".
        return await Context.Set<SubCategory>().AnyAsync(s =>
            s.IdMainCategory == mainCategory
            && s.Name.ToLower() == trimmed
            && (s.IdUser == null || s.IdUser == idUser), cancellationToken);
    }
}
