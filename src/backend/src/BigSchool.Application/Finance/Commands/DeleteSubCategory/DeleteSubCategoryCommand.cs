using MediatR;

namespace BigSchool.Application.Finance.Commands.DeleteSubCategory;

public record DeleteSubCategoryCommand(int IdUser, int IdSubCategory) : IRequest;
