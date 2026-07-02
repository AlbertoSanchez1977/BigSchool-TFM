using BigSchool.Application.Investments.DTOs;
using BigSchool.Application.Investments.Queries;
using BigSchool.Application.SharedKernel.Interfaces.Services;
using BigSchool.Domain.SharedKernel.Enums;
using Dapper;
using MediatR;
using BigSchool.Application.SharedKernel.Interfaces;

namespace BigSchool.Application.Investments.Queries.GetPortfolioById;

public class GetPortfolioByIdQueryHandler : IRequestHandler<GetPortfolioByIdQuery, PortfolioDetailDto?>
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly IUserBaseCurrencyProvider _userBaseCurrencyProvider;

    public GetPortfolioByIdQueryHandler(IDbConnectionFactory dbFactory, IUserBaseCurrencyProvider userBaseCurrencyProvider)
    {
        _dbFactory = dbFactory;
        _userBaseCurrencyProvider = userBaseCurrencyProvider;
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
        var userBaseCurrency = await _userBaseCurrencyProvider.GetBaseCurrencyAsync(request.IdUser, cancellationToken);
        var baseCurrency = (userBaseCurrency ?? Currency.EUR).ToString();

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
