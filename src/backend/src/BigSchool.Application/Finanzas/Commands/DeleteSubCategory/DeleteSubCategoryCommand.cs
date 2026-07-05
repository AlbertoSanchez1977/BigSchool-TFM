using MediatR;

namespace BigSchool.Application.Finanzas.Commands.DeleteSubCategory;

public record DeleteSubCategoryCommand(int IdUser, int IdSubCategory) : IRequest;
