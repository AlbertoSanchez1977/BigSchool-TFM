using BigSchool.Application.Finance.Queries.Transactions.GetMonthlyChart;
using BigSchool.Domain.Finance.Enums;
using MediatR;

namespace BigSchool.Application.Finance.Queries.Transactions.GetMonthly;

public record GetMonthlyQuery(int IdUser, DateOnly? From, DateOnly? To, MainCategory? Category, TransactionType? Type)
    : IRequest<IReadOnlyList<MonthlyChartPointDto>>;
