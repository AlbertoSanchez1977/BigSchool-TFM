using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Domain.Entities;
using BigSchool.Domain.SharedKernel.Exceptions;
using MediatR;

namespace BigSchool.Application.Commands.Investments.DeleteHolding;

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
