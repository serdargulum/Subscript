using FluentAssertions;
using Subscript.Application.Common;
using Subscript.Application.Customers;

namespace Subscript.Application.UnitTests;

public class CustomerServiceTests
{
    [Fact]
    public async Task Create_NormalizesEmail()
    {
        using var harness = new TestHarness();

        var customer = await harness.CreateCustomerAsync("USER@Example.COM");

        customer.Email.Should().Be("user@example.com");
    }

    [Fact]
    public async Task Create_WithDuplicateEmail_IsCaseInsensitiveConflict()
    {
        using var harness = new TestHarness();
        await harness.CreateCustomerAsync("user@example.com");

        var act = () => harness.CreateCustomerAsync("USER@example.com");

        await act.Should().ThrowAsync<ConflictException>()
            .Where(x => x.Code == "customer_email_exists");
    }

    [Fact]
    public async Task Update_ToAnotherCustomersEmail_IsConflict()
    {
        using var harness = new TestHarness();
        await harness.CreateCustomerAsync("first@example.com");
        var second = await harness.CreateCustomerAsync("second@example.com");

        var act = () => harness.Customers.UpdateAsync(
            second.Id,
            new UpdateCustomerRequest("Second", "first@example.com", null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .Where(x => x.Code == "customer_email_exists");
    }

    [Fact]
    public async Task List_PagesAndFiltersBySearch()
    {
        using var harness = new TestHarness();
        await harness.CreateCustomerAsync("alpha@example.com");
        await harness.CreateCustomerAsync("beta@example.com");
        await harness.CreateCustomerAsync("gamma@example.com");

        var page = await harness.Customers.ListAsync(1, 2, null, false, CancellationToken.None);
        var filtered = await harness.Customers.ListAsync(1, 10, "BETA", false, CancellationToken.None);

        page.TotalCount.Should().Be(3);
        page.Items.Should().HaveCount(2);
        page.TotalPages.Should().Be(2);
        filtered.Items.Should().ContainSingle()
            .Which.Email.Should().Be("beta@example.com");
    }

    [Fact]
    public async Task SoftDeletedCustomer_IsHiddenUnlessRequested()
    {
        using var harness = new TestHarness();
        var customer = await harness.CreateCustomerAsync();

        await harness.Customers.SoftDeleteAsync(customer.Id, CancellationToken.None);

        var visible = await harness.Customers.ListAsync(1, 10, null, false, CancellationToken.None);
        var all = await harness.Customers.ListAsync(1, 10, null, true, CancellationToken.None);

        visible.Items.Should().BeEmpty();
        all.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task MissingCustomer_ThrowsNotFound()
    {
        using var harness = new TestHarness();

        var act = () => harness.Customers.GetAsync(Guid.NewGuid(), false, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
