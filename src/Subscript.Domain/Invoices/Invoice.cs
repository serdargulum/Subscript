using Subscript.Domain.Common;
using Subscript.Domain.Plans;

namespace Subscript.Domain.Invoices;

public sealed class Invoice : SoftDeletableEntity
{
    public Guid SubscriptionId { get; private set; }
    public string? InvoiceNumber { get; private set; }
    public DateOnly InvoiceDate { get; private set; }
    public DateOnly DueDate { get; private set; }
    public DateOnly PeriodStart { get; private set; }
    public DateOnly PeriodEnd { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = Plan.DefaultCurrency;
    public string PlanNameSnapshot { get; private set; } = string.Empty;
    public BillingPeriod BillingPeriodSnapshot { get; private set; }
    public InvoiceStatus Status { get; private set; }

    private Invoice()
    {
    }

    public static Invoice CreateForPeriod(
        Guid subscriptionId,
        DateOnly invoiceDate,
        DateOnly dueDate,
        DateOnly periodStart,
        DateOnly periodEnd,
        decimal amount,
        string currency,
        string planNameSnapshot,
        BillingPeriod billingPeriodSnapshot,
        DateTimeOffset utcNow)
    {
        if (subscriptionId == Guid.Empty)
        {
            throw new DomainException("invoice_subscription_required", "Subscription is required.");
        }

        if (periodEnd <= periodStart)
        {
            throw new DomainException("invoice_period_invalid", "Invoice period end must be after period start.");
        }

        if (amount < 0)
        {
            throw new DomainException("invoice_amount_invalid", "Invoice amount cannot be negative.");
        }

        var invoice = new Invoice
        {
            SubscriptionId = subscriptionId,
            InvoiceNumber = null,
            InvoiceDate = invoiceDate,
            DueDate = dueDate,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero),
            Currency = string.IsNullOrWhiteSpace(currency) ? Plan.DefaultCurrency : currency.Trim().ToUpperInvariant(),
            PlanNameSnapshot = string.IsNullOrWhiteSpace(planNameSnapshot)
                ? throw new DomainException("invoice_plan_name_required", "Plan name snapshot is required.")
                : planNameSnapshot.Trim(),
            BillingPeriodSnapshot = billingPeriodSnapshot,
            Status = InvoiceStatus.Open
        };

        invoice.SetCreated(utcNow);
        return invoice;
    }

    public void MarkPaid(DateTimeOffset utcNow)
    {
        EnsureOpen();
        Status = InvoiceStatus.Paid;
        Touch(utcNow);
    }

    public void Cancel(DateTimeOffset utcNow)
    {
        EnsureOpen();
        Status = InvoiceStatus.Cancelled;
        Touch(utcNow);
    }

    private void EnsureOpen()
    {
        if (IsDeleted)
        {
            throw new DomainException("invoice_deleted", "Cannot modify a deleted invoice.");
        }

        if (Status != InvoiceStatus.Open)
        {
            throw new DomainException("invoice_not_open", "Only open invoices can be modified.");
        }
    }
}
