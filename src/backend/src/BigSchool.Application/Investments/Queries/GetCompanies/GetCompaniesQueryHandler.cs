using BigSchool.Application.Investments.DTOs;
using BigSchool.Application.SharedKernel.Common;
using BigSchool.Domain.Investments.Enums;
using BigSchool.Domain.SharedKernel.Enums;
using Dapper;
using MediatR;
using BigSchool.Application.SharedKernel.Interfaces;

namespace BigSchool.Application.Investments.Queries.GetCompanies;

public class GetCompaniesQueryHandler : IRequestHandler<GetCompaniesQuery, PagedResult<CompanyListItemDto>>
{
    private readonly IDbConnectionFactory _dbFactory;

    public GetCompaniesQueryHandler(IDbConnectionFactory dbFactory) => _dbFactory = dbFactory;

    private const string COMPANIES_WHERE = @"WHERE c.IdStatus <> @StatusDeleted
                                              AND (@Sector IS NULL OR c.Sector = @Sector)
                                              AND (@Market IS NULL OR c.Market = @Market)";

    private const string GETCOMPANIES_QUERY = @"SELECT COUNT(*) FROM Companies c " + COMPANIES_WHERE + @";
            SELECT c.IdCompany, c.Name, c.Ticker, c.Sector, c.Market, c.Currency,
                   v.Price AS LastPrice, v.Date AS LastValuationDate
            FROM Companies c
            LEFT JOIN Valuations v ON v.IdValuation = (
                SELECT v2.IdValuation FROM Valuations v2
                WHERE v2.IdCompany = c.IdCompany AND v2.IdStatus <> @StatusDeleted
                ORDER BY v2.Date DESC, v2.IdValuation DESC LIMIT 1)
            " + COMPANIES_WHERE + @"
            ORDER BY c.Name, c.IdCompany
            LIMIT @PageSize OFFSET @Offset;";

    public async Task<PagedResult<CompanyListItemDto>> Handle(GetCompaniesQuery request, CancellationToken cancellationToken)
    {
        var page = Pagination.NormalizePage(request.Page);
        var pageSize = Pagination.NormalizePageSize(request.PageSize);

        var parameters = new DynamicParameters();
        parameters.Add("@StatusDeleted", EntityStatus.Deleted);
        parameters.Add("@Sector", request.Sector?.ToString());
        parameters.Add("@Market", request.Market?.ToString());
        parameters.Add("@PageSize", pageSize);
        parameters.Add("@Offset", (page - 1) * pageSize);

        using var conn = _dbFactory.CreateConnection();
        using var multi = await conn.QueryMultipleAsync(GETCOMPANIES_QUERY, parameters);
        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<CompanyListItemDto>()).ToList();

        return new PagedResult<CompanyListItemDto>(items, page, pageSize, total);
    }
}
