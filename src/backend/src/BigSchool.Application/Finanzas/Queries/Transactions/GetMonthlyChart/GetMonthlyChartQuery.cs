using MediatR;

namespace BigSchool.Application.Finanzas.Queries.Transactions.GetMonthlyChart;

public record GetMonthlyChartQuery(int IdUser, int Year) : IRequest<IReadOnlyList<MonthlyChartPointDto>>;
