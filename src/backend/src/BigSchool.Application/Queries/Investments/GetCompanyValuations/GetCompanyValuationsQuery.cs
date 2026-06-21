using BigSchool.Application.DTOs.Investments;
using MediatR;

namespace BigSchool.Application.Queries.Investments.GetCompanyValuations;

public record GetCompanyValuationsQuery(int IdCompany) : IRequest<IReadOnlyList<ValuationListItemDto>>;
