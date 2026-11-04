using System.Net;
using FluentAssertions;
using Subscript.Application.Customers;
using Subscript.Application.Plans;
using Subscript.Application.Subscriptions;
using Subscript.Domain.Plans;
using Subscript.IntegrationTests.Infrastructure;

namespace Subscript.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class SubscriptionApiTests
{
    private readonly SubscriptApiFactory _factory;

    public SubscriptionApiTests(SubscriptApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Customer_Cannot_Have_Two_Active_Or_Trialing_Subscriptions()
    {
        var client = _factory.CreateAuthenticatedClient();
        var customer = await CreateCustomerAsync(client);
        var plan = await CreatePlanAsync(client);

        var first = await client.PostJsonAsync("/api/v1/subscriptions", new CreateSubscriptionRequest(customer.Id, plan.Id, null));
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostJsonAsync("/api/v1/subscriptions", new CreateSubscriptionRequest(customer.Id, plan.Id, null));
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task SoftDeleted_Trialing_Subscription_Still_Blocks_New_Subscription()
    {
        var client = _factory.CreateAuthenticatedClient();
        var customer = await CreateCustomerAsync(client);
        var plan = await CreatePlanAsync(client);

        var create = await client.PostJsonAsync("/api/v1/subscriptions", new CreateSubscriptionRequest(customer.Id, plan.Id, null));
        var subscription = await create.Content.ReadJsonAsync<SubscriptionDto>();

        var delete = await client.DeleteAsync($"/api/v1/subscriptions/{subscription!.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var second = await client.PostJsonAsync("/api/v1/subscriptions", new CreateSubscriptionRequest(customer.Id, plan.Id, null));
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Manual_Cancel_Allows_New_Subscription()
    {
        var client = _factory.CreateAuthenticatedClient();
        var customer = await CreateCustomerAsync(client);
        var plan = await CreatePlanAsync(client);

        var create = await client.PostJsonAsync("/api/v1/subscriptions", new CreateSubscriptionRequest(customer.Id, plan.Id, null));
        var subscription = await create.Content.ReadJsonAsync<SubscriptionDto>();

        var cancel = await client.PostAsync($"/api/v1/subscriptions/{subscription!.Id}/cancel", null);
        cancel.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await client.PostJsonAsync("/api/v1/subscriptions", new CreateSubscriptionRequest(customer.Id, plan.Id, null));
        second.StatusCode.Should().Be(HttpStatusCode.Created);
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
