using BigSchool.Domain.Finance.Enums;
using BigSchool.Domain.SharedKernel.Exceptions;

namespace BigSchool.Domain.Finance.Exceptions;

public class DuplicateSubCategoryDomainException : ConflictException
{
    public DuplicateSubCategoryDomainException(string name, MainCategory mainCategory)
        : base("DUPLICATE_SUBCATEGORY",
            $"Ya existe una subcategoría '{name}' en la categoría '{mainCategory}'.") { }
}
