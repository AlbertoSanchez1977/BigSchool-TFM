using BigSchool.Application.DTOs.Investments;
using BigSchool.Domain.Investments.Enums;
using MediatR;

namespace BigSchool.Application.Queries.Investments.GetCompanies;

public record GetCompaniesQuery(Sector? Sector, Market? Market) : IRequest<IReadOnlyList<CompanyListItemDto>>;
