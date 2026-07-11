using BigSchool.Application.Investments.DTOs;
using BigSchool.Domain.Investments.Entities;
using BigSchool.Domain.SharedKernel.Exceptions;
using MediatR;
using BigSchool.Application.Investments.Interfaces.Repositories;

namespace BigSchool.Application.Investments.Commands.UpdateHolding;

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
