using BigSchool.Application.DTOs.Investments;
using BigSchool.Application.Interfaces;
using BigSchool.Domain.Enums;
using Dapper;
using MediatR;

namespace BigSchool.Application.Queries.Investments.GetCompanyValuations;

public class GetCompanyValuationsQueryHandler : IRequestHandler<GetCompanyValuationsQuery, IReadOnlyList<ValuationListItemDto>>
{
    private readonly IDbConnectionFactory _dbFactory;

    public GetCompanyValuationsQueryHandler(IDbConnectionFactory dbFactory) => _dbFactory = dbFactory;

    private const string GETCOMPANYVALUATIONS_QUERY = @"SELECT IdValuation, IdCompany, Price, PriceCurrency, Date, Source
                                                        FROM Valuations
                                                        WHERE IdCompany = @IdCompany AND IdStatus <> @StatusDeleted
                                                        ORDER BY Date DESC, IdValuation DESC;";

    public async Task<IReadOnlyList<ValuationListItemDto>> Handle(GetCompanyValuationsQuery request, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@IdCompany", request.IdCompany);
        parameters.Add("@StatusDeleted", EntityStatus.Deleted);

        using var conn = _dbFactory.CreateConnection();
        var rows = await conn.QueryAsync<ValuationListItemDto>(GETCOMPANYVALUATIONS_QUERY, parameters);
        return rows.ToList();
    }
}
