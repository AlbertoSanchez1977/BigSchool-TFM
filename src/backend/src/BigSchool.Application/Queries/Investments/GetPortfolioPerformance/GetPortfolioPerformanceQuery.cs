using BigSchool.Application.DTOs.Investments;
using MediatR;

namespace BigSchool.Application.Queries.Investments.GetPortfolioPerformance;

public record GetPortfolioPerformanceQuery(int IdPortfolio, int IdUser) : IRequest<PortfolioPerformanceDto?>;
