using BigSchool.Application.Investments.DTOs;
using MediatR;

namespace BigSchool.Application.Investments.Queries.GetInvestmentsSummary;

public record GetInvestmentsSummaryQuery(int IdUser) : IRequest<InvestmentsSummaryDto>;
