using Subscript.Domain.Subscriptions;

namespace Subscript.Application.Subscriptions;

public sealed record SubscriptionDto(
    Guid Id,
    Guid CustomerId,
    Guid PlanId,
    SubscriptionStatus Status,
    DateOnly StartDate,
    DateOnly? TrialEndDate,
    DateOnly NextBillingDate,
    DateOnly CurrentPeriodStart,
    DateOnly CurrentPeriodEnd,
    int BillingAnchorDay,
    Guid? PendingPlanId,
    DateTimeOffset? CancelledAt,
    bool IsDeleted,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateSubscriptionRequest(Guid CustomerId, Guid PlanId, DateOnly? StartDate);

public sealed record ChangePlanRequest(Guid PlanId);
