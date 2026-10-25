using ApexPost.Domain.Enums;

namespace ApexPost.Application.Events;

public sealed record IdentityVerificationRecordedEvent(
    Guid VerificationId,
    Guid BranchId,
    Guid StaffId,
    DateTimeOffset OccurredAt,
    IdentityDocumentType DocumentType,
    decimal MoneyOrderAmount,
    IdentityVerificationStatus Status,
    string CorrelationId);
