using BigSchool.Application.DTOs.Investments;
using MediatR;

namespace BigSchool.Application.Queries.Investments.GetCompanyById;

public record GetCompanyByIdQuery(int IdCompany) : IRequest<CompanyListItemDto?>;
