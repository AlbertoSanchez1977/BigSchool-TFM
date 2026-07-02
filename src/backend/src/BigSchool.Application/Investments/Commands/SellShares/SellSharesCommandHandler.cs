using BigSchool.Application.Investments.DTOs;
using BigSchool.Domain.Investments.Entities;
using BigSchool.Domain.SharedKernel.Exceptions;
using BigSchool.Domain.SharedKernel.ValueObjects;
using MediatR;
using BigSchool.Application.Investments.Interfaces.Repositories;
using BigSchool.Application.SharedKernel.Interfaces.Services;

namespace BigSchool.Application.Investments.Commands.SellShares;

public class SellSharesCommandHandler : IRequestHandler<SellSharesCommand, SellSharesResultDto>
{
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IExchangeRateProvider _exchangeRateProvider;

    public SellSharesCommandHandler(
        IPortfolioRepository portfolioRepository,
        ICompanyRepository companyRepository,
        IExchangeRateProvider exchangeRateProvider)
    {
        _portfolioRepository = portfolioRepository;
        _companyRepository = companyRepository;
        _exchangeRateProvider = exchangeRateProvider;
    }

    public async Task<SellSharesResultDto> Handle(SellSharesCommand request, CancellationToken cancellationToken)
    {
        var portfolio = await _portfolioRepository.GetByIdWithHoldingsAsync(request.PortfolioId, cancellationToken);
        if (portfolio is null || portfolio.IdUser != request.IdUser)
            throw new NotFoundException(nameof(Portfolio), request.PortfolioId);

        var company = await _companyRepository.GetByIdAsync(request.IdCompany, cancellationToken)
            ?? throw new NotFoundException(nameof(Company), request.IdCompany);

        var baseCurrency = portfolio.RealizedPnL.Currency;
        var rate = company.Currency == baseCurrency
            ? 1m
            : await _exchangeRateProvider.GetRateAsync(company.Currency, baseCurrency, request.SellDate, cancellationToken);

        var created = portfolio.SellShares(
            request.IdCompany,
            request.Shares,
            Money.Create(request.SellPrice, company.Currency),
            baseCurrency,
            rate,
            request.SellDate,
            request.SellDate);

        await _portfolioRepository.UnitOfWork.SaveChangesAsync();

        var disposalDtos = created
            .Select(d =>
            {
                var holding = portfolio.Holdings.First(h => h.Disposals.Contains(d));
                var s = d.SellPrice;
                return new DisposalDto(
                    d.IdDisposal, holding.IdHolding, d.Shares,
                    s.Original.Amount, s.Original.Currency, s.Rate, s.Base.Amount, s.Base.Currency, s.RateDate,
                    d.SellDate, d.RealizedPnL.Amount, d.RealizedPnL.Currency, d.Notes);
            })
            .ToList();

        return new SellSharesResultDto(disposalDtos, portfolio.RealizedPnL.Amount, portfolio.RealizedPnL.Currency);
    }
}
