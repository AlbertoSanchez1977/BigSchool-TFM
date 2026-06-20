using BigSchool.Application.Queries.Transactions.GetTransactions;
using FluentAssertions;
using Xunit;

namespace BigSchool.Application.Tests.Queries.Transactions;

public class GetTransactionsQueryTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(3, 3)]
    public void NormalizePage_ClampsToMinimumOne(int input, int expected)
    {
        GetTransactionsQuery.NormalizePage(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(500, 100)]
    [InlineData(25, 25)]
    public void NormalizePageSize_ClampsBetween1And100(int input, int expected)
    {
        GetTransactionsQuery.NormalizePageSize(input).Should().Be(expected);
    }
}
