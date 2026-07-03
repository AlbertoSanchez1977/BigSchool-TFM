using BigSchool.Application.SharedKernel.Common;
using FluentAssertions;
using Xunit;

namespace BigSchool.Application.Tests.SharedKernel;

public class PaginationTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(1, 1)]
    [InlineData(3, 3)]
    public void NormalizePage_fuerza_minimo_1(int input, int expected)
        => Pagination.NormalizePage(input).Should().Be(expected);

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(50, 50)]
    [InlineData(100, 100)]
    [InlineData(101, 100)]
    [InlineData(500, 100)]
    public void NormalizePageSize_aplica_default_y_tope(int input, int expected)
        => Pagination.NormalizePageSize(input).Should().Be(expected);
}
