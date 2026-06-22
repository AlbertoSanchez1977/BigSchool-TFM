using BigSchool.Application.DTOs.Investments;
using MediatR;

namespace BigSchool.Application.Commands.Investments.UpdateHolding;

public record UpdateHoldingCommand(int PortfolioId, int IdUser, int HoldingId, string? Notes)
    : IRequest<HoldingDto>;
