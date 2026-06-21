using BigSchool.Application.DTOs.Investments;
using BigSchool.Domain.Enums;
using MediatR;

namespace BigSchool.Application.Commands.Investments.CreateCompany;
public record CreateCompanyCommand(string Name, string Ticker, string? Sector, string? Market, Currency Currency)
    : IRequest<CompanyDto>;
