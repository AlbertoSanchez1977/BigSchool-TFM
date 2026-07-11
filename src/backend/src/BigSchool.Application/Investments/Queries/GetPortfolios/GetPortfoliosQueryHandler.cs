using BigSchool.Application.Investments.DTOs;
using BigSchool.Application.SharedKernel.Common;
using BigSchool.Application.SharedKernel.Interfaces.Services;
using BigSchool.Domain.SharedKernel.Enums;
using Dapper;
using MediatR;
using BigSchool.Application.SharedKernel.Interfaces;

namespace BigSchool.Application.Investments.Queries.GetPortfolios;

public class GetPortfoliosQueryHandler : IRequestHandler<GetPortfoliosQuery, PagedResult<PortfolioListItemDto>>
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly IUserBaseCurrencyProvider _userBaseCurrencyProvider;

    public GetPortfoliosQueryHandler(IDbConnectionFactory dbFactory, IUserBaseCurrencyProvider userBaseCurrencyProvider)
    {
        _dbFactory = dbFactory;
        _userBaseCurrencyProvider = userBaseCurrencyProvider;
    }

    private const string GETPORTFOLIOS_QUERY = @"
SELECT COUNT(*) FROM Portfolios WHERE IdUser = @IdUser AND IdStatus <> @StatusDeleted;
SELECT p.IdPortfolio, p.Name, p.RealizedPnL, p.RealizedPnLCurrency,
       COALESCE(SUM(hv.MarketValue), 0)   AS MarketValue,
       COALESCE(SUM(hv.CostBasis), 0)     AS CostBasis,
       COALESCE(SUM(hv.UnrealizedPnL), 0) AS UnrealizedPnL,
       (p.RealizedPnL + COALESCE(SUM(hv.UnrealizedPnL), 0)) AS TotalPnL
FROM Portfolios p
LEFT JOIN (" + PortfolioSqlFragments.HOLDING_VALUATION + @") hv
       ON hv.IdPortfolio = p.IdPortfolio AND hv.OpenShares > 0
WHERE p.IdUser = @IdUser AND p.IdStatus <> @StatusDeleted
GROUP BY p.IdPortfolio, p.Name, p.RealizedPnL, p.RealizedPnLCurrency
ORDER BY p.IdPortfolio
LIMIT @PageSize OFFSET @Offset;";

    public async Task<PagedResult<PortfolioListItemDto>> Handle(GetPortfoliosQuery request, CancellationToken cancellationToken)
    {
        var userBaseCurrency = await _userBaseCurrencyProvider.GetBaseCurrencyAsync(request.IdUser, cancellationToken);
        var baseCurrency = (userBaseCurrency ?? Currency.EUR).ToString();
        var page = Pagination.NormalizePage(request.Page);
        var pageSize = Pagination.NormalizePageSize(request.PageSize);

        var parameters = new DynamicParameters();
        parameters.Add("@IdUser", request.IdUser);
        parameters.Add("@StatusDeleted", EntityStatus.Deleted);
        parameters.Add("@BaseCurrency", baseCurrency);
        parameters.Add("@PageSize", pageSize);
        parameters.Add("@Offset", (page - 1) * pageSize);

        using var conn = _dbFactory.CreateConnection();
        using var multi = await conn.QueryMultipleAsync(GETPORTFOLIOS_QUERY, parameters);
        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<PortfolioListItemDto>()).ToList();

        return new PagedResult<PortfolioListItemDto>(items, page, pageSize, total);
    }
}
