using MediatR;

namespace BigSchool.Application.Commands.Investments.DeleteHolding;

public record DeleteHoldingCommand(int PortfolioId, int IdUser, int HoldingId) : IRequest;
