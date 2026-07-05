using BigSchool.Application.Finanzas.Interfaces.Repositories;
using BigSchool.Domain.SharedKernel.Exceptions;
using MediatR;

namespace BigSchool.Application.Finanzas.Commands.DeleteSubCategory;

public class DeleteSubCategoryCommandHandler : IRequestHandler<DeleteSubCategoryCommand>
{
    private readonly ISubCategoryRepository _subCategories;

    public DeleteSubCategoryCommandHandler(ISubCategoryRepository subCategories) => _subCategories = subCategories;

    public async Task Handle(DeleteSubCategoryCommand request, CancellationToken cancellationToken)
    {
        var sub = await _subCategories.GetByIdAsync(request.IdSubCategory, cancellationToken);
        // Decisión HTTP: 404 no-leak si no existe, es predefinida, global o de otro usuario.
        if (sub is null || sub.IsDefault || sub.IdUser != request.IdUser)
            throw new NotFoundException("SubCategory", request.IdSubCategory);

        sub.Delete(request.IdUser); // el dominio reafirma la invariante (defensa en profundidad)
        await _subCategories.UnitOfWork.SaveChangesAsync();
    }
}
