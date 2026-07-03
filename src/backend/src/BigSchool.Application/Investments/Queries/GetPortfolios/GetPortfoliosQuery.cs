using BigSchool.Application.Investments.DTOs;
using BigSchool.Application.SharedKernel.Common;
using MediatR;

namespace BigSchool.Application.Investments.Queries.GetPortfolios;

public record GetPortfoliosQuery(int IdUser, int Page, int PageSize)
    : IRequest<PagedResult<PortfolioListItemDto>>;
