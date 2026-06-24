using BigSchool.Application.DTOs.Investments;
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Domain.Entities;
using BigSchool.Domain.Exceptions;
using MediatR;

namespace BigSchool.Application.Commands.Investments.UpdateHolding;

public class UpdateHoldingCommandHandler : IRequestHandler<UpdateHoldingCommand, HoldingDto>
{
    private readonly IPortfolioRepository _portfolioRepository;

    public UpdateHoldingCommandHandler(IPortfolioRepository portfolioRepository)
        => _portfolioRepository = portfolioRepository;

    public async Task<HoldingDto> Handle(UpdateHoldingCommand request, CancellationToken cancellationToken)
    {
        var portfolio = await _portfolioRepository.GetByIdWithHoldingsAsync(request.PortfolioId, cancellationToken);
        if (portfolio is null || portfolio.IdUser != request.IdUser)
            throw new NotFoundException(nameof(Portfolio), request.PortfolioId);

        portfolio.UpdateHolding(request.HoldingId, request.Notes);
        await _portfolioRepository.UnitOfWork.SaveChangesAsync();

        var holding = portfolio.Holdings.First(h => h.IdHolding == request.HoldingId);
        var c = holding.AvgBuyPrice;
        return new HoldingDto(holding.IdHolding, holding.IdCompany, holding.Shares,
            c.Original.Amount, c.Original.Currency, c.Rate, c.Base.Amount, c.Base.Currency, c.RateDate,
            holding.BuyDate, holding.Notes);
    }
}
