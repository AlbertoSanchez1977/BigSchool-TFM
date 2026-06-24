using BigSchool.Application.DTOs.Investments;
using MediatR;

namespace BigSchool.Application.Queries.Investments.GetPortfolios;

public record GetPortfoliosQuery(int IdUser) : IRequest<IReadOnlyList<PortfolioListItemDto>>;
