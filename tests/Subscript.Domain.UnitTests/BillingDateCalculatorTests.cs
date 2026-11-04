using FluentAssertions;
using Subscript.Domain.Plans;
using Subscript.Domain.Subscriptions;

namespace Subscript.Domain.UnitTests;

public class BillingDateCalculatorTests
{
    [Fact]
    public void AddTrialPeriod_AddsExactlyThirtyDays()
    {
        var start = new DateOnly(2026, 1, 15);

        var trialEnd = BillingDateCalculator.AddTrialPeriod(start);

        trialEnd.Should().Be(new DateOnly(2026, 2, 14));
    }

    [Fact]
    public void Monthly_FromJan31_GoesToFebEnd_ThenPrefers31WhenPossible()
    {
        var jan31 = new DateOnly(2026, 1, 31);
        var anchor = BillingDateCalculator.ResolveAnchorDay(jan31);

        var feb = BillingDateCalculator.AddBillingPeriod(jan31, BillingPeriod.Monthly, anchor);
        var mar = BillingDateCalculator.AddBillingPeriod(feb, BillingPeriod.Monthly, anchor);
        var apr = BillingDateCalculator.AddBillingPeriod(mar, BillingPeriod.Monthly, anchor);

        feb.Should().Be(new DateOnly(2026, 2, 28));
        mar.Should().Be(new DateOnly(2026, 3, 31));
        apr.Should().Be(new DateOnly(2026, 4, 30));
    }

    [Fact]
    public void Yearly_LeapDay_ClampsInNonLeapYear()
    {
        var leapStart = new DateOnly(2024, 2, 29);
        var anchor = BillingDateCalculator.ResolveAnchorDay(leapStart);

        var next = BillingDateCalculator.AddBillingPeriod(leapStart, BillingPeriod.Yearly, anchor);

        next.Should().Be(new DateOnly(2025, 2, 28));
    }
}
