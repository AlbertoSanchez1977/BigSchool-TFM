namespace BigSchool.Application.Finance.Queries.Categories.GetCategories;

public record SubCategoryResponseDto(int IdSubCategory, string Name, bool IsDefault);
public record CategoryDto(int IdMainCategory, string Name, IReadOnlyList<SubCategoryResponseDto> SubCategories);
