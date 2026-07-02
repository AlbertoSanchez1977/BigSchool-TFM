using BigSchool.Application.Investments.DTOs;
using MediatR;

namespace BigSchool.Application.Investments.Commands.CreatePortfolio;

public record CreatePortfolioCommand(int IdUser, string Name) : IRequest<PortfolioDto>;
