namespace BigSchool.Application.Finance.Queries.Transactions.GetMonthlyChart;

public record MonthlyChartPointDto(int Year, int Month, decimal Income, decimal Expense);
