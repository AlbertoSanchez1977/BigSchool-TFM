using MediatR;

namespace BigSchool.Application.Finanzas.Queries.Categories.GetCategories;

public record GetCategoriesQuery(int IdUser) : IRequest<IReadOnlyList<CategoryDto>>;
