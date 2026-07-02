using BigSchool.Application.Investments.DTOs;
using MediatR;

namespace BigSchool.Application.Investments.Queries.GetPortfolios;

public record GetPortfoliosQuery(int IdUser) : IRequest<IReadOnlyList<PortfolioListItemDto>>;
