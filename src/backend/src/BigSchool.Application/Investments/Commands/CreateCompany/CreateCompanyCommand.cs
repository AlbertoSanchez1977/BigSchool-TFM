using BigSchool.Application.Investments.DTOs;
using BigSchool.Domain.Investments.Enums;
using BigSchool.Domain.SharedKernel.Enums;
using MediatR;

namespace BigSchool.Application.Investments.Commands.CreateCompany;
public record CreateCompanyCommand(string Name, string Ticker, Sector? Sector, Market? Market, Currency Currency)
    : IRequest<CompanyDto>;
