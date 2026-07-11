using BigSchool.Application.Investments.Interfaces.Repositories;
using BigSchool.Domain.Investments.Entities;
using BigSchool.Domain.SharedKernel.Exceptions;
using MediatR;

namespace BigSchool.Application.Investments.Commands.RenamePortfolio;

public class RenamePortfolioCommandHandler : IRequestHandler<RenamePortfolioCommand>
{
    private readonly IPortfolioRepository _portfolios;
    public RenamePortfolioCommandHandler(IPortfolioRepository portfolios) => _portfolios = portfolios;

    public async Task Handle(RenamePortfolioCommand request, CancellationToken cancellationToken)
    {
        var p = await _portfolios.GetByIdWithHoldingsAsync(request.IdPortfolio, cancellationToken);
        if (p is null || p.IdUser != request.IdUser)
            throw new NotFoundException(nameof(Portfolio), request.IdPortfolio);
        p.Rename(request.Name);
        await _portfolios.UnitOfWork.SaveChangesAsync();
    }
}
