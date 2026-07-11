using BigSchool.Application.Investments.DTOs;
using MediatR;

namespace BigSchool.Application.Investments.Commands.UpdateHolding;

public record UpdateHoldingCommand(int PortfolioId, int IdUser, int HoldingId, string? Notes)
    : IRequest<HoldingDto>;
