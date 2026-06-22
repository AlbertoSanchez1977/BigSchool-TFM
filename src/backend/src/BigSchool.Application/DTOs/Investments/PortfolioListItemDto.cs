namespace BigSchool.Application.DTOs.Investments;

public record PortfolioListItemDto(
    int IdPortfolio, string Name,
    decimal RealizedPnL, string RealizedPnLCurrency,
    decimal MarketValue, decimal CostBasis, decimal UnrealizedPnL, decimal TotalPnL);
