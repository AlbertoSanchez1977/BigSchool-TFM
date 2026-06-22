using BigSchool.Application.DTOs.Investments;
using MediatR;

namespace BigSchool.Application.Queries.Investments.GetPortfolioById;

public record GetPortfolioByIdQuery(int IdPortfolio, int IdUser) : IRequest<PortfolioDetailDto?>;
