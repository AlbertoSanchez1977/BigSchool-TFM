using BigSchool.Application.Investments.DTOs;
using BigSchool.Application.SharedKernel.Interfaces.Services;
using BigSchool.Domain.Investments.Entities;
using BigSchool.Domain.SharedKernel.Exceptions;
using MediatR;
using BigSchool.Application.Investments.Interfaces.Repositories;

namespace BigSchool.Application.Investments.Commands.CreatePortfolio;

public class CreatePortfolioCommandHandler : IRequestHandler<CreatePortfolioCommand, PortfolioDto>
{
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly IUserBaseCurrencyProvider _userBaseCurrencyProvider;

    public CreatePortfolioCommandHandler(IPortfolioRepository portfolioRepository, IUserBaseCurrencyProvider userBaseCurrencyProvider)
    {
        _portfolioRepository = portfolioRepository;
        _userBaseCurrencyProvider = userBaseCurrencyProvider;
    }

    public async Task<PortfolioDto> Handle(CreatePortfolioCommand request, CancellationToken cancellationToken)
    {
        var userBaseCurrency = await _userBaseCurrencyProvider.GetBaseCurrencyAsync(request.IdUser, cancellationToken)
            ?? throw new NotFoundException("User", request.IdUser);

        var portfolio = Portfolio.Create(request.IdUser, request.Name, userBaseCurrency);
        await _portfolioRepository.AddAsync(portfolio, cancellationToken);
        await _portfolioRepository.UnitOfWork.SaveChangesAsync();

        return new PortfolioDto(portfolio.IdPortfolio, portfolio.Name,
            portfolio.RealizedPnL.Amount, portfolio.RealizedPnL.Currency);
    }
}
