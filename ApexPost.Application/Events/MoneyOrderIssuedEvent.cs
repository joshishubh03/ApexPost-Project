namespace ApexPost.Application.Events;

public sealed record MoneyOrderIssuedEvent(
    Guid MoneyOrderId,
    string MoneyOrderNumber,
    Guid BranchId,
    Guid StaffId,
    DateTimeOffset OccurredAt,
    decimal TotalAmount,
    string CorrelationId);
