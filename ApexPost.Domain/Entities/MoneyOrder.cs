using ApexPost.Domain.Enums;

namespace ApexPost.Domain.Entities;

public sealed record MoneyOrder
{
    public Guid Id { get; }
    public string MoneyOrderNumber { get; }
    public Guid BranchId { get; }
    public Guid StaffId { get; }
    public DateTimeOffset OccurredAt { get; }
    public string SenderName { get; }
    public string RecipientName { get; }
    public decimal Amount { get; }
    public decimal Fee { get; }
    public decimal TotalAmount { get; }
    public IdentityVerificationStatus VerificationStatus { get; }
    public Guid? VerificationRecordId { get; }
    public IdentityDocumentType? DocumentType { get; }
    public string? MaskedDocumentIdentifier { get; }

    public MoneyOrder(
        Guid id,
        string moneyOrderNumber,
        Guid branchId,
        Guid staffId,
        DateTimeOffset occurredAt,
        string senderName,
        string recipientName,
        decimal amount,
        decimal fee,
        IdentityVerificationStatus verificationStatus,
        Guid? verificationRecordId,
        IdentityDocumentType? documentType,
        string? maskedDocumentIdentifier)
    {
        if (id == Guid.Empty || branchId == Guid.Empty || staffId == Guid.Empty)
        {
            throw new ArgumentException("Money order, branch, and staff identifiers are required.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(moneyOrderNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(senderName);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientName);
        ValidateAmount(amount, nameof(amount), allowZero: false);
        ValidateAmount(fee, nameof(fee), allowZero: true);
        if (verificationStatus is not (IdentityVerificationStatus.NotRequired or IdentityVerificationStatus.Verified))
        {
            throw new ArgumentOutOfRangeException(nameof(verificationStatus));
        }

        if (documentType is not null && !Enum.IsDefined(documentType.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(documentType));
        }

        if (senderName.Trim().Length > 100 || recipientName.Trim().Length > 100)
        {
            throw new ArgumentException("Sender and recipient names cannot exceed 100 characters.");
        }

        if (verificationStatus == IdentityVerificationStatus.Verified &&
            (verificationRecordId is null || documentType is null || string.IsNullOrWhiteSpace(maskedDocumentIdentifier)))
        {
            throw new ArgumentException("Verified money orders must link to their identity verification record.");
        }

        if (verificationStatus == IdentityVerificationStatus.NotRequired &&
            (verificationRecordId is not null || documentType is not null || maskedDocumentIdentifier is not null))
        {
            throw new ArgumentException("Identity details are only recorded when verification is required.");
        }

        Id = id;
        MoneyOrderNumber = moneyOrderNumber.Trim();
        BranchId = branchId;
        StaffId = staffId;
        OccurredAt = occurredAt;
        SenderName = senderName.Trim();
        RecipientName = recipientName.Trim();
        Amount = amount;
        Fee = fee;
        TotalAmount = decimal.Round(amount + fee, 2, MidpointRounding.AwayFromZero);
        VerificationStatus = verificationStatus;
        VerificationRecordId = verificationRecordId;
        DocumentType = documentType;
        MaskedDocumentIdentifier = maskedDocumentIdentifier;
    }

    private static void ValidateAmount(decimal value, string parameterName, bool allowZero)
    {
        if ((allowZero ? value < 0 : value <= 0) || decimal.Round(value, 2) != value)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Money order amounts must use no more than two decimal places.");
        }
    }
}
