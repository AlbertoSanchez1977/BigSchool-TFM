using BigSchool.Domain.Enums;

namespace BigSchool.Application.DTOs.Transactions;

public record TransactionDto(
    int IdTransaction,
    TransactionType Type,
    MainCategory IdMainCategory,
    int? IdSubCategory,
    string? Description,
    DateOnly TransactionDate,
    decimal OriginalAmount,
    Currency OriginalCurrency,
    decimal ExchangeRate,
    decimal BaseAmount,
    Currency BaseCurrency,
    DateOnly RateDate);
