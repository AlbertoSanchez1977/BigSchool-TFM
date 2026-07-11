using BigSchool.Application.Investments.DTOs;
using BigSchool.Application.SharedKernel.Interfaces;
using BigSchool.Domain.SharedKernel.Enums;
using Dapper;
using MediatR;

namespace BigSchool.Application.Investments.Queries.GetCompanyValuationSeries;

public class GetCompanyValuationSeriesQueryHandler : IRequestHandler<GetCompanyValuationSeriesQuery, ValuationSeriesDto>
{
    private readonly IDbConnectionFactory _dbFactory;

    public GetCompanyValuationSeriesQueryHandler(IDbConnectionFactory dbFactory) => _dbFactory = dbFactory;

    private const string LASTDATE_QUERY = @"SELECT Date FROM Valuations
        WHERE IdCompany = @IdCompany AND IdStatus <> @StatusDeleted
        ORDER BY Date DESC, IdValuation DESC LIMIT 1;";

    private const string SERIES_QUERY = @"SELECT Date, Price, PriceCurrency
        FROM Valuations
        WHERE IdCompany = @IdCompany AND IdStatus <> @StatusDeleted
          AND Date >= @From AND Date <= @Last
        ORDER BY Date ASC, IdValuation ASC;";

    private sealed record Row(DateOnly Date, decimal Price, string PriceCurrency);

    public async Task<ValuationSeriesDto> Handle(GetCompanyValuationSeriesQuery request, CancellationToken cancellationToken)
    {
        var empty = new ValuationSeriesDto("", new List<ValuationPointDto>(), new ValuationSeriesSummaryDto(0, 0, 0, 0, 0));

        var p = new DynamicParameters();
        p.Add("@IdCompany", request.IdCompany);
        p.Add("@StatusDeleted", EntityStatus.Deleted);

        using var conn = _dbFactory.CreateConnection();
        var lastDate = await conn.QuerySingleOrDefaultAsync<DateOnly?>(LASTDATE_QUERY, p);
        if (lastDate is null) return empty; // empresa sin valoraciones → serie vacía

        var months = (int)request.Period; // el valor del enum ES el nº de meses
        p.Add("@Last", lastDate.Value);
        p.Add("@From", lastDate.Value.AddMonths(-months));

        var rows = (await conn.QueryAsync<Row>(SERIES_QUERY, p)).ToList();
        if (rows.Count == 0) return empty;

        var prices = rows.Select(r => r.Price).ToList();
        var first = prices.First();
        var last = prices.Last();
        var changePct = first == 0m ? 0m : Math.Round((last - first) / first * 100m, 2);

        var points = rows.Select(r => new ValuationPointDto(r.Date, r.Price)).ToList();
        var summary = new ValuationSeriesSummaryDto(first, last, prices.Min(), prices.Max(), changePct);
        return new ValuationSeriesDto(rows.First().PriceCurrency, points, summary);
    }
}
