using Microsoft.EntityFrameworkCore;
using Subscript.Application.Billing;
using Subscript.Application.Customers;
using Subscript.Application.Invoices;
using Subscript.Application.Payments;
using Subscript.Application.Plans;
using Subscript.Application.Subscriptions;
using Subscript.Domain.Common;
using Subscript.Domain.Plans;
using Subscript.Infrastructure.Persistence;

namespace Subscript.Application.UnitTests;

internal sealed class TestClock(DateTimeOffset utcNow) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;
}

internal sealed class TestHarness : IDisposable
{
    public static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    public SubscriptDbContext Db { get; }
    public TestClock Clock { get; } = new(Now);

    public CustomerService Customers { get; }
    public PlanService Plans { get; }
    public SubscriptionService Subscriptions { get; }
    public InvoiceService Invoices { get; }
    public PaymentService Payments { get; }
    public BillingService Billing { get; }

    public TestHarness()
    {
        var options = new DbContextOptionsBuilder<SubscriptDbContext>()
            .UseInMemoryDatabase($"subscript-tests-{Guid.NewGuid():N}")
            .Options;

        Db = new SubscriptDbContext(options);
        Customers = new CustomerService(Db, Clock);
        Plans = new PlanService(Db, Clock);
        Subscriptions = new SubscriptionService(Db, Clock);
        Invoices = new InvoiceService(Db, Clock);
        Payments = new PaymentService(Db, Clock);
        Billing = new BillingService(Db, Clock);
    }

    public Task<CustomerDto> CreateCustomerAsync(string? email = null) =>
        Customers.CreateAsync(
            new CreateCustomerRequest(
                "Acme",
                email ?? $"{Guid.NewGuid():N}@example.com",
                "Acme Inc"),
            CancellationToken.None);

    public Task<PlanDto> CreatePlanAsync(
        decimal price = 29.99m,
        BillingPeriod billingPeriod = BillingPeriod.Monthly,
        string name = "Pro")
        => Plans.CreateAsync(
            new CreatePlanRequest(name, "Test plan", price, billingPeriod),
            CancellationToken.None);

    public async Task<(CustomerDto Customer, PlanDto Plan, SubscriptionDto Subscription)> CreateDueSubscriptionAsync(
        decimal price = 29.99m,
        BillingPeriod billingPeriod = BillingPeriod.Monthly)
    {
        var customer = await CreateCustomerAsync();
        var plan = await CreatePlanAsync(price, billingPeriod);

        // Trial is 30 days; starting 31 days ago makes the first billing run due.
        var subscription = await Subscriptions.CreateAsync(
            new CreateSubscriptionRequest(
                customer.Id,
                plan.Id,
                DateOnly.FromDateTime(Now.UtcDateTime).AddDays(-31)),
            CancellationToken.None);

        return (customer, plan, subscription);
    }

    public void Dispose() => Db.Dispose();
}
