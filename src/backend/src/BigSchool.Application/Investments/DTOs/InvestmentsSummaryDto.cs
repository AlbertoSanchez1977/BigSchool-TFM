namespace BigSchool.Application.Investments.DTOs;

public record InvestmentsSummaryDto(
    string BaseCurrency, decimal MarketValue, decimal CostBasis, decimal UnrealizedPnL,
    decimal RealizedPnL, decimal TotalPnL, decimal ReturnPct, int PortfolioCount);
