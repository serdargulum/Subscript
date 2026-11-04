using Microsoft.EntityFrameworkCore;
using Subscript.Application.Abstractions;
using Subscript.Application.Common;
using Subscript.Domain.Common;
using Subscript.Domain.Subscriptions;

namespace Subscript.Application.Subscriptions;

public sealed class SubscriptionService(ISubscriptDbContext db, IClock clock)
{
    public async Task<SubscriptionDto> CreateAsync(CreateSubscriptionRequest request, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(
            x => x.Id == request.CustomerId && !x.IsDeleted,
            cancellationToken);
        if (customer is null)
        {
            throw new NotFoundException("customer_not_found", $"Customer '{request.CustomerId}' was not found.");
        }

        var plan = await db.Plans.FirstOrDefaultAsync(
            x => x.Id == request.PlanId && !x.IsDeleted,
            cancellationToken);
        if (plan is null)
        {
            throw new NotFoundException("plan_not_found", $"Plan '{request.PlanId}' was not found.");
        }

        var hasBlocking = await db.Subscriptions.AnyAsync(
            x => x.CustomerId == request.CustomerId &&
                 (x.Status == SubscriptionStatus.Active || x.Status == SubscriptionStatus.Trialing),
            cancellationToken);
        if (hasBlocking)
        {
            throw new ConflictException(
                "subscription_already_active",
                "Customer already has an Active or Trialing subscription.");
        }

        var startDate = request.StartDate ?? DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var subscription = Subscription.StartTrial(
            request.CustomerId,
            request.PlanId,
            plan.BillingPeriod,
            startDate,
            clock.UtcNow);

        db.Subscriptions.Add(subscription);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictException(
                "subscription_already_active",
                "Customer already has an Active or Trialing subscription.");
        }

        return ToDto(subscription);
    }

    public async Task<SubscriptionDto> GetAsync(Guid id, bool includeDeleted, CancellationToken cancellationToken)
    {
        var subscription = await FindAsync(id, includeDeleted, cancellationToken);
        return ToDto(subscription);
    }

    public async Task<PagedResult<SubscriptionDto>> ListAsync(
        int page,
        int pageSize,
        SubscriptionStatus? status,
        Guid? customerId,
        bool includeDeleted,
        CancellationToken cancellationToken)
    {
        var (normalizedPage, normalizedSize) = Paging.Normalize(page, pageSize);

        var query = db.Subscriptions.AsNoTracking().AsQueryable();
        if (!includeDeleted)
        {
            query = query.Where(x => !x.IsDeleted);
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        if (customerId.HasValue)
        {
            query = query.Where(x => x.CustomerId == customerId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((normalizedPage - 1) * normalizedSize)
            .Take(normalizedSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<SubscriptionDto>(
            items.Select(ToDto).ToList(),
            normalizedPage,
            normalizedSize,
            totalCount);
    }

    public async Task<PagedResult<SubscriptionDto>> ListByCustomerAsync(
        Guid customerId,
        int page,
        int pageSize,
        bool includeDeleted,
        CancellationToken cancellationToken)
    {
        var (normalizedPage, normalizedSize) = Paging.Normalize(page, pageSize);

        var query = db.Subscriptions.AsNoTracking().Where(x => x.CustomerId == customerId);
        if (!includeDeleted)
        {
            query = query.Where(x => !x.IsDeleted);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((normalizedPage - 1) * normalizedSize)
            .Take(normalizedSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<SubscriptionDto>(
            items.Select(ToDto).ToList(),
            normalizedPage,
            normalizedSize,
            totalCount);
    }

    public async Task<SubscriptionDto> CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        var subscription = await FindAsync(id, includeDeleted: false, cancellationToken);
        subscription.Cancel(clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(subscription);
    }

    public async Task<SubscriptionDto> ChangePlanAsync(Guid id, ChangePlanRequest request, CancellationToken cancellationToken)
    {
        var subscription = await FindAsync(id, includeDeleted: false, cancellationToken);

        var plan = await db.Plans.FirstOrDefaultAsync(
            x => x.Id == request.PlanId && !x.IsDeleted,
            cancellationToken);
        if (plan is null)
        {
            throw new NotFoundException("plan_not_found", $"Plan '{request.PlanId}' was not found.");
        }

        subscription.SchedulePlanChange(request.PlanId, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(subscription);
    }

    public async Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var subscription = await FindAsync(id, includeDeleted: false, cancellationToken);
        subscription.SoftDelete(clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Subscription> FindAsync(Guid id, bool includeDeleted, CancellationToken cancellationToken)
    {
        var query = db.Subscriptions.AsQueryable();
        if (!includeDeleted)
        {
            query = query.Where(x => !x.IsDeleted);
        }

        var subscription = await query.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (subscription is null)
        {
            throw new NotFoundException("subscription_not_found", $"Subscription '{id}' was not found.");
        }

        return subscription;
    }

    private static SubscriptionDto ToDto(Subscription subscription) =>
        new(
            subscription.Id,
            subscription.CustomerId,
            subscription.PlanId,
            subscription.Status,
            subscription.StartDate,
            subscription.TrialEndDate,
            subscription.NextBillingDate,
            subscription.CurrentPeriodStart,
            subscription.CurrentPeriodEnd,
            subscription.BillingAnchorDay,
            subscription.PendingPlanId,
            subscription.CancelledAt,
            subscription.IsDeleted,
            subscription.CreatedAt,
            subscription.UpdatedAt);
}
