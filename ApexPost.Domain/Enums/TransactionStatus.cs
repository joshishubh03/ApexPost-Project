namespace ApexPost.Domain.Enums;

public enum TransactionStatus
{
    Open,
    Paid,
    // Kept for records created by earlier versions of the demo data.
    Completed,
    Voided,
    Refunded
}
