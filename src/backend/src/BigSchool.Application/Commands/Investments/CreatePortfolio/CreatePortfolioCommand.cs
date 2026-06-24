using BigSchool.Application.DTOs.Investments;
using MediatR;

namespace BigSchool.Application.Commands.Investments.CreatePortfolio;

public record CreatePortfolioCommand(int IdUser, string Name) : IRequest<PortfolioDto>;
