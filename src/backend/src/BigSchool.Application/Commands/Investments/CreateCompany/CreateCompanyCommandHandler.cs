using BigSchool.Application.DTOs.Investments;
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Domain.Entities;
using BigSchool.Domain.Exceptions;
using MediatR;

namespace BigSchool.Application.Commands.Investments.CreateCompany;
public class CreateCompanyCommandHandler : IRequestHandler<CreateCompanyCommand, CompanyDto>
{
    private readonly ICompanyRepository _companyRepository;

    public CreateCompanyCommandHandler(ICompanyRepository companyRepository) => _companyRepository = companyRepository;

    public async Task<CompanyDto> Handle(CreateCompanyCommand request, CancellationToken cancellationToken)
    {
        var existing = await _companyRepository.GetByTickerAsync(request.Ticker, cancellationToken);
        if (existing is not null)
            throw new DuplicateTickerDomainException(request.Ticker.Trim().ToUpperInvariant());

        var company = Company.Create(request.Name, request.Ticker, request.Sector, request.Market, request.Currency);
        await _companyRepository.AddAsync(company, cancellationToken);
        await _companyRepository.UnitOfWork.SaveChangesAsync();

        return new CompanyDto(company.IdCompany, company.Name, company.Ticker,
            company.Sector, company.Market, company.Currency);
    }
}
