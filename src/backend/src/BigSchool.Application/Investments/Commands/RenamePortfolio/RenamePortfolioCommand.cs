using MediatR;

namespace BigSchool.Application.Investments.Commands.RenamePortfolio;

public record RenamePortfolioCommand(int IdPortfolio, int IdUser, string Name) : IRequest;
