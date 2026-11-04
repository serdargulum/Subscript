using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Subscript.Application.Common;
using Subscript.Application.Customers;
using Subscript.Application.Invoices;
using Subscript.Application.Payments;
using Subscript.Application.Plans;
using Subscript.Application.Subscriptions;
using Subscript.Domain.Invoices;
using Subscript.Domain.Payments;
using Subscript.Domain.Plans;
using Subscript.Domain.Subscriptions;
using Subscript.IntegrationTests.Infrastructure;

namespace Subscript.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class BillingApiTests
{
    private readonly SubscriptApiFactory _factory;

    public BillingApiTests(SubscriptApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task BillingRun_GeneratesInvoice_AndIsIdempotent()
    {
        var client = _factory.CreateAuthenticatedClient();
        var (_, _, subscription) = await CreateDueSubscriptionAsync(client);

        var firstRun = await client.PostAsync("/api/v1/billing/run", null);
        firstRun.StatusCode.Should().Be(HttpStatusCode.OK);

        var invoices = await GetInvoicesAsync(client, subscription.Id);
        invoices.Items.Should().ContainSingle();
        invoices.Items[0].Status.Should().Be(InvoiceStatus.Open);
        invoices.Items[0].Amount.Should().Be(29.99m);

        var secondRun = await client.PostAsync("/api/v1/billing/run", null);
        secondRun.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterSecondRun = await GetInvoicesAsync(client, subscription.Id);
        afterSecondRun.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task SucceededPayment_ClosesInvoice()
    {
        var client = _factory.CreateAuthenticatedClient();
        var (_, _, subscription) = await CreateDueSubscriptionAsync(client);
        await client.PostAsync("/api/v1/billing/run", null);
        var invoice = (await GetInvoicesAsync(client, subscription.Id)).Items.Single();

        var paymentResponse = await client.PostJsonAsync("/api/v1/payments", new RecordPaymentRequest(
            invoice.Id,
            PaymentMethod.Manual,
            PaymentStatus.Succeeded,
            null,
            null));

        paymentResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdPayment = await paymentResponse.Content.ReadJsonAsync<PaymentDto>();

        var updated = await client.GetAsync($"/api/v1/invoices/{invoice.Id}");
        var updatedInvoice = await updated.Content.ReadJsonAsync<InvoiceDto>();
        updatedInvoice!.Status.Should().Be(InvoiceStatus.Paid);

        var paymentsResponse = await client.GetAsync($"/api/v1/invoices/{invoice.Id}/payments");
        var payments = await paymentsResponse.Content.ReadJsonAsync<List<PaymentDto>>();
        payments.Should().ContainSingle();

        var listResponse = await client.GetAsync("/api/v1/payments?status=Succeeded&page=1&pageSize=10");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var allPayments = await listResponse.Content.ReadJsonAsync<PagedResult<PaymentDto>>();
        allPayments!.Items.Should().ContainSingle(payment => payment.Id == createdPayment!.Id);
    }

    [Fact]
    public async Task FailedPayment_ExpiresSubscription_AndLeavesInvoiceOpen()
    {
        var client = _factory.CreateAuthenticatedClient();
        var (_, _, subscription) = await CreateDueSubscriptionAsync(client);
        await client.PostAsync("/api/v1/billing/run", null);
        var invoice = (await GetInvoicesAsync(client, subscription.Id)).Items.Single();

        var paymentResponse = await client.PostJsonAsync("/api/v1/payments", new RecordPaymentRequest(
            invoice.Id,
            PaymentMethod.Card,
            PaymentStatus.Failed,
            null,
            "declined"));

        paymentResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var invoiceResponse = await client.GetAsync($"/api/v1/invoices/{invoice.Id}");
        var updatedInvoice = await invoiceResponse.Content.ReadJsonAsync<InvoiceDto>();
        updatedInvoice!.Status.Should().Be(InvoiceStatus.Open);

        var subscriptionResponse = await client.GetAsync($"/api/v1/subscriptions/{subscription.Id}");
        var updatedSubscription = await subscriptionResponse.Content.ReadJsonAsync<SubscriptionDto>();
        updatedSubscription!.Status.Should().Be(SubscriptionStatus.Expired);
    }

    [Fact]
    public async Task CreateSubscription_AgainstDeletedPlan_ReturnsNotFound()
    {
        var client = _factory.CreateAuthenticatedClient();
        var customer = await CreateCustomerAsync(client);
        var plan = await CreatePlanAsync(client);

        var delete = await client.DeleteAsync($"/api/v1/plans/{plan.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var create = await client.PostJsonAsync("/api/v1/subscriptions", new CreateSubscriptionRequest(
            customer.Id,
            plan.Id,
            null));

        create.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CustomerList_IsPaged()
    {
        var client = _factory.CreateAuthenticatedClient();
        await CreateCustomerAsync(client);
        await CreateCustomerAsync(client);
        await CreateCustomerAsync(client);

        var response = await client.GetAsync("/api/v1/customers?page=1&pageSize=2");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await response.Content.ReadJsonAsync<PagedResult<CustomerDto>>();

        page.Should().NotBeNull();
        page!.Page.Should().Be(1);
        page.PageSize.Should().Be(2);
        page.TotalCount.Should().BeGreaterThanOrEqualTo(3);
        page.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task InvoiceList_RequiresApiKey()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/invoices");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task<(CustomerDto Customer, PlanDto Plan, SubscriptionDto Subscription)> CreateDueSubscriptionAsync(
        HttpClient client)
    {
        var customer = await CreateCustomerAsync(client);
        var plan = await CreatePlanAsync(client);

        var response = await client.PostJsonAsync("/api/v1/subscriptions", new CreateSubscriptionRequest(
            customer.Id,
            plan.Id,
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-31)));

        response.EnsureSuccessStatusCode();
        var subscription = (await response.Content.ReadJsonAsync<SubscriptionDto>())!;
        return (customer, plan, subscription);
    }

    private static async Task<PagedResult<InvoiceDto>> GetInvoicesAsync(HttpClient client, Guid subscriptionId)
    {
        var response = await client.GetAsync(
            $"/api/v1/subscriptions/{subscriptionId}/invoices?page=1&pageSize=10");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadJsonAsync<PagedResult<InvoiceDto>>())!;
    }

    private static async Task<CustomerDto> CreateCustomerAsync(HttpClient client)
    {
        var response = await client.PostJsonAsync("/api/v1/customers", new CreateCustomerRequest(
            $"Customer-{Guid.NewGuid():N}",
            $"{Guid.NewGuid():N}@example.com",
            null));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadJsonAsync<CustomerDto>())!;
    }

    private static async Task<PlanDto> CreatePlanAsync(HttpClient client)
    {
        var response = await client.PostJsonAsync("/api/v1/plans", new CreatePlanRequest(
            $"Plan-{Guid.NewGuid():N}",
            "Test plan",
            29.99m,
            BillingPeriod.Monthly));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadJsonAsync<PlanDto>())!;
    }
}
