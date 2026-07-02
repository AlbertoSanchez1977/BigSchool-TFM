using BigSchool.Application.Investments.DTOs;
using BigSchool.Domain.SharedKernel.Enums;
using Dapper;
using MediatR;
using BigSchool.Application.SharedKernel.Interfaces;
using BigSchool.Application.Auth.Interfaces.Repositories;

namespace BigSchool.Application.Investments.Queries.GetPortfolios;

public class GetPortfoliosQueryHandler : IRequestHandler<GetPortfoliosQuery, IReadOnlyList<PortfolioListItemDto>>
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly IUserRepository _userRepository;

    public GetPortfoliosQueryHandler(IDbConnectionFactory dbFactory, IUserRepository userRepository)
    {
        _dbFactory = dbFactory;
        _userRepository = userRepository;
    }

    private const string GETPORTFOLIOS_QUERY = @"
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
ORDER BY p.IdPortfolio;";

    public async Task<IReadOnlyList<PortfolioListItemDto>> Handle(GetPortfoliosQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.IdUser, cancellationToken);
        var baseCurrency = (user?.BaseCurrency ?? Currency.EUR).ToString();

        var parameters = new DynamicParameters();
        parameters.Add("@IdUser", request.IdUser);
        parameters.Add("@StatusDeleted", EntityStatus.Deleted);
        parameters.Add("@BaseCurrency", baseCurrency);

        using var conn = _dbFactory.CreateConnection();
        var rows = await conn.QueryAsync<PortfolioListItemDto>(GETPORTFOLIOS_QUERY, parameters);
        return rows.ToList();
    }
}
