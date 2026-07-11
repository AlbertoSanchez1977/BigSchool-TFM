using BigSchool.Application.Investments.DTOs;
using BigSchool.Application.SharedKernel.Common;
using MediatR;

namespace BigSchool.Application.Investments.Queries.GetCompanyValuations;

public record GetCompanyValuationsQuery(int IdCompany, int Page, int PageSize)
    : IRequest<PagedResult<ValuationListItemDto>>;
