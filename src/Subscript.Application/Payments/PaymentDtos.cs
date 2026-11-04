using Subscript.Domain.Payments;

namespace Subscript.Application.Payments;

public sealed record PaymentDto(
    Guid Id,
    Guid InvoiceId,
    decimal Amount,
    string Currency,
    PaymentMethod Method,
    PaymentStatus Status,
    DateTimeOffset PaidAt,
    string? FailureReason,
    DateTimeOffset CreatedAt);

public sealed record RecordPaymentRequest(
    Guid InvoiceId,
    PaymentMethod Method,
    PaymentStatus Status,
    DateTimeOffset? PaidAt,
    string? FailureReason);
