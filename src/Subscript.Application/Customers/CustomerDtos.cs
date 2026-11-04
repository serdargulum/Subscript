namespace Subscript.Application.Customers;

public sealed record CustomerDto(
    Guid Id,
    string Name,
    string Email,
    string? CompanyName,
    bool IsDeleted,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateCustomerRequest(string Name, string Email, string? CompanyName);

public sealed record UpdateCustomerRequest(string Name, string Email, string? CompanyName);
