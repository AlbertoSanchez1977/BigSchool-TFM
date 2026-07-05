using BigSchool.Application.SharedKernel.Interfaces;
using BigSchool.Domain.Finance.Entities;
using BigSchool.Domain.Finance.Enums;

namespace BigSchool.Application.Finance.Interfaces.Repositories;

public interface ISubCategoryRepository : IRepository<SubCategory, int>
{
    // Existe una activa con ese nombre+categoría entre las del usuario O las globales.
    Task<bool> ExistsActiveAsync(int? idUser, MainCategory mainCategory, string name, CancellationToken cancellationToken = default);
}
