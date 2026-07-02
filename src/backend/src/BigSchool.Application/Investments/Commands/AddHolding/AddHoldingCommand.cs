using BigSchool.Application.Investments.DTOs;
using MediatR;

namespace BigSchool.Application.Investments.Commands.AddHolding;

public record AddHoldingCommand(
    int PortfolioId,
    int IdUser,
    int IdCompany,
    decimal Shares,
    decimal BuyPrice,
    DateOnly BuyDate,
    string? Notes) : IRequest<HoldingDto>;
