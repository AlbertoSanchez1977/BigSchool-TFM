using BigSchool.Application.DTOs.Investments;
using MediatR;

namespace BigSchool.Application.Commands.Investments.AddHolding;

public record AddHoldingCommand(
    int PortfolioId,
    int IdUser,
    int IdCompany,
    decimal Shares,
    decimal BuyPrice,
    DateOnly BuyDate,
    string? Notes) : IRequest<HoldingDto>;
