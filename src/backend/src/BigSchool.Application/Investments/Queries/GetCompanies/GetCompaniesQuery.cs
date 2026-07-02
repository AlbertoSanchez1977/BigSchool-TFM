using BigSchool.Application.Investments.DTOs;
using BigSchool.Domain.Investments.Enums;
using MediatR;

namespace BigSchool.Application.Investments.Queries.GetCompanies;

public record GetCompaniesQuery(Sector? Sector, Market? Market) : IRequest<IReadOnlyList<CompanyListItemDto>>;
