namespace ApexPost.Application.Events;

public sealed record CounterTransactionReversedEvent(
    Guid TransactionId,
    string TransactionNumber,
    Guid BranchId,
    Guid StaffId,
    DateTimeOffset OccurredAt,
    decimal Amount,
    string Status,
    string CorrelationId);
