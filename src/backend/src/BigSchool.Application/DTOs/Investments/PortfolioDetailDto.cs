namespace BigSchool.Application.DTOs.Investments;

public record PortfolioDetailDto(
    int IdPortfolio, string Name,
    decimal RealizedPnL, string RealizedPnLCurrency,
    IReadOnlyList<HoldingListItemDto> Holdings);
