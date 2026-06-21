using BigSchool.Application.DTOs.Investments;
using BigSchool.Domain.Enums;
using MediatR;

namespace BigSchool.Application.Commands.Investments.CreateCompany;
public record CreateCompanyCommand(string Name, string Ticker, Sector? Sector, Market? Market, Currency Currency)
    : IRequest<CompanyDto>;
