namespace BigSchool.Application.Investments.DTOs;

public record PortfolioListItemDto(
    int IdPortfolio, string Name,
    decimal RealizedPnL, string RealizedPnLCurrency,
    decimal MarketValue, decimal CostBasis, decimal UnrealizedPnL, decimal TotalPnL);
