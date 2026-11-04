using Subscript.Domain.Plans;

namespace Subscript.Domain.Subscriptions;

/// <summary>
/// Calendar-based billing period calculations with end-of-month anchoring.
/// Trial length is fixed at 30 days and does not affect calendar billing periods.
/// </summary>
public static class BillingDateCalculator
{
    public const int TrialDays = 30;

    public static DateOnly AddTrialPeriod(DateOnly startDate) =>
        startDate.AddDays(TrialDays);

    public static DateOnly AddBillingPeriod(DateOnly fromDate, BillingPeriod period, int billingAnchorDay)
    {
        return period switch
        {
            BillingPeriod.Monthly => AddMonthsPreservingAnchor(fromDate, 1, billingAnchorDay),
            BillingPeriod.Yearly => AddYearsPreservingAnchor(fromDate, 1, billingAnchorDay),
            _ => throw new ArgumentOutOfRangeException(nameof(period), period, "Unsupported billing period.")
        };
    }

    public static int ResolveAnchorDay(DateOnly startDate) => startDate.Day;

    private static DateOnly AddMonthsPreservingAnchor(DateOnly fromDate, int months, int billingAnchorDay)
    {
        var target = fromDate.AddMonths(months);
        return ClampToAnchorDay(target.Year, target.Month, billingAnchorDay);
    }

    private static DateOnly AddYearsPreservingAnchor(DateOnly fromDate, int years, int billingAnchorDay)
    {
        var target = fromDate.AddYears(years);
        return ClampToAnchorDay(target.Year, target.Month, billingAnchorDay);
    }

    private static DateOnly ClampToAnchorDay(int year, int month, int billingAnchorDay)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var day = Math.Min(billingAnchorDay, daysInMonth);
        return new DateOnly(year, month, day);
    }
}
