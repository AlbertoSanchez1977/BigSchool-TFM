using MediatR;

namespace BigSchool.Application.Finance.Queries.Transactions.GetMonthlyChart;

public record GetMonthlyChartQuery(int IdUser, int Year) : IRequest<IReadOnlyList<MonthlyChartPointDto>>;
