using Microsoft.EntityFrameworkCore;
using Subscript.Application.Abstractions;
using Subscript.Application.Common;
using Subscript.Domain.Common;
using Subscript.Domain.Invoices;

namespace Subscript.Application.Invoices;

public sealed class InvoiceService(ISubscriptDbContext db, IClock clock)
{
    public async Task<InvoiceDto> GetAsync(Guid id, bool includeDeleted, CancellationToken cancellationToken)
    {
        var invoice = await FindAsync(id, includeDeleted, cancellationToken);
        return ToDto(invoice);
    }

    public async Task<PagedResult<InvoiceDto>> ListAsync(
        int page,
        int pageSize,
        InvoiceStatus? status,
        Guid? subscriptionId,
        bool includeDeleted,
        CancellationToken cancellationToken)
    {
        var (normalizedPage, normalizedSize) = Paging.Normalize(page, pageSize);

        var query = db.Invoices.AsNoTracking().AsQueryable();
        if (!includeDeleted)
        {
            query = query.Where(x => !x.IsDeleted);
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        if (subscriptionId.HasValue)
        {
            query = query.Where(x => x.SubscriptionId == subscriptionId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.InvoiceDate)
            .ThenByDescending(x => x.CreatedAt)
            .Skip((normalizedPage - 1) * normalizedSize)
            .Take(normalizedSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<InvoiceDto>(
            items.Select(ToDto).ToList(),
            normalizedPage,
            normalizedSize,
            totalCount);
    }

    public async Task<PagedResult<InvoiceDto>> ListBySubscriptionAsync(
        Guid subscriptionId,
        int page,
        int pageSize,
        bool includeDeleted,
        CancellationToken cancellationToken)
    {
        var (normalizedPage, normalizedSize) = Paging.Normalize(page, pageSize);

        var query = db.Invoices.AsNoTracking().Where(x => x.SubscriptionId == subscriptionId);
        if (!includeDeleted)
        {
            query = query.Where(x => !x.IsDeleted);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.InvoiceDate)
            .Skip((normalizedPage - 1) * normalizedSize)
            .Take(normalizedSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<InvoiceDto>(
            items.Select(ToDto).ToList(),
            normalizedPage,
            normalizedSize,
            totalCount);
    }

    public async Task<InvoiceDto> CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        var invoice = await FindAsync(id, includeDeleted: false, cancellationToken);
        invoice.Cancel(clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(invoice);
    }

    private async Task<Invoice> FindAsync(Guid id, bool includeDeleted, CancellationToken cancellationToken)
    {
        var query = db.Invoices.AsQueryable();
        if (!includeDeleted)
        {
            query = query.Where(x => !x.IsDeleted);
        }

        var invoice = await query.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (invoice is null)
        {
            throw new NotFoundException("invoice_not_found", $"Invoice '{id}' was not found.");
        }

        return invoice;
    }

    internal static InvoiceDto ToDto(Invoice invoice) =>
        new(
            invoice.Id,
            invoice.SubscriptionId,
            invoice.InvoiceNumber,
            invoice.InvoiceDate,
            invoice.DueDate,
            invoice.PeriodStart,
            invoice.PeriodEnd,
            invoice.Amount,
            invoice.Currency,
            invoice.PlanNameSnapshot,
            invoice.BillingPeriodSnapshot,
            invoice.Status,
            invoice.IsDeleted,
            invoice.CreatedAt,
            invoice.UpdatedAt);
}
