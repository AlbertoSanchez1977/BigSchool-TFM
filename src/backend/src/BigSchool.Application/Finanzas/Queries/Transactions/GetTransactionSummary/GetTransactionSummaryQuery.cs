using MediatR;

namespace BigSchool.Application.Finanzas.Queries.Transactions.GetTransactionSummary;

public record GetTransactionSummaryQuery(int IdUser, DateOnly? From, DateOnly? To) : IRequest<TransactionSummaryDto>;
