using BigSchool.Application.DTOs.Investments;
using MediatR;

namespace BigSchool.Application.Commands.Investments.AddValuation;

public record AddValuationCommand(int IdCompany, decimal Price, DateOnly Date, string? Source)
    : IRequest<ValuationDto>;
