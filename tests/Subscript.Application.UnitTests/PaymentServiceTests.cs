using FluentAssertions;
using Subscript.Application.Common;
using Subscript.Application.Invoices;
using Subscript.Application.Payments;
using Subscript.Application.Subscriptions;
using Subscript.Domain.Common;
using Subscript.Domain.Invoices;
using Subscript.Domain.Payments;
using Subscript.Domain.Subscriptions;

namespace Subscript.Application.UnitTests;

public class PaymentServiceTests
{
    [Fact]
    public async Task SucceededPayment_MarksInvoicePaid()
    {
        using var harness = new TestHarness();
        var invoice = await CreateOpenInvoiceAsync(harness);

        var payment = await harness.Payments.RecordAsync(
            new RecordPaymentRequest(invoice.Id, PaymentMethod.Manual, PaymentStatus.Succeeded, null, null),
            CancellationToken.None);

        var updated = await harness.Invoices.GetAsync(invoice.Id, false, CancellationToken.None);

        payment.Status.Should().Be(PaymentStatus.Succeeded);
        payment.Amount.Should().Be(invoice.Amount);
        updated.Status.Should().Be(InvoiceStatus.Paid);
    }

    [Fact]
    public async Task Payment_OnPaidInvoice_IsConflict()
    {
        using var harness = new TestHarness();
        var invoice = await CreateOpenInvoiceAsync(harness);
        await harness.Payments.RecordAsync(
            new RecordPaymentRequest(invoice.Id, PaymentMethod.Manual, PaymentStatus.Succeeded, null, null),
            CancellationToken.None);

        var act = () => harness.Payments.RecordAsync(
            new RecordPaymentRequest(invoice.Id, PaymentMethod.Manual, PaymentStatus.Succeeded, null, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .Where(x => x.Code == "invoice_not_open");
    }

    [Fact]
    public async Task FailedPayment_LeavesInvoiceOpen_AndExpiresSubscription()
    {
        using var harness = new TestHarness();
        var (_, _, subscription) = await harness.CreateDueSubscriptionAsync();
        await harness.Billing.ProcessDueSubscriptionsAsync(CancellationToken.None);
        var invoice = await GetSingleInvoiceAsync(harness);

        var payment = await harness.Payments.RecordAsync(
            new RecordPaymentRequest(
                invoice.Id,
                PaymentMethod.Card,
                PaymentStatus.Failed,
                null,
                "card declined"),
            CancellationToken.None);

        var updatedInvoice = await harness.Invoices.GetAsync(invoice.Id, false, CancellationToken.None);
        var updatedSubscription = await harness.Subscriptions.GetAsync(subscription.Id, false, CancellationToken.None);

        payment.Status.Should().Be(PaymentStatus.Failed);
        payment.FailureReason.Should().Be("card declined");
        updatedInvoice.Status.Should().Be(InvoiceStatus.Open);
        updatedSubscription.Status.Should().Be(SubscriptionStatus.Expired);
    }

    [Fact]
    public async Task FailedPayment_CanBeRetried()
    {
        using var harness = new TestHarness();
        var invoice = await CreateOpenInvoiceAsync(harness);
        await harness.Payments.RecordAsync(
            new RecordPaymentRequest(invoice.Id, PaymentMethod.Card, PaymentStatus.Failed, null, "declined"),
            CancellationToken.None);

        var retry = await harness.Payments.RecordAsync(
            new RecordPaymentRequest(invoice.Id, PaymentMethod.Card, PaymentStatus.Succeeded, null, null),
            CancellationToken.None);

        var payments = await harness.Payments.ListByInvoiceAsync(invoice.Id, CancellationToken.None);

        retry.Status.Should().Be(PaymentStatus.Succeeded);
        payments.Should().HaveCount(2);
    }

    [Fact]
    public async Task CancelOpenInvoice_Succeeds_AndSecondCancelFails()
    {
        using var harness = new TestHarness();
        var invoice = await CreateOpenInvoiceAsync(harness);

        var cancelled = await harness.Invoices.CancelAsync(invoice.Id, CancellationToken.None);
        var act = () => harness.Invoices.CancelAsync(invoice.Id, CancellationToken.None);

        cancelled.Status.Should().Be(InvoiceStatus.Cancelled);
        await act.Should().ThrowAsync<DomainException>()
            .Where(x => x.Code == "invoice_not_open");
    }

    [Fact]
    public async Task CancelPaidInvoice_IsRejected()
    {
        using var harness = new TestHarness();
        var invoice = await CreateOpenInvoiceAsync(harness);
        await harness.Payments.RecordAsync(
            new RecordPaymentRequest(invoice.Id, PaymentMethod.Manual, PaymentStatus.Succeeded, null, null),
            CancellationToken.None);

        var act = () => harness.Invoices.CancelAsync(invoice.Id, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>()
            .Where(x => x.Code == "invoice_not_open");
    }

    [Fact]
    public async Task PaymentForMissingInvoice_ThrowsNotFound()
    {
        using var harness = new TestHarness();

        var act = () => harness.Payments.RecordAsync(
            new RecordPaymentRequest(Guid.NewGuid(), PaymentMethod.Manual, PaymentStatus.Succeeded, null, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    private static async Task<InvoiceDto> CreateOpenInvoiceAsync(TestHarness harness)
    {
        await harness.CreateDueSubscriptionAsync();
        await harness.Billing.ProcessDueSubscriptionsAsync(CancellationToken.None);
        return await GetSingleInvoiceAsync(harness);
    }

    private static async Task<InvoiceDto> GetSingleInvoiceAsync(TestHarness harness)
    {
        var invoices = await harness.Invoices.ListAsync(1, 10, null, null, false, CancellationToken.None);
        return invoices.Items.Single();
    }
}
