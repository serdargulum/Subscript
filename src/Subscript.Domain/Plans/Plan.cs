using Subscript.Domain.Common;

namespace Subscript.Domain.Plans;

public sealed class Plan : SoftDeletableEntity
{
    public const string DefaultCurrency = "USD";

    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public decimal Price { get; private set; }
    public string Currency { get; private set; } = DefaultCurrency;
    public BillingPeriod BillingPeriod { get; private set; }

    private Plan()
    {
    }

    public static Plan Create(
        string name,
        string? description,
        decimal price,
        BillingPeriod billingPeriod,
        DateTimeOffset utcNow)
    {
        var plan = new Plan();
        plan.SetCreated(utcNow);
        plan.Update(name, description, price, billingPeriod, utcNow);
        return plan;
    }

    public void Update(
        string name,
        string? description,
        decimal price,
        BillingPeriod billingPeriod,
        DateTimeOffset utcNow)
    {
        if (IsDeleted)
        {
            throw new DomainException("plan_deleted", "Cannot update a deleted plan.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("plan_name_required", "Plan name is required.");
        }

        if (price < 0)
        {
            throw new DomainException("plan_price_invalid", "Plan price cannot be negative.");
        }

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Price = decimal.Round(price, 2, MidpointRounding.AwayFromZero);
        Currency = DefaultCurrency;
        BillingPeriod = billingPeriod;
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
}
