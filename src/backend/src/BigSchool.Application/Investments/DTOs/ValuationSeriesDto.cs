namespace BigSchool.Application.Investments.DTOs;

public record ValuationPointDto(DateOnly Date, decimal Price);

public record ValuationSeriesSummaryDto(decimal First, decimal Last, decimal Min, decimal Max, decimal ChangePct);

public record ValuationSeriesDto(string Currency, IReadOnlyList<ValuationPointDto> Points, ValuationSeriesSummaryDto Summary);
