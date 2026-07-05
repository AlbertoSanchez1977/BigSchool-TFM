using BigSchool.Application.Finance.DTOs;
using BigSchool.Application.Finance.Interfaces.Repositories;
using BigSchool.Domain.Finance.Entities;
using BigSchool.Domain.Finance.Exceptions;
using MediatR;

namespace BigSchool.Application.Finance.Commands.CreateSubCategory;

public class CreateSubCategoryCommandHandler : IRequestHandler<CreateSubCategoryCommand, SubCategoryDto>
{
    private readonly ISubCategoryRepository _subCategories;

    public CreateSubCategoryCommandHandler(ISubCategoryRepository subCategories) => _subCategories = subCategories;

    public async Task<SubCategoryDto> Handle(CreateSubCategoryCommand request, CancellationToken cancellationToken)
    {
        if (await _subCategories.ExistsActiveAsync(request.IdUser, request.MainCategory, request.Name, cancellationToken))
            throw new DuplicateSubCategoryDomainException(request.Name.Trim(), request.MainCategory);

        var sub = SubCategory.Create(request.MainCategory, request.Name, request.IdUser);
        await _subCategories.AddAsync(sub, cancellationToken);
        await _subCategories.UnitOfWork.SaveChangesAsync();

        return new SubCategoryDto(sub.IdSubCategory, (int)sub.IdMainCategory, sub.IdMainCategory.ToString(), sub.Name);
    }
}
