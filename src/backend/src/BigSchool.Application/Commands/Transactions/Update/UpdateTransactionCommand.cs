using BigSchool.Application.DTOs.Transactions;
using BigSchool.Domain.Enums;
using MediatR;

namespace BigSchool.Application.Commands.Transactions.Update;

public record UpdateTransactionCommand(
    int IdTransaction,
    int IdUser,
    TransactionType Type,
    MainCategory IdMainCategory,
    int? IdSubCategory,
    string? Description,
    DateOnly TransactionDate,
    decimal Amount,
    Currency? Currency) : IRequest<TransactionDto>;
