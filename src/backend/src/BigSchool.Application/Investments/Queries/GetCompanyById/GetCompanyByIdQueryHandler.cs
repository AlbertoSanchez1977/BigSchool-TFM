using BigSchool.Application.Investments.DTOs;
using BigSchool.Domain.Investments.Enums;
using BigSchool.Domain.SharedKernel.Enums;
using Dapper;
using MediatR;
using BigSchool.Application.SharedKernel.Interfaces;

namespace BigSchool.Application.Investments.Queries.GetCompanyById;

public class GetCompanyByIdQueryHandler : IRequestHandler<GetCompanyByIdQuery, CompanyListItemDto?>
{
    private readonly IDbConnectionFactory _dbFactory;

    public GetCompanyByIdQueryHandler(IDbConnectionFactory dbFactory) => _dbFactory = dbFactory;

    private const string GETCOMPANYBYID_QUERY = @"SELECT c.IdCompany, c.Name, c.Ticker, c.Sector, c.Market, c.Currency,
                                                         v.Price AS LastPrice, v.Date AS LastValuationDate
                                                  FROM Companies c
                                                  LEFT JOIN Valuations v ON v.IdValuation = (
                                                      SELECT v2.IdValuation FROM Valuations v2
                                                      WHERE v2.IdCompany = c.IdCompany AND v2.IdStatus <> @StatusDeleted
                                                      ORDER BY v2.Date DESC, v2.IdValuation DESC LIMIT 1)
                                                  WHERE c.IdCompany = @IdCompany AND c.IdStatus <> @StatusDeleted
                                                  LIMIT 1;";

    public async Task<CompanyListItemDto?> Handle(GetCompanyByIdQuery request, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@IdCompany", request.IdCompany);
        parameters.Add("@StatusDeleted", EntityStatus.Deleted);

        using var conn = _dbFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<CompanyListItemDto>(GETCOMPANYBYID_QUERY, parameters);
    }
}
