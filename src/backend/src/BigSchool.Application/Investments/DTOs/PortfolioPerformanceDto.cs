namespace BigSchool.Application.Investments.DTOs;

public record PortfolioPerformanceDto(
    int IdPortfolio, string Name, string BaseCurrency,
    decimal MarketValue, decimal CostBasis, decimal UnrealizedPnL,
    decimal RealizedPnL, decimal TotalPnL, decimal ReturnPct,
    IReadOnlyList<HoldingPerformanceDto> Holdings);
