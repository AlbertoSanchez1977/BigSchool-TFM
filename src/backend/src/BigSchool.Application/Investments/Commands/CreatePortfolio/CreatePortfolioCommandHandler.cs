using BigSchool.Application.Investments.DTOs;
using BigSchool.Domain.Auth.Entities;
using BigSchool.Domain.Investments.Entities;
using BigSchool.Domain.SharedKernel.Exceptions;
using MediatR;
using BigSchool.Application.Auth.Interfaces.Repositories;
using BigSchool.Application.Investments.Interfaces.Repositories;

namespace BigSchool.Application.Investments.Commands.CreatePortfolio;

public class CreatePortfolioCommandHandler : IRequestHandler<CreatePortfolioCommand, PortfolioDto>
{
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly IUserRepository _userRepository;

    public CreatePortfolioCommandHandler(IPortfolioRepository portfolioRepository, IUserRepository userRepository)
    {
        _portfolioRepository = portfolioRepository;
        _userRepository = userRepository;
    }

    public async Task<PortfolioDto> Handle(CreatePortfolioCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.IdUser, cancellationToken)
            ?? throw new NotFoundException(nameof(User), request.IdUser);

        var portfolio = Portfolio.Create(request.IdUser, request.Name, user.BaseCurrency);
        await _portfolioRepository.AddAsync(portfolio, cancellationToken);
        await _portfolioRepository.UnitOfWork.SaveChangesAsync();

        return new PortfolioDto(portfolio.IdPortfolio, portfolio.Name,
            portfolio.RealizedPnL.Amount, portfolio.RealizedPnL.Currency);
    }
}
