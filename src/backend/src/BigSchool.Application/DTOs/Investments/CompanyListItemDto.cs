namespace BigSchool.Application.DTOs.Investments;

/// <summary>DTO de query Dapper. Currency como string (Dapper no convierte CHAR(3)→enum).</summary>
public record CompanyListItemDto(
    int IdCompany, string Name, string Ticker, string? Sector, string? Market,
    string Currency, decimal? LastPrice, DateOnly? LastValuationDate);
