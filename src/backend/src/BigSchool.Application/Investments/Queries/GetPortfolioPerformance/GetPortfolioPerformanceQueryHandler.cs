using BigSchool.Application.Investments.DTOs;
using BigSchool.Application.Investments.Queries;
using BigSchool.Domain.SharedKernel.Enums;
using Dapper;
using MediatR;
using BigSchool.Application.SharedKernel.Interfaces;
using BigSchool.Application.Auth.Interfaces.Repositories;

namespace BigSchool.Application.Investments.Queries.GetPortfolioPerformance;

public class GetPortfolioPerformanceQueryHandler
    : IRequestHandler<GetPortfolioPerformanceQuery, PortfolioPerformanceDto?>
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly IUserRepository _userRepository;

    public GetPortfolioPerformanceQueryHandler(IDbConnectionFactory dbFactory, IUserRepository userRepository)
    {
        _dbFactory = dbFactory;
        _userRepository = userRepository;
    }

    private const string HEADER_QUERY = @"SELECT IdPortfolio, Name, RealizedPnL, RealizedPnLCurrency
                                          FROM Portfolios
                                          WHERE IdPortfolio = @IdPortfolio AND IdUser = @IdUser AND IdStatus <> @StatusDeleted
                                          LIMIT 1;";

    // Solo lotes abiertos (OpenShares > 0) para el desglose no realizado.
    private const string OPEN_HOLDINGS_QUERY = @"
SELECT hv.IdHolding, hv.IdCompany, hv.Ticker, hv.OpenShares,
       hv.CostBasis, hv.MarketValue, hv.UnrealizedPnL
FROM (" + PortfolioSqlFragments.HOLDING_VALUATION + @") hv
WHERE hv.IdPortfolio = @IdPortfolio AND hv.OpenShares > 0
ORDER BY hv.BuyDate, hv.IdHolding;";

    private sealed record HoldingRow(int IdHolding, int IdCompany, string Ticker, decimal OpenShares,
        decimal CostBasis, decimal MarketValue, decimal UnrealizedPnL);

    public async Task<PortfolioPerformanceDto?> Handle(GetPortfolioPerformanceQuery request, CancellationToken cancellationToken)
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

        var rows = (await conn.QueryAsync<HoldingRow>(OPEN_HOLDINGS_QUERY, parameters)).ToList();

        var holdings = rows.Select(r => new HoldingPerformanceDto(
            r.IdHolding, r.IdCompany, r.Ticker, r.OpenShares, r.CostBasis, r.MarketValue, r.UnrealizedPnL,
            r.CostBasis > 0m ? Math.Round(r.UnrealizedPnL / r.CostBasis * 100m, 2) : 0m)).ToList();

        var marketValue = holdings.Sum(h => h.MarketValue);
        var costBasis = holdings.Sum(h => h.CostBasis);
        var unrealized = holdings.Sum(h => h.UnrealizedPnL);
        var realized = (decimal)header.RealizedPnL;
        var total = realized + unrealized;
        var returnPct = costBasis > 0m ? Math.Round(unrealized / costBasis * 100m, 2) : 0m;

        return new PortfolioPerformanceDto(
            (int)header.IdPortfolio, (string)header.Name, (string)header.RealizedPnLCurrency,
            marketValue, costBasis, unrealized, realized, total, returnPct, holdings);
    }
}
