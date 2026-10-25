namespace ApexPost.Application.Models;

/// <summary>
/// Safe, structured audit information for a completed application action.
/// </summary>
public sealed record AuditRecord(
    Guid Id,
    DateTimeOffset OccurredAt,
    string Action,
    string EntityType,
    string EntityId,
    Guid ActorId,
    Guid BranchId,
    string CorrelationId,
    decimal? Amount,
    string? Details = null,
    string? Role = null,
    int? ResultStatusCode = null,
    long? DurationMilliseconds = null,
    IReadOnlyDictionary<string, string>? RouteValues = null);
