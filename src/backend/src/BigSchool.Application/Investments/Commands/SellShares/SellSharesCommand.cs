using BigSchool.Application.Investments.DTOs;
using MediatR;

namespace BigSchool.Application.Investments.Commands.SellShares;

public record SellSharesCommand(
    int PortfolioId,
    int IdUser,
    int IdCompany,
    decimal Shares,
    decimal SellPrice,
    DateOnly SellDate,
    string? Notes) : IRequest<SellSharesResultDto>;
