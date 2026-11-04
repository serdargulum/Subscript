using System.Net;
using FluentAssertions;
using Subscript.Application.Customers;
using Subscript.IntegrationTests.Infrastructure;

namespace Subscript.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class CustomerApiTests
{
    private readonly SubscriptApiFactory _factory;

    public CustomerApiTests(SubscriptApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Create_And_Get_Customer_Works()
    {
        var client = _factory.CreateAuthenticatedClient();

        var createResponse = await client.PostJsonAsync("/api/v1/customers", new CreateCustomerRequest(
            "Serdar",
            "serdar@example.com",
            "Subscript"));

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadJsonAsync<CustomerDto>();
        created.Should().NotBeNull();
        created!.Email.Should().Be("serdar@example.com");

        var getResponse = await client.GetAsync($"/api/v1/customers/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Duplicate_Email_CaseInsensitive_ReturnsConflict()
    {
        var client = _factory.CreateAuthenticatedClient();
        var email = $"dup-{Guid.NewGuid():N}@example.com";

        var first = await client.PostJsonAsync("/api/v1/customers", new CreateCustomerRequest("One", email, null));
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostJsonAsync("/api/v1/customers", new CreateCustomerRequest("Two", email.ToUpperInvariant(), null));
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task SoftDeleted_Email_Cannot_Be_Reused()
    {
        var client = _factory.CreateAuthenticatedClient();
        var email = $"deleted-{Guid.NewGuid():N}@example.com";

        var create = await client.PostJsonAsync("/api/v1/customers", new CreateCustomerRequest("Temp", email, null));
        var created = await create.Content.ReadJsonAsync<CustomerDto>();

        var deactivate = await client.PostAsync($"/api/v1/customers/{created!.Id}/deactivate", null);
        deactivate.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var reuse = await client.PostJsonAsync("/api/v1/customers", new CreateCustomerRequest("Other", email, null));
        reuse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Unauthorized_Without_ApiKey()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/customers");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
