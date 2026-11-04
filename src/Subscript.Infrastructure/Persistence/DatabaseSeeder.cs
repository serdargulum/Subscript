using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Subscript.Domain.Common;
using Subscript.Domain.Customers;
using Subscript.Domain.Plans;
using Subscript.Domain.Subscriptions;

namespace Subscript.Infrastructure.Persistence;

/// <summary>
/// Seeds a small, deterministic demo dataset the first time the database is empty.
/// </summary>
public static class DatabaseSeeder
{
    public static async Task SeedAsync(
        SubscriptDbContext db,
        IClock clock,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (await db.Plans.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = clock.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var starter = Plan.Create("Starter", "For small teams getting started.", 19m, BillingPeriod.Monthly, now);
        var growth = Plan.Create("Growth", "For growing product teams.", 49m, BillingPeriod.Monthly, now);
        var scale = Plan.Create("Scale", "Annual plan with priority support.", 499m, BillingPeriod.Yearly, now);

        var ada = Customer.Create("Ada Lovelace", "ada@analytical.dev", "Analytical Engines", now);
        var grace = Customer.Create("Grace Hopper", "grace@navy.mil", "US Navy", now);
        var alan = Customer.Create("Alan Turing", "alan@bletchley.uk", "Bletchley Park", now);

        db.Plans.AddRange(starter, growth, scale);
        db.Customers.AddRange(ada, grace, alan);

        var due = Subscription.StartTrial(ada.Id, growth.Id, growth.BillingPeriod, today.AddDays(-40), now);
        var trialing = Subscription.StartTrial(grace.Id, starter.Id, starter.BillingPeriod, today.AddDays(-5), now);
        var cancelled = Subscription.StartTrial(alan.Id, scale.Id, scale.BillingPeriod, today.AddDays(-10), now);
        cancelled.Cancel(now);

        db.Subscriptions.AddRange(due, trialing, cancelled);

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Seeded {PlanCount} plans, {CustomerCount} customers and {SubscriptionCount} subscriptions.",
            3,
            3,
            3);
    }
}
