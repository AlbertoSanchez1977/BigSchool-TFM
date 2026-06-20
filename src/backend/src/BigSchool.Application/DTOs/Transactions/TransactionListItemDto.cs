namespace BigSchool.Application.DTOs.Transactions;

public record TransactionListItemDto(
    int IdTransaction,
    short Type,
    int IdMainCategory,
    int? IdSubCategory,
    string? Description,
    DateOnly TransactionDate,
    decimal OriginalAmount,
    string OriginalCurrency,
    decimal ExchangeRate,
    decimal BaseAmount,
    string BaseCurrency,
    DateOnly RateDate);
