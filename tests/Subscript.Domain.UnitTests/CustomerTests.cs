using FluentAssertions;
using Subscript.Domain.Common;
using Subscript.Domain.Customers;

namespace Subscript.Domain.UnitTests;

public class CustomerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_NormalizesEmailToLowerInvariant()
    {
        var customer = Customer.Create("Acme User", "User@Example.COM", "Acme", Now);

        customer.Email.Should().Be("user@example.com");
        customer.Name.Should().Be("Acme User");
        customer.CompanyName.Should().Be("Acme");
        customer.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void SoftDelete_SetsFlags()
    {
        var customer = Customer.Create("Acme User", "user@example.com", null, Now);

        customer.SoftDelete(Now.AddMinutes(1));

        customer.IsDeleted.Should().BeTrue();
        customer.DeletedAt.Should().Be(Now.AddMinutes(1));
    }

    [Fact]
    public void Update_WhenDeleted_Throws()
    {
        var customer = Customer.Create("Acme User", "user@example.com", null, Now);
        customer.SoftDelete(Now);

        var act = () => customer.UpdateDetails("New", "new@example.com", null, Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("customer_deleted");
    }
}
