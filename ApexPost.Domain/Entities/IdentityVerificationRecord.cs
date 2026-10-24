using ApexPost.Domain.Enums;

namespace ApexPost.Domain.Entities;

public sealed record IdentityVerificationRecord
{
    public Guid Id { get; }
    public Guid BranchId { get; }
    public Guid StaffId { get; }
    public DateTimeOffset OccurredAt { get; }
    public IdentityDocumentType DocumentType { get; }
    public string MaskedDocumentIdentifier { get; }
    public decimal MoneyOrderAmount { get; }
    public IdentityVerificationStatus Status { get; }
    public string CorrelationId { get; }

    public IdentityVerificationRecord(
        Guid id,
        Guid branchId,
        Guid staffId,
        DateTimeOffset occurredAt,
        IdentityDocumentType documentType,
        string maskedDocumentIdentifier,
        decimal moneyOrderAmount,
        IdentityVerificationStatus status,
        string correlationId)
    {
        if (id == Guid.Empty || branchId == Guid.Empty || staffId == Guid.Empty)
        {
            throw new ArgumentException("Verification, branch, and staff identifiers are required.");
        }

        if (!Enum.IsDefined(documentType) || status is not (IdentityVerificationStatus.Verified or IdentityVerificationStatus.Failed))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(maskedDocumentIdentifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        if (moneyOrderAmount <= 0 || decimal.Round(moneyOrderAmount, 2) != moneyOrderAmount)
        {
            throw new ArgumentOutOfRangeException(nameof(moneyOrderAmount));
        }

        Id = id;
        BranchId = branchId;
        StaffId = staffId;
        OccurredAt = occurredAt;
        DocumentType = documentType;
        MaskedDocumentIdentifier = maskedDocumentIdentifier;
        MoneyOrderAmount = moneyOrderAmount;
        Status = status;
        CorrelationId = correlationId;
    }
}
