using BigSchool.Application.Investments.DTOs;
using MediatR;

namespace BigSchool.Application.Investments.Queries.GetCompanyById;

public record GetCompanyByIdQuery(int IdCompany) : IRequest<CompanyListItemDto?>;
