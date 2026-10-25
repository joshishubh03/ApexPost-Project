namespace ApexPost.Application.Events;

public sealed record HighValueMoneyOrderEvent(
    Guid MoneyOrderId,
    string MoneyOrderNumber,
    Guid BranchId,
    Guid ActorId,
    DateTimeOffset OccurredAt,
    decimal TotalAmount,
    string CorrelationId);
