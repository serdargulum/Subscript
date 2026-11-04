using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Subscript.Domain.Customers;
using Subscript.Domain.Plans;
using Subscript.Domain.Subscriptions;

namespace Subscript.Infrastructure.Persistence.Configurations;

public sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("subscriptions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(x => new { x.NextBillingDate, x.Status });
        builder.HasIndex(x => x.CustomerId);

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Plan>()
            .WithMany()
            .HasForeignKey(x => x.PlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Plan>()
            .WithMany()
            .HasForeignKey(x => x.PendingPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        // One Active/Trialing subscription per customer, including soft-deleted rows.
        builder.HasIndex(x => x.CustomerId)
            .IsUnique()
            .HasFilter("\"Status\" IN ('Active', 'Trialing')")
            .HasDatabaseName("ix_subscriptions_one_active_or_trialing_per_customer");
    }
}
