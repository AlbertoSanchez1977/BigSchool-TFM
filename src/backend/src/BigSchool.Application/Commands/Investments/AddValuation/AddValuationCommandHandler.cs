using BigSchool.Application.DTOs.Investments;
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Domain.Entities;
using BigSchool.Domain.SharedKernel.Exceptions;
using MediatR;

namespace BigSchool.Application.Commands.Investments.AddValuation;

public class AddValuationCommandHandler : IRequestHandler<AddValuationCommand, ValuationDto>
{
    private readonly ICompanyRepository _companyRepository;

    public AddValuationCommandHandler(ICompanyRepository companyRepository) => _companyRepository = companyRepository;

    public async Task<ValuationDto> Handle(AddValuationCommand request, CancellationToken cancellationToken)
    {
        var company = await _companyRepository.GetByIdWithValuationsAsync(request.IdCompany, cancellationToken)
            ?? throw new NotFoundException(nameof(Company), request.IdCompany);

        var valuation = company.AddValuation(request.Price, request.Date, request.Source);
        await _companyRepository.UnitOfWork.SaveChangesAsync();

        return new ValuationDto(valuation.IdValuation, request.IdCompany, valuation.Price.Amount,
            valuation.Price.Currency, valuation.Date, valuation.Source);
    }
}
