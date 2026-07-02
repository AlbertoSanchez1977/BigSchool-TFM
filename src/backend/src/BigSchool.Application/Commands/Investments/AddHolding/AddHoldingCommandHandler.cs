using BigSchool.Application.DTOs.Investments;
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Application.Interfaces.Services;
using BigSchool.Domain.Entities;
using BigSchool.Domain.SharedKernel.Exceptions;
using BigSchool.Domain.SharedKernel.ValueObjects;
using MediatR;

namespace BigSchool.Application.Commands.Investments.AddHolding;

public class AddHoldingCommandHandler : IRequestHandler<AddHoldingCommand, HoldingDto>
{
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IExchangeRateProvider _exchangeRateProvider;

    public AddHoldingCommandHandler(
        IPortfolioRepository portfolioRepository,
        ICompanyRepository companyRepository,
        IExchangeRateProvider exchangeRateProvider)
    {
        _portfolioRepository = portfolioRepository;
        _companyRepository = companyRepository;
        _exchangeRateProvider = exchangeRateProvider;
    }

    public async Task<HoldingDto> Handle(AddHoldingCommand request, CancellationToken cancellationToken)
    {
        var portfolio = await _portfolioRepository.GetByIdWithHoldingsAsync(request.PortfolioId, cancellationToken);
        if (portfolio is null || portfolio.IdUser != request.IdUser)
            throw new NotFoundException(nameof(Portfolio), request.PortfolioId);

        var company = await _companyRepository.GetByIdAsync(request.IdCompany, cancellationToken)
            ?? throw new NotFoundException(nameof(Company), request.IdCompany);

        var baseCurrency = portfolio.RealizedPnL.Currency;
        var rate = company.Currency == baseCurrency
            ? 1m
            : await _exchangeRateProvider.GetRateAsync(company.Currency, baseCurrency, request.BuyDate, cancellationToken);

        var holding = portfolio.AddHolding(
            request.IdCompany,
            request.Shares,
            Money.Create(request.BuyPrice, company.Currency),
            baseCurrency,
            rate,
            request.BuyDate,
            request.BuyDate,
            request.Notes);

        await _portfolioRepository.UnitOfWork.SaveChangesAsync();

        var c = holding.AvgBuyPrice;
        return new HoldingDto(holding.IdHolding, holding.IdCompany, holding.Shares,
            c.Original.Amount, c.Original.Currency, c.Rate, c.Base.Amount, c.Base.Currency, c.RateDate,
            holding.BuyDate, holding.Notes);
    }
}
