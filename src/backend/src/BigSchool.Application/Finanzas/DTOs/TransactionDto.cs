using BigSchool.Domain.Finanzas.Enums;
using BigSchool.Domain.SharedKernel.Enums;

namespace BigSchool.Application.Finanzas.DTOs;

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
