using BigSchool.Domain.Finanzas.Enums;
using BigSchool.Domain.SharedKernel.Enums;
using Dapper;
using MediatR;
using BigSchool.Application.SharedKernel.Interfaces;

namespace BigSchool.Application.Finanzas.Queries.Categories.GetCategories;

public class GetCategoriesQueryHandler : IRequestHandler<GetCategoriesQuery, IReadOnlyList<CategoryDto>>
{
    private readonly IDbConnectionFactory _dbFactory;

    public GetCategoriesQueryHandler(IDbConnectionFactory dbFactory) => _dbFactory = dbFactory;

    private const string GETCATEGORIES_QUERY = @"SELECT IdSubCategory, IdMainCategory, Name, IsDefault
                                                 FROM SubCategories
                                                 WHERE IdStatus <> @StatusDeleted
                                                   AND (IdUser IS NULL OR IdUser = @IdUser)
                                                 ORDER BY IdMainCategory, Name;";

    public async Task<IReadOnlyList<CategoryDto>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@IdUser", request.IdUser);
        parameters.Add("@StatusDeleted", EntityStatus.Deleted);

        using var conn = _dbFactory.CreateConnection();
        var rows = (await conn.QueryAsync<(int IdSubCategory, int IdMainCategory, string Name, bool IsDefault)>(
            GETCATEGORIES_QUERY, parameters)).ToList();

        return Enum.GetValues<MainCategory>()
            .Select(mc => new CategoryDto(
                (int)mc,
                mc.ToString(),
                rows.Where(r => r.IdMainCategory == (int)mc)
                    .Select(r => new SubCategoryDto(r.IdSubCategory, r.Name, r.IsDefault))
                    .ToList()))
            .ToList();
    }
}
