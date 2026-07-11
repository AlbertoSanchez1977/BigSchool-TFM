using BigSchool.Application.Finance.DTOs;
using BigSchool.Domain.Finance.Enums;
using MediatR;

namespace BigSchool.Application.Finance.Commands.CreateSubCategory;

public record CreateSubCategoryCommand(int IdUser, MainCategory MainCategory, string Name) : IRequest<SubCategoryFromCategoryDto>;
