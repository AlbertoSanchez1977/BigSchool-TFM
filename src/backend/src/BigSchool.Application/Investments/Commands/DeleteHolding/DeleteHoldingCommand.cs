using MediatR;

namespace BigSchool.Application.Investments.Commands.DeleteHolding;

public record DeleteHoldingCommand(int PortfolioId, int IdUser, int HoldingId) : IRequest;
