using FluentAssertions;
using Subscript.Domain.Common;
using Subscript.Domain.Invoices;
using Subscript.Domain.Payments;
using Subscript.Domain.Plans;

namespace Subscript.Domain.UnitTests;

public class InvoicePaymentTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid SubscriptionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid InvoiceId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public void CreateInvoice_LeavesInvoiceNumberNull_ForFutureGib()
    {
        var invoice = Invoice.CreateForPeriod(
            SubscriptionId,
            new DateOnly(2026, 2, 1),
            new DateOnly(2026, 2, 8),
            new DateOnly(2026, 2, 1),
            new DateOnly(2026, 3, 1),
            49.99m,
            Plan.DefaultCurrency,
            "Pro",
            BillingPeriod.Monthly,
            Now);

        invoice.InvoiceNumber.Should().BeNull();
        invoice.Status.Should().Be(InvoiceStatus.Open);
        invoice.Amount.Should().Be(49.99m);
        invoice.Currency.Should().Be("USD");
    }

    [Fact]
    public void MarkPaid_FromOpen_Succeeds()
    {
        var invoice = CreateOpenInvoice();

        invoice.MarkPaid(Now);

        invoice.Status.Should().Be(InvoiceStatus.Paid);
    }

    [Fact]
    public void Cancel_WhenPaid_Throws()
    {
        var invoice = CreateOpenInvoice();
        invoice.MarkPaid(Now);

        var act = () => invoice.Cancel(Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("invoice_not_open");
    }

    [Fact]
    public void Payment_PartialAmountNotAllowed_ByDomainGuardOnPositiveAmountOnly()
    {
        var act = () => Payment.RecordSucceeded(InvoiceId, 0m, "USD", PaymentMethod.Manual, Now, Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("payment_amount_invalid");
    }

    private static Invoice CreateOpenInvoice() =>
        Invoice.CreateForPeriod(
            SubscriptionId,
            new DateOnly(2026, 2, 1),
            new DateOnly(2026, 2, 8),
            new DateOnly(2026, 2, 1),
            new DateOnly(2026, 3, 1),
            10m,
            "USD",
            "Basic",
            BillingPeriod.Monthly,
            Now);
}
