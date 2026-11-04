using Subscript.Domain.Common;
using Subscript.Domain.Plans;

namespace Subscript.Domain.Subscriptions;

public sealed class Subscription : SoftDeletableEntity
{
    public Guid CustomerId { get; private set; }
    public Guid PlanId { get; private set; }
    public SubscriptionStatus Status { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly? TrialEndDate { get; private set; }
    public DateOnly NextBillingDate { get; private set; }
    public DateOnly CurrentPeriodStart { get; private set; }
    public DateOnly CurrentPeriodEnd { get; private set; }
    public int BillingAnchorDay { get; private set; }
    public Guid? PendingPlanId { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }

    private Subscription()
    {
    }

    public static Subscription StartTrial(
        Guid customerId,
        Guid planId,
        BillingPeriod billingPeriod,
        DateOnly startDate,
        DateTimeOffset utcNow)
    {
        if (customerId == Guid.Empty)
        {
            throw new DomainException("subscription_customer_required", "Customer is required.");
        }

        if (planId == Guid.Empty)
        {
            throw new DomainException("subscription_plan_required", "Plan is required.");
        }

        var trialEnd = BillingDateCalculator.AddTrialPeriod(startDate);
        var anchorDay = BillingDateCalculator.ResolveAnchorDay(startDate);

        var subscription = new Subscription
        {
            CustomerId = customerId,
            PlanId = planId,
            Status = SubscriptionStatus.Trialing,
            StartDate = startDate,
            TrialEndDate = trialEnd,
            NextBillingDate = trialEnd,
            CurrentPeriodStart = startDate,
            CurrentPeriodEnd = trialEnd,
            BillingAnchorDay = anchorDay
        };

        subscription.SetCreated(utcNow);
        _ = billingPeriod; // period applies after trial when converting to active billing
        return subscription;
    }

    public bool IsBlockingNewSubscription =>
        Status is SubscriptionStatus.Active or SubscriptionStatus.Trialing;

    public void SchedulePlanChange(Guid newPlanId, DateTimeOffset utcNow)
    {
        EnsureMutable();

        if (newPlanId == Guid.Empty)
        {
            throw new DomainException("subscription_plan_required", "Plan is required.");
        }

        if (newPlanId == PlanId)
        {
            PendingPlanId = null;
            Touch(utcNow);
            return;
        }

        PendingPlanId = newPlanId;
        Touch(utcNow);
    }

    public void Cancel(DateTimeOffset utcNow)
    {
        EnsureMutable();

        Status = SubscriptionStatus.Cancelled;
        CancelledAt = utcNow;
        PendingPlanId = null;
        Touch(utcNow);
    }

    public void ExpireDueToPaymentFailure(DateTimeOffset utcNow)
    {
        EnsureMutable();

        Status = SubscriptionStatus.Expired;
        CancelledAt = utcNow;
        PendingPlanId = null;
        Touch(utcNow);
    }

    public void ActivateAfterTrial(BillingPeriod billingPeriod, DateTimeOffset utcNow)
    {
        if (Status != SubscriptionStatus.Trialing)
        {
            throw new DomainException("subscription_not_trialing", "Subscription is not in trial.");
        }

        var periodStart = TrialEndDate ?? StartDate;
        var periodEnd = BillingDateCalculator.AddBillingPeriod(periodStart, billingPeriod, BillingAnchorDay);

        Status = SubscriptionStatus.Active;
        CurrentPeriodStart = periodStart;
        CurrentPeriodEnd = periodEnd;
        NextBillingDate = periodEnd;
        Touch(utcNow);
    }

    public void AdvanceBillingPeriod(BillingPeriod billingPeriod, DateTimeOffset utcNow)
    {
        if (Status is not (SubscriptionStatus.Active or SubscriptionStatus.Trialing))
        {
            throw new DomainException("subscription_not_billable", "Subscription cannot be billed in its current status.");
        }

        if (PendingPlanId.HasValue)
        {
            PlanId = PendingPlanId.Value;
            PendingPlanId = null;
        }

        if (Status == SubscriptionStatus.Trialing)
        {
            ActivateAfterTrial(billingPeriod, utcNow);
            return;
        }

        var periodStart = CurrentPeriodEnd;
        var periodEnd = BillingDateCalculator.AddBillingPeriod(periodStart, billingPeriod, BillingAnchorDay);

        CurrentPeriodStart = periodStart;
        CurrentPeriodEnd = periodEnd;
        NextBillingDate = periodEnd;
        Touch(utcNow);
    }

    public override void SoftDelete(DateTimeOffset utcNow)
    {
        if (IsDeleted)
        {
            return;
        }

        base.SoftDelete(utcNow);
    }

    private void EnsureMutable()
    {
        if (IsDeleted)
        {
            throw new DomainException("subscription_deleted", "Cannot modify a deleted subscription.");
        }

        if (Status is SubscriptionStatus.Cancelled or SubscriptionStatus.Expired)
        {
            throw new DomainException("subscription_terminal", "Subscription is already terminated.");
        }
    }
}
