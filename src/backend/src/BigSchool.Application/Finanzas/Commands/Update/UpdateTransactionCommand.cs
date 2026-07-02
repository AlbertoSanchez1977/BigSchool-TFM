using BigSchool.Application.Finanzas.DTOs;
using BigSchool.Domain.Finanzas.Enums;
using BigSchool.Domain.SharedKernel.Enums;
using MediatR;

namespace BigSchool.Application.Finanzas.Commands.Update;

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
