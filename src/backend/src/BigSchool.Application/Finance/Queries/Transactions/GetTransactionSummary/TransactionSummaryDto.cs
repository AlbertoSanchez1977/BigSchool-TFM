namespace BigSchool.Application.Finance.Queries.Transactions.GetTransactionSummary;

public record TransactionSummaryDto(decimal TotalIncome, decimal TotalExpense, decimal Balance, string BaseCurrency);
