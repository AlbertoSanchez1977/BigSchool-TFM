using BigSchool.Application.Finance.DTOs;
using BigSchool.Domain.Finance.Enums;
using BigSchool.Domain.SharedKernel.Enums;
using MediatR;

namespace BigSchool.Application.Finance.Commands.Create;

public record CreateTransactionCommand(
    int IdUser,
    TransactionType Type,
    MainCategory IdMainCategory,
    int? IdSubCategory,
    string? Description,
    DateOnly TransactionDate,
    decimal Amount,
    Currency? Currency) : IRequest<TransactionDto>;
