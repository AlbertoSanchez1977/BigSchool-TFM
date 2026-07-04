using BigSchool.Application.Investments.DTOs;
using BigSchool.Application.SharedKernel.Common;
using BigSchool.Application.SharedKernel.Interfaces;
using BigSchool.Domain.SharedKernel.Enums;
using Dapper;
using MediatR;

namespace BigSchool.Application.Investments.Queries.GetCompanyValuations;

public class GetCompanyValuationsQueryHandler : IRequestHandler<GetCompanyValuationsQuery, PagedResult<ValuationListItemDto>>
{
    private readonly IDbConnectionFactory _dbFactory;

    public GetCompanyValuationsQueryHandler(IDbConnectionFactory dbFactory) => _dbFactory = dbFactory;

    private const string VALUATIONS_WHERE = @"WHERE IdCompany = @IdCompany AND IdStatus <> @StatusDeleted";

    private const string GETCOMPANYVALUATIONS_QUERY = @"SELECT COUNT(*) FROM Valuations " + VALUATIONS_WHERE + @";
            SELECT IdValuation, IdCompany, Price, PriceCurrency, Date, Source
            FROM Valuations " + VALUATIONS_WHERE + @"
            ORDER BY Date DESC, IdValuation DESC
            LIMIT @PageSize OFFSET @Offset;";

    public async Task<PagedResult<ValuationListItemDto>> Handle(GetCompanyValuationsQuery request, CancellationToken cancellationToken)
    {
        var page = Pagination.NormalizePage(request.Page);
        var pageSize = Pagination.NormalizePageSize(request.PageSize);

        var parameters = new DynamicParameters();
        parameters.Add("@IdCompany", request.IdCompany);
        parameters.Add("@StatusDeleted", EntityStatus.Deleted);
        parameters.Add("@PageSize", pageSize);
        parameters.Add("@Offset", (page - 1) * pageSize);

        using var conn = _dbFactory.CreateConnection();
        using var multi = await conn.QueryMultipleAsync(GETCOMPANYVALUATIONS_QUERY, parameters);
        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<ValuationListItemDto>()).ToList();

        return new PagedResult<ValuationListItemDto>(items, page, pageSize, total);
    }
}
