using Subscript.Domain.Invoices;
using Subscript.Domain.Plans;

namespace Subscript.Application.Invoices;

public sealed record InvoiceDto(
    Guid Id,
    Guid SubscriptionId,
    string? InvoiceNumber,
    DateOnly InvoiceDate,
    DateOnly DueDate,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal Amount,
    string Currency,
    string PlanNameSnapshot,
    BillingPeriod BillingPeriodSnapshot,
    InvoiceStatus Status,
    bool IsDeleted,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
