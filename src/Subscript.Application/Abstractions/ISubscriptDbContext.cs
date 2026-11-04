using Microsoft.EntityFrameworkCore;
using Subscript.Domain.Customers;
using Subscript.Domain.Invoices;
using Subscript.Domain.Payments;
using Subscript.Domain.Plans;
using Subscript.Domain.Subscriptions;

namespace Subscript.Application.Abstractions;

public interface ISubscriptDbContext
{
    DbSet<Customer> Customers { get; }
    DbSet<Plan> Plans { get; }
    DbSet<Subscription> Subscriptions { get; }
    DbSet<Invoice> Invoices { get; }
    DbSet<Payment> Payments { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    void Detach(object entity);
}
