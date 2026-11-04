using Subscript.Domain.Common;
using Subscript.Domain.Plans;

namespace Subscript.Domain.Payments;

public sealed class Payment : SoftDeletableEntity
{
    public Guid InvoiceId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = Plan.DefaultCurrency;
    public PaymentMethod Method { get; private set; }
    public PaymentStatus Status { get; private set; }
    public DateTimeOffset PaidAt { get; private set; }
    public string? FailureReason { get; private set; }

    private Payment()
    {
    }

    public static Payment RecordSucceeded(
        Guid invoiceId,
        decimal amount,
        string currency,
        PaymentMethod method,
        DateTimeOffset paidAt,
        DateTimeOffset utcNow)
    {
        var payment = CreateBase(invoiceId, amount, currency, method, paidAt, utcNow);
        payment.Status = PaymentStatus.Succeeded;
        payment.FailureReason = null;
        return payment;
    }

    public static Payment RecordFailed(
        Guid invoiceId,
        decimal amount,
        string currency,
        PaymentMethod method,
        DateTimeOffset paidAt,
        string? failureReason,
        DateTimeOffset utcNow)
    {
        var payment = CreateBase(invoiceId, amount, currency, method, paidAt, utcNow);
        payment.Status = PaymentStatus.Failed;
        payment.FailureReason = string.IsNullOrWhiteSpace(failureReason) ? null : failureReason.Trim();
        return payment;
    }

    private static Payment CreateBase(
        Guid invoiceId,
        decimal amount,
        string currency,
        PaymentMethod method,
        DateTimeOffset paidAt,
        DateTimeOffset utcNow)
    {
        if (invoiceId == Guid.Empty)
        {
            throw new DomainException("payment_invoice_required", "Invoice is required.");
        }

        if (amount <= 0)
        {
            throw new DomainException("payment_amount_invalid", "Payment amount must be greater than zero.");
        }

        var payment = new Payment
        {
            InvoiceId = invoiceId,
            Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero),
            Currency = string.IsNullOrWhiteSpace(currency) ? Plan.DefaultCurrency : currency.Trim().ToUpperInvariant(),
            Method = method,
            PaidAt = paidAt
        };

        payment.SetCreated(utcNow);
        return payment;
    }
}
