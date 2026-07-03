using BigSchool.Application.Investments.DTOs;
using BigSchool.Application.SharedKernel.Common;
using BigSchool.Domain.Investments.Enums;
using MediatR;

namespace BigSchool.Application.Investments.Queries.GetCompanies;

public record GetCompaniesQuery(Sector? Sector, Market? Market, int Page, int PageSize)
    : IRequest<PagedResult<CompanyListItemDto>>;
