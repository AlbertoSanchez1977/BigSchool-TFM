using MediatR;

namespace BigSchool.Application.Investments.Commands.DeletePortfolio;

public record DeletePortfolioCommand(int IdPortfolio, int IdUser) : IRequest;
