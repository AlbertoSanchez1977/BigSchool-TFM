using BigSchool.Application.DTOs.Investments;
using MediatR;

namespace BigSchool.Application.Commands.Investments.SellShares;

public record SellSharesCommand(
    int PortfolioId,
    int IdUser,
    int IdCompany,
    decimal Shares,
    decimal SellPrice,
    DateOnly SellDate,
    string? Notes) : IRequest<SellSharesResultDto>;
