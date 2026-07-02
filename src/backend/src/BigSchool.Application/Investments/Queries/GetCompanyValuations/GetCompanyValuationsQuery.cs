using BigSchool.Application.Investments.DTOs;
using MediatR;

namespace BigSchool.Application.Investments.Queries.GetCompanyValuations;

public record GetCompanyValuationsQuery(int IdCompany) : IRequest<IReadOnlyList<ValuationListItemDto>>;
