using BigSchool.Application.Investments.DTOs;
using BigSchool.Domain.Investments.Enums;
using MediatR;

namespace BigSchool.Application.Investments.Queries.GetCompanyValuationSeries;

public record GetCompanyValuationSeriesQuery(int IdCompany, ValuationPeriod Period) : IRequest<ValuationSeriesDto>;
