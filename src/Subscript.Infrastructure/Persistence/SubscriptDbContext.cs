using Microsoft.EntityFrameworkCore;
using Subscript.Application.Abstractions;
using Subscript.Domain.Customers;
using Subscript.Domain.Invoices;
using Subscript.Domain.Payments;
using Subscript.Domain.Plans;
using Subscript.Domain.Subscriptions;

namespace Subscript.Infrastructure.Persistence;

public sealed class SubscriptDbContext(DbContextOptions<SubscriptDbContext> options)
    : DbContext(options), ISubscriptDbContext
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public void Detach(object entity) => Entry(entity).State = EntityState.Detached;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SubscriptDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
