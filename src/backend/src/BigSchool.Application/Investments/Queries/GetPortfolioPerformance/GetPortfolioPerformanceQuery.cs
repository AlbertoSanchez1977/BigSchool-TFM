using BigSchool.Application.Investments.DTOs;
using MediatR;

namespace BigSchool.Application.Investments.Queries.GetPortfolioPerformance;

public record GetPortfolioPerformanceQuery(int IdPortfolio, int IdUser) : IRequest<PortfolioPerformanceDto?>;
