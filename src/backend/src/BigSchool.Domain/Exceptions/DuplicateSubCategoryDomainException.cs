using BigSchool.Domain.Enums;

namespace BigSchool.Domain.Exceptions;

public class DuplicateSubCategoryDomainException : DomainException
{
    public DuplicateSubCategoryDomainException(string name, MainCategory mainCategory)
        : base("DUPLICATE_SUBCATEGORY",
            $"Ya existe una subcategoría '{name}' en la categoría '{mainCategory}'.") { }
}
