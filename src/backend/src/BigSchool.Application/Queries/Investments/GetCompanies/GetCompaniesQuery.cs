using BigSchool.Application.DTOs.Investments;
using MediatR;

namespace BigSchool.Application.Queries.Investments.GetCompanies;

public record GetCompaniesQuery(string? Sector, string? Market) : IRequest<IReadOnlyList<CompanyListItemDto>>;
