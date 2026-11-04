using FluentAssertions;
using Subscript.Domain.Common;
using Subscript.Domain.Plans;
using Subscript.Domain.Subscriptions;

namespace Subscript.Domain.UnitTests;

public class SubscriptionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid CustomerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid PlanId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherPlanId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void StartTrial_SetsThirtyDayTrialAndNextBillingDate()
    {
        var start = new DateOnly(2026, 1, 1);

        var subscription = Subscription.StartTrial(CustomerId, PlanId, BillingPeriod.Monthly, start, Now);

        subscription.Status.Should().Be(SubscriptionStatus.Trialing);
        subscription.TrialEndDate.Should().Be(new DateOnly(2026, 1, 31));
        subscription.NextBillingDate.Should().Be(new DateOnly(2026, 1, 31));
        subscription.BillingAnchorDay.Should().Be(1);
        subscription.IsBlockingNewSubscription.Should().BeTrue();
    }

    [Fact]
    public void Cancel_SetsCancelledImmediately()
    {
        var subscription = Subscription.StartTrial(CustomerId, PlanId, BillingPeriod.Monthly, new DateOnly(2026, 1, 1), Now);

        subscription.Cancel(Now);

        subscription.Status.Should().Be(SubscriptionStatus.Cancelled);
        subscription.CancelledAt.Should().Be(Now);
        subscription.IsBlockingNewSubscription.Should().BeFalse();
    }

    [Fact]
    public void ExpireDueToPaymentFailure_SetsExpired()
    {
        var subscription = Subscription.StartTrial(CustomerId, PlanId, BillingPeriod.Monthly, new DateOnly(2026, 1, 1), Now);
        subscription.ActivateAfterTrial(BillingPeriod.Monthly, Now);

        subscription.ExpireDueToPaymentFailure(Now);

        subscription.Status.Should().Be(SubscriptionStatus.Expired);
        subscription.CancelledAt.Should().Be(Now);
    }

    [Fact]
    public void SchedulePlanChange_StoresPendingPlan()
    {
        var subscription = Subscription.StartTrial(CustomerId, PlanId, BillingPeriod.Monthly, new DateOnly(2026, 1, 1), Now);

        subscription.SchedulePlanChange(OtherPlanId, Now);

        subscription.PendingPlanId.Should().Be(OtherPlanId);
        subscription.PlanId.Should().Be(PlanId);
    }

    [Fact]
    public void AdvanceBillingPeriod_AppliesPendingPlanChange()
    {
        var subscription = Subscription.StartTrial(CustomerId, PlanId, BillingPeriod.Monthly, new DateOnly(2026, 1, 1), Now);
        subscription.ActivateAfterTrial(BillingPeriod.Monthly, Now);
        subscription.SchedulePlanChange(OtherPlanId, Now);

        subscription.AdvanceBillingPeriod(BillingPeriod.Monthly, Now);

        subscription.PlanId.Should().Be(OtherPlanId);
        subscription.PendingPlanId.Should().BeNull();
    }

    [Fact]
    public void Cancel_WhenAlreadyCancelled_Throws()
    {
        var subscription = Subscription.StartTrial(CustomerId, PlanId, BillingPeriod.Monthly, new DateOnly(2026, 1, 1), Now);
        subscription.Cancel(Now);

        var act = () => subscription.Cancel(Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("subscription_terminal");
    }

    [Fact]
    public void SoftDeleted_StillBlocksNewSubscription_WhenActiveOrTrialing()
    {
        var subscription = Subscription.StartTrial(CustomerId, PlanId, BillingPeriod.Monthly, new DateOnly(2026, 1, 1), Now);
        subscription.SoftDelete(Now);

        subscription.IsDeleted.Should().BeTrue();
        subscription.Status.Should().Be(SubscriptionStatus.Trialing);
        subscription.IsBlockingNewSubscription.Should().BeTrue();
    }
}
