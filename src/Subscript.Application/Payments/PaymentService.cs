using Microsoft.EntityFrameworkCore;
using Subscript.Application.Abstractions;
using Subscript.Application.Common;
using Subscript.Domain.Common;
using Subscript.Domain.Invoices;
using Subscript.Domain.Payments;
using Subscript.Domain.Subscriptions;

namespace Subscript.Application.Payments;

public sealed class PaymentService(ISubscriptDbContext db, IClock clock)
{
    public async Task<PaymentDto> RecordAsync(RecordPaymentRequest request, CancellationToken cancellationToken)
    {
        var invoice = await db.Invoices.FirstOrDefaultAsync(
            x => x.Id == request.InvoiceId && !x.IsDeleted,
            cancellationToken);
        if (invoice is null)
        {
            throw new NotFoundException("invoice_not_found", $"Invoice '{request.InvoiceId}' was not found.");
        }

        if (invoice.Status != InvoiceStatus.Open)
        {
            throw new ConflictException("invoice_not_open", "Payments can only be recorded against open invoices.");
        }

        var alreadyPaid = await db.Payments.AnyAsync(
            x => x.InvoiceId == invoice.Id && x.Status == PaymentStatus.Succeeded && !x.IsDeleted,
            cancellationToken);
        if (alreadyPaid)
        {
            throw new ConflictException("invoice_already_paid", "Invoice already has a successful payment.");
        }

        var paidAt = request.PaidAt ?? clock.UtcNow;
        Payment payment;

        if (request.Status == PaymentStatus.Succeeded)
        {
            payment = Payment.RecordSucceeded(
                invoice.Id,
                invoice.Amount,
                invoice.Currency,
                request.Method,
                paidAt,
                clock.UtcNow);

            invoice.MarkPaid(clock.UtcNow);
        }
        else
        {
            payment = Payment.RecordFailed(
                invoice.Id,
                invoice.Amount,
                invoice.Currency,
                request.Method,
                paidAt,
                request.FailureReason,
                clock.UtcNow);

            var subscription = await db.Subscriptions.FirstOrDefaultAsync(
                x => x.Id == invoice.SubscriptionId && !x.IsDeleted,
                cancellationToken);
            if (subscription is not null &&
                subscription.Status is SubscriptionStatus.Active or SubscriptionStatus.Trialing)
            {
                subscription.ExpireDueToPaymentFailure(clock.UtcNow);
            }
        }

        db.Payments.Add(payment);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(payment);
    }

    public async Task<PagedResult<PaymentDto>> ListAsync(
        int page,
        int pageSize,
        Guid? invoiceId,
        PaymentStatus? status,
        CancellationToken cancellationToken)
    {
        var (normalizedPage, normalizedSize) = Paging.Normalize(page, pageSize);

        var query = db.Payments.AsNoTracking().Where(x => !x.IsDeleted);

        if (invoiceId is { } filterInvoiceId)
        {
            query = query.Where(x => x.InvoiceId == filterInvoiceId);
        }

        if (status is { } filterStatus)
        {
            query = query.Where(x => x.Status == filterStatus);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.PaidAt)
            .ThenByDescending(x => x.Id)
            .Skip((normalizedPage - 1) * normalizedSize)
            .Take(normalizedSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<PaymentDto>(
            items.Select(ToDto).ToList(),
            normalizedPage,
            normalizedSize,
            totalCount);
    }

    public async Task<IReadOnlyList<PaymentDto>> ListByInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken)
    {
        var items = await db.Payments.AsNoTracking()
            .Where(x => x.InvoiceId == invoiceId && !x.IsDeleted)
            .OrderByDescending(x => x.PaidAt)
            .ToListAsync(cancellationToken);

        return items.Select(ToDto).ToList();
    }

    private static PaymentDto ToDto(Payment payment) =>
        new(
            payment.Id,
            payment.InvoiceId,
            payment.Amount,
            payment.Currency,
            payment.Method,
            payment.Status,
            payment.PaidAt,
            payment.FailureReason,
            payment.CreatedAt);
}
