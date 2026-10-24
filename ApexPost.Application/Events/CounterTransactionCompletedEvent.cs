using ApexPost.Domain.Enums;

namespace ApexPost.Application.Events;

/// <summary>
/// Raised after a counter transaction and its stock changes have been saved.
/// </summary>
public sealed record CounterTransactionCompletedEvent(
    Guid TransactionId,
    string TransactionNumber,
    Guid BranchId,
    Guid StaffId,
    DateTimeOffset OccurredAt,
    decimal TotalAmount,
    PaymentMethod PaymentMethod,
    string CorrelationId);
