using BigSchool.Application.DTOs.Investments;
using BigSchool.Application.Interfaces;
using BigSchool.Domain.Enums;
using Dapper;
using MediatR;

namespace BigSchool.Application.Queries.Investments.GetCompanies;

public class GetCompaniesQueryHandler : IRequestHandler<GetCompaniesQuery, IReadOnlyList<CompanyListItemDto>>
{
    private readonly IDbConnectionFactory _dbFactory;

    public GetCompaniesQueryHandler(IDbConnectionFactory dbFactory) => _dbFactory = dbFactory;

    private const string GETCOMPANIES_QUERY = @"SELECT c.IdCompany, c.Name, c.Ticker, c.Sector, c.Market, c.Currency,
                                                       v.Price AS LastPrice, v.Date AS LastValuationDate
                                                FROM Companies c
                                                LEFT JOIN Valuations v ON v.IdValuation = (
                                                    SELECT v2.IdValuation FROM Valuations v2
                                                    WHERE v2.IdCompany = c.IdCompany AND v2.IdStatus <> @StatusDeleted
                                                    ORDER BY v2.Date DESC, v2.IdValuation DESC LIMIT 1)
                                                WHERE c.IdStatus <> @StatusDeleted
                                                  AND (@Sector IS NULL OR c.Sector = @Sector)
                                                  AND (@Market IS NULL OR c.Market = @Market)
                                                ORDER BY c.Name;";

    public async Task<IReadOnlyList<CompanyListItemDto>> Handle(GetCompaniesQuery request, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@StatusDeleted", EntityStatus.Deleted);
        parameters.Add("@Sector", request.Sector);
        parameters.Add("@Market", request.Market);

        using var conn = _dbFactory.CreateConnection();
        var rows = await conn.QueryAsync<CompanyListItemDto>(GETCOMPANIES_QUERY, parameters);
        return rows.ToList();
    }
}
