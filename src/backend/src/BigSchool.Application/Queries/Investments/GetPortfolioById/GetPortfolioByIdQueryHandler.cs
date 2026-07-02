using BigSchool.Application.DTOs.Investments;
using BigSchool.Application.Interfaces;
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Application.Queries.Investments;
using BigSchool.Domain.SharedKernel.Enums;
using Dapper;
using MediatR;

namespace BigSchool.Application.Queries.Investments.GetPortfolioById;

public class GetPortfolioByIdQueryHandler : IRequestHandler<GetPortfolioByIdQuery, PortfolioDetailDto?>
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly IUserRepository _userRepository;

    public GetPortfolioByIdQueryHandler(IDbConnectionFactory dbFactory, IUserRepository userRepository)
    {
        _dbFactory = dbFactory;
        _userRepository = userRepository;
    }

    private const string HEADER_QUERY = @"SELECT IdPortfolio, Name, RealizedPnL, RealizedPnLCurrency
                                          FROM Portfolios
                                          WHERE IdPortfolio = @IdPortfolio AND IdUser = @IdUser AND IdStatus <> @StatusDeleted
                                          LIMIT 1;";

    private const string HOLDINGS_QUERY = @"
SELECT hv.IdHolding, hv.IdCompany, hv.Ticker, hv.CompanyCurrency,
       hv.Shares, hv.OpenShares,
       hv.BuyOriginalAmount, hv.BuyOriginalCurrency, hv.BuyExchangeRate,
       hv.BuyBaseAmount, hv.BuyBaseCurrency, hv.BuyRateDate, hv.BuyDate, hv.Notes,
       hv.MarketValue, hv.CostBasis, hv.UnrealizedPnL
FROM (" + PortfolioSqlFragments.HOLDING_VALUATION + @") hv
WHERE hv.IdPortfolio = @IdPortfolio
ORDER BY hv.BuyDate, hv.IdHolding;";

    public async Task<PortfolioDetailDto?> Handle(GetPortfolioByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.IdUser, cancellationToken);
        var baseCurrency = (user?.BaseCurrency ?? Currency.EUR).ToString();

        var parameters = new DynamicParameters();
        parameters.Add("@IdPortfolio", request.IdPortfolio);
        parameters.Add("@IdUser", request.IdUser);
        parameters.Add("@StatusDeleted", EntityStatus.Deleted);
        parameters.Add("@BaseCurrency", baseCurrency);

        using var conn = _dbFactory.CreateConnection();

        var header = await conn.QuerySingleOrDefaultAsync(HEADER_QUERY, parameters);
        if (header is null) return null;

        var holdings = (await conn.QueryAsync<HoldingListItemDto>(HOLDINGS_QUERY, parameters)).ToList();

        return new PortfolioDetailDto(
            (int)header.IdPortfolio, (string)header.Name,
            (decimal)header.RealizedPnL, (string)header.RealizedPnLCurrency,
            holdings);
    }
}
