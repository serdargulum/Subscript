using Microsoft.EntityFrameworkCore;
using Subscript.Application.Abstractions;
using Subscript.Application.Common;
using Subscript.Domain.Common;
using Subscript.Domain.Plans;

namespace Subscript.Application.Plans;

public sealed class PlanService(ISubscriptDbContext db, IClock clock)
{
    public async Task<PlanDto> CreateAsync(CreatePlanRequest request, CancellationToken cancellationToken)
    {
        var plan = Plan.Create(request.Name, request.Description, request.Price, request.BillingPeriod, clock.UtcNow);
        db.Plans.Add(plan);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(plan);
    }

    public async Task<PlanDto> GetAsync(Guid id, bool includeDeleted, CancellationToken cancellationToken)
    {
        var plan = await FindAsync(id, includeDeleted, cancellationToken);
        return ToDto(plan);
    }

    public async Task<PagedResult<PlanDto>> ListAsync(
        int page,
        int pageSize,
        bool includeDeleted,
        CancellationToken cancellationToken)
    {
        var (normalizedPage, normalizedSize) = Paging.Normalize(page, pageSize);

        var query = db.Plans.AsNoTracking().AsQueryable();
        if (!includeDeleted)
        {
            query = query.Where(x => !x.IsDeleted);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.Name)
            .Skip((normalizedPage - 1) * normalizedSize)
            .Take(normalizedSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<PlanDto>(
            items.Select(ToDto).ToList(),
            normalizedPage,
            normalizedSize,
            totalCount);
    }

    public async Task<PlanDto> UpdateAsync(Guid id, UpdatePlanRequest request, CancellationToken cancellationToken)
    {
        var plan = await FindAsync(id, includeDeleted: false, cancellationToken);
        plan.Update(request.Name, request.Description, request.Price, request.BillingPeriod, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(plan);
    }

    public async Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var plan = await FindAsync(id, includeDeleted: false, cancellationToken);
        plan.SoftDelete(clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Plan> FindAsync(Guid id, bool includeDeleted, CancellationToken cancellationToken)
    {
        var query = db.Plans.AsQueryable();
        if (!includeDeleted)
        {
            query = query.Where(x => !x.IsDeleted);
        }

        var plan = await query.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (plan is null)
        {
            throw new NotFoundException("plan_not_found", $"Plan '{id}' was not found.");
        }

        return plan;
    }

    private static PlanDto ToDto(Plan plan) =>
        new(plan.Id, plan.Name, plan.Description, plan.Price, plan.Currency, plan.BillingPeriod, plan.IsDeleted, plan.CreatedAt, plan.UpdatedAt);
}
