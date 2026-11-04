using FluentAssertions;
using Subscript.Application.Plans;
using Subscript.Application.Subscriptions;
using Subscript.Domain.Plans;
using Subscript.Domain.Subscriptions;

namespace Subscript.Application.UnitTests;

public class BillingServiceTests
{
    [Fact]
    public async Task DueTrial_CreatesFirstInvoice_AndActivatesSubscription()
    {
        using var harness = new TestHarness();
        var (_, _, subscription) = await harness.CreateDueSubscriptionAsync();

        var processed = await harness.Billing.ProcessDueSubscriptionsAsync(CancellationToken.None);

        var invoices = await harness.Invoices.ListAsync(1, 10, null, null, false, CancellationToken.None);
        var updated = await harness.Subscriptions.GetAsync(subscription.Id, false, CancellationToken.None);

        processed.Should().Be(1);
        invoices.Items.Should().ContainSingle();
        invoices.Items[0].PeriodStart.Should().Be(new DateOnly(2026, 11, 2));
        invoices.Items[0].PeriodEnd.Should().Be(new DateOnly(2026, 12, 3));
        invoices.Items[0].Amount.Should().Be(29.99m);
        invoices.Items[0].PlanNameSnapshot.Should().Be("Pro");
        updated.Status.Should().Be(SubscriptionStatus.Active);
        updated.NextBillingDate.Should().Be(new DateOnly(2026, 12, 3));
        updated.CurrentPeriodStart.Should().Be(new DateOnly(2026, 11, 2));
    }

    [Fact]
    public async Task SecondRun_SameDay_DoesNotDuplicateInvoice()
    {
        using var harness = new TestHarness();
        await harness.CreateDueSubscriptionAsync();

        await harness.Billing.ProcessDueSubscriptionsAsync(CancellationToken.None);
        var second = await harness.Billing.ProcessDueSubscriptionsAsync(CancellationToken.None);

        var invoices = await harness.Invoices.ListAsync(1, 10, null, null, false, CancellationToken.None);

        second.Should().Be(0);
        invoices.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task YearlyPlan_BillsFullYearPeriod()
    {
        using var harness = new TestHarness();
        await harness.CreateDueSubscriptionAsync(499m, BillingPeriod.Yearly);

        await harness.Billing.ProcessDueSubscriptionsAsync(CancellationToken.None);

        var invoices = await harness.Invoices.ListAsync(1, 10, null, null, false, CancellationToken.None);

        invoices.Items[0].Amount.Should().Be(499m);
        invoices.Items[0].BillingPeriodSnapshot.Should().Be(BillingPeriod.Yearly);
        invoices.Items[0].PeriodEnd.Should().Be(new DateOnly(2027, 11, 3));
    }

    [Fact]
    public async Task PendingPlanChange_IsAppliedOnNextBillingRun()
    {
        using var harness = new TestHarness();
        var (_, _, subscription) = await harness.CreateDueSubscriptionAsync();
        var nextPlan = await harness.CreatePlanAsync(99m, name: "Enterprise");
        await harness.Subscriptions.ChangePlanAsync(
            subscription.Id,
            new ChangePlanRequest(nextPlan.Id),
            CancellationToken.None);

        await harness.Billing.ProcessDueSubscriptionsAsync(CancellationToken.None);

        var invoices = await harness.Invoices.ListAsync(1, 10, null, null, false, CancellationToken.None);
        var updated = await harness.Subscriptions.GetAsync(subscription.Id, false, CancellationToken.None);

        invoices.Items[0].Amount.Should().Be(99m);
        invoices.Items[0].PlanNameSnapshot.Should().Be("Enterprise");
        updated.PlanId.Should().Be(nextPlan.Id);
        updated.PendingPlanId.Should().BeNull();
    }

    [Fact]
    public async Task CancelledSubscription_IsNotBilled()
    {
        using var harness = new TestHarness();
        var (_, _, subscription) = await harness.CreateDueSubscriptionAsync();
        await harness.Subscriptions.CancelAsync(subscription.Id, CancellationToken.None);

        var processed = await harness.Billing.ProcessDueSubscriptionsAsync(CancellationToken.None);

        var invoices = await harness.Invoices.ListAsync(1, 10, null, null, false, CancellationToken.None);

        processed.Should().Be(0);
        invoices.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task TrialNotYetDue_IsNotBilled()
    {
        using var harness = new TestHarness();
        var customer = await harness.CreateCustomerAsync();
        var plan = await harness.CreatePlanAsync();
        await harness.Subscriptions.CreateAsync(
            new CreateSubscriptionRequest(customer.Id, plan.Id, null),
            CancellationToken.None);

        var processed = await harness.Billing.ProcessDueSubscriptionsAsync(CancellationToken.None);

        var invoices = await harness.Invoices.ListAsync(1, 10, null, null, false, CancellationToken.None);

        processed.Should().Be(0);
        invoices.Items.Should().BeEmpty();
    }
}
