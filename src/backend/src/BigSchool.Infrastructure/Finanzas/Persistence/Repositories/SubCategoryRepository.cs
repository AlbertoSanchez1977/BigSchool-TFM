using BigSchool.Application.Finanzas.Interfaces.Repositories;
using BigSchool.Domain.Finanzas.Entities;
using BigSchool.Domain.Finanzas.Enums;
using BigSchool.Infrastructure.SharedKernel.Persistence;
using BigSchool.Infrastructure.SharedKernel.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BigSchool.Infrastructure.Finanzas.Persistence.Repositories;

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
