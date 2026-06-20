namespace BigSchool.Application.Queries.Categories.GetCategories;

public record SubCategoryDto(int IdSubCategory, string Name, bool IsDefault);
public record CategoryDto(int IdMainCategory, string Name, IReadOnlyList<SubCategoryDto> SubCategories);
