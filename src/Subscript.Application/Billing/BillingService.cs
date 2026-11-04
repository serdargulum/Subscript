using Microsoft.EntityFrameworkCore;
using Subscript.Application.Abstractions;
using Subscript.Domain.Common;
using Subscript.Domain.Invoices;
using Subscript.Domain.Subscriptions;

namespace Subscript.Application.Billing;

public sealed class BillingService(ISubscriptDbContext db, IClock clock)
{
    public async Task<int> ProcessDueSubscriptionsAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);

        var dueSubscriptions = await db.Subscriptions
            .Where(x =>
                !x.IsDeleted &&
                (x.Status == SubscriptionStatus.Active || x.Status == SubscriptionStatus.Trialing) &&
                x.NextBillingDate <= today)
            .OrderBy(x => x.NextBillingDate)
            .Take(100)
            .ToListAsync(cancellationToken);

        var processed = 0;

        foreach (var subscription in dueSubscriptions)
        {
            var plan = await db.Plans.FirstOrDefaultAsync(x => x.Id == subscription.PlanId, cancellationToken);
            if (plan is null)
            {
                continue;
            }

            var effectivePlan = plan;
            if (subscription.PendingPlanId is Guid pendingPlanId)
            {
                var pending = await db.Plans.FirstOrDefaultAsync(x => x.Id == pendingPlanId, cancellationToken);
                if (pending is not null)
                {
                    effectivePlan = pending;
                }
            }

            var periodStart = subscription.Status == SubscriptionStatus.Trialing
                ? subscription.TrialEndDate ?? subscription.StartDate
                : subscription.CurrentPeriodEnd;

            var periodEnd = BillingDateCalculator.AddBillingPeriod(
                periodStart,
                effectivePlan.BillingPeriod,
                subscription.BillingAnchorDay);

            var existing = await db.Invoices.AnyAsync(
                x => x.SubscriptionId == subscription.Id &&
                     x.PeriodStart == periodStart &&
                     x.PeriodEnd == periodEnd,
                cancellationToken);
            if (existing)
            {
                subscription.AdvanceBillingPeriod(effectivePlan.BillingPeriod, clock.UtcNow);
                await db.SaveChangesAsync(cancellationToken);
                processed++;
                continue;
            }

            var invoiceDate = today;
            var dueDate = today.AddDays(7);
            var invoice = Invoice.CreateForPeriod(
                subscription.Id,
                invoiceDate,
                dueDate,
                periodStart,
                periodEnd,
                effectivePlan.Price,
                effectivePlan.Currency,
                effectivePlan.Name,
                effectivePlan.BillingPeriod,
                clock.UtcNow);

            db.Invoices.Add(invoice);
            subscription.AdvanceBillingPeriod(effectivePlan.BillingPeriod, clock.UtcNow);

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                processed++;
            }
            catch (DbUpdateException)
            {
                // Concurrent billing produced a duplicate period invoice.
                // Drop the failed entities so the remaining batch can still be saved.
                db.Detach(invoice);
                db.Detach(subscription);
            }
        }

        return processed;
    }
}
