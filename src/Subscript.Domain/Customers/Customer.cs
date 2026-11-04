using Subscript.Domain.Common;

namespace Subscript.Domain.Customers;

public sealed class Customer : SoftDeletableEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string? CompanyName { get; private set; }

    private Customer()
    {
    }

    public static Customer Create(
        string name,
        string email,
        string? companyName,
        DateTimeOffset utcNow)
    {
        var customer = new Customer();
        customer.SetCreated(utcNow);
        customer.UpdateDetails(name, email, companyName, utcNow);
        return customer;
    }

    public void UpdateDetails(string name, string email, string? companyName, DateTimeOffset utcNow)
    {
        if (IsDeleted)
        {
            throw new DomainException("customer_deleted", "Cannot update a deleted customer.");
        }

        Name = NormalizeRequired(name, "name");
        Email = NormalizeEmail(email);
        CompanyName = string.IsNullOrWhiteSpace(companyName) ? null : companyName.Trim();
        Touch(utcNow);
    }

    public override void SoftDelete(DateTimeOffset utcNow)
    {
        if (IsDeleted)
        {
            return;
        }

        base.SoftDelete(utcNow);
    }

    public static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new DomainException("customer_email_required", "Customer email is required.");
        }

        return email.Trim().ToLowerInvariant();
    }

    private static string NormalizeRequired(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"customer_{field}_required", $"Customer {field} is required.");
        }

        return value.Trim();
    }
}
