namespace ApexPost.Application.Events;

public sealed record MoneyOrderRefundedEvent(
    Guid MoneyOrderId,
    string MoneyOrderNumber,
    Guid BranchId,
    Guid ManagerId,
    DateTimeOffset OccurredAt,
    decimal TotalAmount,
    string Reason,
    string CorrelationId,
    bool PartOfCounterTransaction = false);
