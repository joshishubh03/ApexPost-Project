namespace ApexPost.Application.Models;

public sealed record OperationalNotification(
    Guid Id,
    string Type,
    string Message,
    Guid BranchId,
    Guid ActorId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string? ReferenceId);
