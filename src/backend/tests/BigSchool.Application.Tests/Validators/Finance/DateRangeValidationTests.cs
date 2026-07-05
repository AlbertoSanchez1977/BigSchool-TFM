using BigSchool.Application.SharedKernel.Common;
using FluentAssertions;
using Xunit;

namespace BigSchool.Application.Tests.Validators.Finance;

public class DateRangeValidationTests
{
    [Fact] public void IsValid_OneDateMissing_ReturnsTrue() => DateRange.IsValid(new DateOnly(2026,1,1), null).Should().BeTrue();
    [Fact] public void IsValid_FromAfterTo_ReturnsFalse() => DateRange.IsValid(new DateOnly(2026,6,1), new DateOnly(2026,1,1)).Should().BeFalse();
    [Fact] public void IsValid_SpanExactly4Years_ReturnsTrue() => DateRange.IsValid(new DateOnly(2022,1,1), new DateOnly(2026,1,1)).Should().BeTrue();
    [Fact] public void IsValid_SpanMoreThan4Years_ReturnsFalse() => DateRange.IsValid(new DateOnly(2022,1,1), new DateOnly(2026,1,2)).Should().BeFalse();
}
