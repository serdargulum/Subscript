using FluentAssertions;
using Subscript.Application.Common;
using Subscript.Application.Plans;
using Subscript.Application.Subscriptions;
using Subscript.Domain.Subscriptions;

namespace Subscript.Application.UnitTests;

public class SubscriptionServiceTests
{
    [Fact]
    public async Task Create_StartsThirtyDayTrial()
    {
        using var harness = new TestHarness();
        var customer = await harness.CreateCustomerAsync();
        var plan = await harness.CreatePlanAsync();
        var start = new DateOnly(2026, 11, 3);

        var subscription = await harness.Subscriptions.CreateAsync(
            new CreateSubscriptionRequest(customer.Id, plan.Id, start),
            CancellationToken.None);

        subscription.Status.Should().Be(SubscriptionStatus.Trialing);
        subscription.StartDate.Should().Be(start);
        subscription.TrialEndDate.Should().Be(start.AddDays(30));
        subscription.NextBillingDate.Should().Be(start.AddDays(30));
        subscription.PendingPlanId.Should().BeNull();
    }

    [Fact]
    public async Task Create_SecondActiveSubscription_IsConflict()
    {
        using var harness = new TestHarness();
        var customer = await harness.CreateCustomerAsync();
        var plan = await harness.CreatePlanAsync();
        await harness.Subscriptions.CreateAsync(
            new CreateSubscriptionRequest(customer.Id, plan.Id, null),
            CancellationToken.None);

        var act = () => harness.Subscriptions.CreateAsync(
            new CreateSubscriptionRequest(customer.Id, plan.Id, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .Where(x => x.Code == "subscription_already_active");
    }

    [Fact]
    public async Task Create_AgainstDeletedPlan_ThrowsNotFound()
    {
        using var harness = new TestHarness();
        var customer = await harness.CreateCustomerAsync();
        var plan = await harness.CreatePlanAsync();
        await harness.Plans.SoftDeleteAsync(plan.Id, CancellationToken.None);

        var act = () => harness.Subscriptions.CreateAsync(
            new CreateSubscriptionRequest(customer.Id, plan.Id, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .Where(x => x.Code == "plan_not_found");
    }

    [Fact]
    public async Task Create_ForDeletedCustomer_ThrowsNotFound()
    {
        using var harness = new TestHarness();
        var plan = await harness.CreatePlanAsync();

        var act = () => harness.Subscriptions.CreateAsync(
            new CreateSubscriptionRequest(Guid.NewGuid(), plan.Id, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .Where(x => x.Code == "customer_not_found");
    }

    [Fact]
    public async Task Cancel_IsImmediateAndAllowsNewSubscription()
    {
        using var harness = new TestHarness();
        var customer = await harness.CreateCustomerAsync();
        var plan = await harness.CreatePlanAsync();
        var subscription = await harness.Subscriptions.CreateAsync(
            new CreateSubscriptionRequest(customer.Id, plan.Id, null),
            CancellationToken.None);

        var cancelled = await harness.Subscriptions.CancelAsync(subscription.Id, CancellationToken.None);
        var replacement = await harness.Subscriptions.CreateAsync(
            new CreateSubscriptionRequest(customer.Id, plan.Id, null),
            CancellationToken.None);

        cancelled.Status.Should().Be(SubscriptionStatus.Cancelled);
        replacement.Status.Should().Be(SubscriptionStatus.Trialing);
    }

    [Fact]
    public async Task ChangePlan_SchedulesPendingPlanUntilNextBillingDate()
    {
        using var harness = new TestHarness();
        var customer = await harness.CreateCustomerAsync();
        var plan = await harness.CreatePlanAsync();
        var nextPlan = await harness.CreatePlanAsync(99m, name: "Enterprise");
        var subscription = await harness.Subscriptions.CreateAsync(
            new CreateSubscriptionRequest(customer.Id, plan.Id, null),
            CancellationToken.None);

        var changed = await harness.Subscriptions.ChangePlanAsync(
            subscription.Id,
            new ChangePlanRequest(nextPlan.Id),
            CancellationToken.None);

        changed.PlanId.Should().Be(plan.Id);
        changed.PendingPlanId.Should().Be(nextPlan.Id);
    }

    [Fact]
    public async Task ChangePlan_ToDeletedPlan_ThrowsNotFound()
    {
        using var harness = new TestHarness();
        var customer = await harness.CreateCustomerAsync();
        var plan = await harness.CreatePlanAsync();
        var deletedPlan = await harness.CreatePlanAsync(99m, name: "Enterprise");
        await harness.Plans.SoftDeleteAsync(deletedPlan.Id, CancellationToken.None);
        var subscription = await harness.Subscriptions.CreateAsync(
            new CreateSubscriptionRequest(customer.Id, plan.Id, null),
            CancellationToken.None);

        var act = () => harness.Subscriptions.ChangePlanAsync(
            subscription.Id,
            new ChangePlanRequest(deletedPlan.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .Where(x => x.Code == "plan_not_found");
    }

    [Fact]
    public async Task ListByCustomer_PagesResults()
    {
        using var harness = new TestHarness();
        var customer = await harness.CreateCustomerAsync();
        var plan = await harness.CreatePlanAsync();
        var subscription = await harness.Subscriptions.CreateAsync(
            new CreateSubscriptionRequest(customer.Id, plan.Id, null),
            CancellationToken.None);
        await harness.Subscriptions.CancelAsync(subscription.Id, CancellationToken.None);
        await harness.Subscriptions.CreateAsync(
            new CreateSubscriptionRequest(customer.Id, plan.Id, null),
            CancellationToken.None);

        var page = await harness.Subscriptions.ListByCustomerAsync(
            customer.Id,
            1,
            1,
            false,
            CancellationToken.None);

        page.TotalCount.Should().Be(2);
        page.Items.Should().ContainSingle();
    }
}
