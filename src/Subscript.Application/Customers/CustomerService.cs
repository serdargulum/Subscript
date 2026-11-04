using Microsoft.EntityFrameworkCore;
using Subscript.Application.Abstractions;
using Subscript.Application.Common;
using Subscript.Domain.Common;
using Subscript.Domain.Customers;

namespace Subscript.Application.Customers;

public sealed class CustomerService(ISubscriptDbContext db, IClock clock)
{
    public async Task<CustomerDto> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        var email = Customer.NormalizeEmail(request.Email);
        var exists = await db.Customers.AnyAsync(x => x.Email == email, cancellationToken);
        if (exists)
        {
            throw new ConflictException("customer_email_exists", "A customer with this email already exists.");
        }

        var customer = Customer.Create(request.Name, email, request.CompanyName, clock.UtcNow);
        db.Customers.Add(customer);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(customer);
    }

    public async Task<CustomerDto> GetAsync(Guid id, bool includeDeleted, CancellationToken cancellationToken)
    {
        var customer = await FindAsync(id, includeDeleted, cancellationToken);
        return ToDto(customer);
    }

    public async Task<PagedResult<CustomerDto>> ListAsync(
        int page,
        int pageSize,
        string? search,
        bool includeDeleted,
        CancellationToken cancellationToken)
    {
        var (normalizedPage, normalizedSize) = Paging.Normalize(page, pageSize);

        var query = db.Customers.AsNoTracking().AsQueryable();
        if (!includeDeleted)
        {
            query = query.Where(x => !x.IsDeleted);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.Name.ToLower().Contains(term) ||
                x.Email.Contains(term) ||
                (x.CompanyName != null && x.CompanyName.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.Name)
            .Skip((normalizedPage - 1) * normalizedSize)
            .Take(normalizedSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<CustomerDto>(
            items.Select(ToDto).ToList(),
            normalizedPage,
            normalizedSize,
            totalCount);
    }

    public async Task<CustomerDto> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken cancellationToken)
    {
        var customer = await FindAsync(id, includeDeleted: false, cancellationToken);
        var email = Customer.NormalizeEmail(request.Email);

        var emailTaken = await db.Customers.AnyAsync(
            x => x.Email == email && x.Id != id,
            cancellationToken);
        if (emailTaken)
        {
            throw new ConflictException("customer_email_exists", "A customer with this email already exists.");
        }

        customer.UpdateDetails(request.Name, email, request.CompanyName, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(customer);
    }

    public async Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var customer = await FindAsync(id, includeDeleted: false, cancellationToken);
        customer.SoftDelete(clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Customer> FindAsync(Guid id, bool includeDeleted, CancellationToken cancellationToken)
    {
        var query = db.Customers.AsQueryable();
        if (!includeDeleted)
        {
            query = query.Where(x => !x.IsDeleted);
        }

        var customer = await query.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (customer is null)
        {
            throw new NotFoundException("customer_not_found", $"Customer '{id}' was not found.");
        }

        return customer;
    }

    private static CustomerDto ToDto(Customer customer) =>
        new(customer.Id, customer.Name, customer.Email, customer.CompanyName, customer.IsDeleted, customer.CreatedAt, customer.UpdatedAt);
}
