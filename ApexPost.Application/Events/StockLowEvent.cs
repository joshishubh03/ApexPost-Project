namespace ApexPost.Application.Events;

public sealed record StockLowEvent(
    Guid BranchId,
    Guid ProductId,
    string ProductName,
    int QuantityOnHand,
    int ReorderLevel,
    Guid ActorId,
    DateTimeOffset OccurredAt,
    string CorrelationId);
