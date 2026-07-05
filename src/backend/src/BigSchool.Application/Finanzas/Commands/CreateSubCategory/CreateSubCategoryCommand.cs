using BigSchool.Application.Finanzas.DTOs;
using BigSchool.Domain.Finanzas.Enums;
using MediatR;

namespace BigSchool.Application.Finanzas.Commands.CreateSubCategory;

public record CreateSubCategoryCommand(int IdUser, MainCategory MainCategory, string Name) : IRequest<SubCategoryDto>;
