using BigSchool.Application.Investments.Interfaces.Repositories;
using BigSchool.Domain.Investments.Entities;
using BigSchool.Domain.SharedKernel.Exceptions;
using MediatR;

namespace BigSchool.Application.Investments.Commands.DeletePortfolio;

public class DeletePortfolioCommandHandler : IRequestHandler<DeletePortfolioCommand>
{
    private readonly IPortfolioRepository _portfolios;
    public DeletePortfolioCommandHandler(IPortfolioRepository portfolios) => _portfolios = portfolios;

    public async Task Handle(DeletePortfolioCommand request, CancellationToken cancellationToken)
    {
        var p = await _portfolios.GetByIdWithHoldingsAsync(request.IdPortfolio, cancellationToken);
        if (p is null || p.IdUser != request.IdUser)
            throw new NotFoundException(nameof(Portfolio), request.IdPortfolio);
        p.Delete(DateOnly.FromDateTime(DateTime.UtcNow)); // guards → 409 si aplica
        await _portfolios.UnitOfWork.SaveChangesAsync();
    }
}
