using Subscript.Domain.Plans;

namespace Subscript.Application.Plans;

public sealed record PlanDto(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    string Currency,
    BillingPeriod BillingPeriod,
    bool IsDeleted,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreatePlanRequest(
    string Name,
    string? Description,
    decimal Price,
    BillingPeriod BillingPeriod);

public sealed record UpdatePlanRequest(
    string Name,
    string? Description,
    decimal Price,
    BillingPeriod BillingPeriod);
