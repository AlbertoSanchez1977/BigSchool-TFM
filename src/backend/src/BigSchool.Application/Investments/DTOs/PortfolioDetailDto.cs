namespace BigSchool.Application.Investments.DTOs;

public record PortfolioDetailDto(
    int IdPortfolio, string Name,
    decimal RealizedPnL, string RealizedPnLCurrency,
    IReadOnlyList<HoldingListItemDto> Holdings);
