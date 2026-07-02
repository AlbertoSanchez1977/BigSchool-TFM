using BigSchool.Domain.Enums;
using BigSchool.Domain.SharedKernel.Exceptions;

namespace BigSchool.Domain.Exceptions;

public class DuplicateSubCategoryDomainException : DomainException
{
    public DuplicateSubCategoryDomainException(string name, MainCategory mainCategory)
        : base("DUPLICATE_SUBCATEGORY",
            $"Ya existe una subcategoría '{name}' en la categoría '{mainCategory}'.") { }
}
