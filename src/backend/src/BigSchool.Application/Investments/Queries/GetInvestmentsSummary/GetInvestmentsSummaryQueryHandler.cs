using BigSchool.Application.Investments.DTOs;
using BigSchool.Application.Investments.Queries;
using BigSchool.Application.SharedKernel.Interfaces;
using BigSchool.Application.SharedKernel.Interfaces.Services;
using BigSchool.Domain.SharedKernel.Enums;
using Dapper;
using MediatR;

namespace BigSchool.Application.Investments.Queries.GetInvestmentsSummary;

public class GetInvestmentsSummaryQueryHandler : IRequestHandler<GetInvestmentsSummaryQuery, InvestmentsSummaryDto>
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly IUserBaseCurrencyProvider _userBaseCurrencyProvider;

    public GetInvestmentsSummaryQueryHandler(IDbConnectionFactory dbFactory, IUserBaseCurrencyProvider userBaseCurrencyProvider)
    {
        _dbFactory = dbFactory;
        _userBaseCurrencyProvider = userBaseCurrencyProvider;
    }

    private const string SUMMARY_QUERY = @"
SELECT COALESCE(SUM(hv.MarketValue),0)   AS MarketValue,
       COALESCE(SUM(hv.CostBasis),0)     AS CostBasis,
       COALESCE(SUM(hv.UnrealizedPnL),0) AS UnrealizedPnL
FROM (" + PortfolioSqlFragments.HOLDING_VALUATION + @") hv
JOIN Portfolios p ON p.IdPortfolio = hv.IdPortfolio
WHERE p.IdUser = @IdUser AND p.IdStatus <> @StatusDeleted AND hv.OpenShares > 0;
SELECT COALESCE(SUM(RealizedPnL),0) AS RealizedPnL, COUNT(*) AS PortfolioCount
FROM Portfolios WHERE IdUser = @IdUser AND IdStatus <> @StatusDeleted;";

    private sealed record AggRow(decimal MarketValue, decimal CostBasis, decimal UnrealizedPnL);

    // COUNT(*) en MySQL/MySqlConnector se materializa como long: un ctor con `int PortfolioCount`
    // no matchea y Dapper lanza InvalidOperationException al construir el record.
    private sealed record RealizedRow(decimal RealizedPnL, long PortfolioCount);

    public async Task<InvestmentsSummaryDto> Handle(GetInvestmentsSummaryQuery request, CancellationToken cancellationToken)
    {
        var userBaseCurrency = await _userBaseCurrencyProvider.GetBaseCurrencyAsync(request.IdUser, cancellationToken);
        var baseCurrency = (userBaseCurrency ?? Currency.EUR).ToString();

        var p = new DynamicParameters();
        p.Add("@IdUser", request.IdUser);
        p.Add("@StatusDeleted", EntityStatus.Deleted);
        p.Add("@BaseCurrency", baseCurrency);

        using var conn = _dbFactory.CreateConnection();
        using var multi = await conn.QueryMultipleAsync(SUMMARY_QUERY, p);
        var agg = await multi.ReadSingleAsync<AggRow>();
        var realized = await multi.ReadSingleAsync<RealizedRow>();

        var total = realized.RealizedPnL + agg.UnrealizedPnL;
        var returnPct = agg.CostBasis > 0m ? Math.Round(agg.UnrealizedPnL / agg.CostBasis * 100m, 2) : 0m;

        return new InvestmentsSummaryDto(baseCurrency, agg.MarketValue, agg.CostBasis, agg.UnrealizedPnL,
            realized.RealizedPnL, total, returnPct, (int)realized.PortfolioCount);
    }
}
