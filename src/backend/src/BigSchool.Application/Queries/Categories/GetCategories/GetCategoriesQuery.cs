using MediatR;

namespace BigSchool.Application.Queries.Categories.GetCategories;

public record GetCategoriesQuery(int IdUser) : IRequest<IReadOnlyList<CategoryDto>>;
