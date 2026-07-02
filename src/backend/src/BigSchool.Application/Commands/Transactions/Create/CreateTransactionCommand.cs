using BigSchool.Application.DTOs.Transactions;
using BigSchool.Domain.Enums;
using BigSchool.Domain.SharedKernel.Enums;
using MediatR;

namespace BigSchool.Application.Commands.Transactions.Create;

public record CreateTransactionCommand(
    int IdUser,
    TransactionType Type,
    MainCategory IdMainCategory,
    int? IdSubCategory,
    string? Description,
    DateOnly TransactionDate,
    decimal Amount,
    Currency? Currency) : IRequest<TransactionDto>;
