using BigSchool.Application.Investments.DTOs;
using MediatR;

namespace BigSchool.Application.Investments.Queries.GetPortfolioById;

public record GetPortfolioByIdQuery(int IdPortfolio, int IdUser) : IRequest<PortfolioDetailDto?>;
