namespace BigSchool.Application.Investments.DTOs;

public record ValuationListItemDto(
    int IdValuation, int IdCompany, decimal Price, string PriceCurrency, DateOnly Date, string? Source);
