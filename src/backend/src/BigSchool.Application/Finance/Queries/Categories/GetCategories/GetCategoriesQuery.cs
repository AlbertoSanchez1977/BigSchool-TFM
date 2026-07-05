using MediatR;

namespace BigSchool.Application.Finance.Queries.Categories.GetCategories;

public record GetCategoriesQuery(int IdUser) : IRequest<IReadOnlyList<CategoryDto>>;
