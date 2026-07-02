using BigSchool.Domain.Investments.Entities;
using BigSchool.Domain.SharedKernel.Exceptions;
using MediatR;
using BigSchool.Application.Investments.Interfaces.Repositories;

namespace BigSchool.Application.Investments.Commands.DeleteHolding;

public class DeleteHoldingCommandHandler : IRequestHandler<DeleteHoldingCommand>
{
    private readonly IPortfolioRepository _portfolioRepository;

    public DeleteHoldingCommandHandler(IPortfolioRepository portfolioRepository)
        => _portfolioRepository = portfolioRepository;

    public async Task Handle(DeleteHoldingCommand request, CancellationToken cancellationToken)
    {
        var portfolio = await _portfolioRepository.GetByIdWithHoldingsAsync(request.PortfolioId, cancellationToken);
        if (portfolio is null || portfolio.IdUser != request.IdUser)
            throw new NotFoundException(nameof(Portfolio), request.PortfolioId);

        portfolio.DeleteHolding(request.HoldingId);
        await _portfolioRepository.UnitOfWork.SaveChangesAsync();
    }
}
