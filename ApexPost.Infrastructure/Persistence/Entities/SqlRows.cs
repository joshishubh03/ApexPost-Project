using ApexPost.Domain.Enums;

namespace ApexPost.Infrastructure.Persistence.Entities;

/// <summary>Audit values are owned by each row and flattened into that table by EF Core.</summary>
public sealed class AuditColumns
{
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedByStaffId { get; set; }
    public Guid? UpdatedByStaffId { get; set; }
    public string? CorrelationId { get; set; }
}

public interface IAuditedRow
{
    AuditColumns Audit { get; set; }
}

public sealed class BranchRow : IAuditedRow
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public bool IsOpen { get; set; }
    public TimeOnly OpeningTime { get; set; }
    public TimeOnly ClosingTime { get; set; }
    public AuditColumns Audit { get; set; } = new();
}

public sealed class CounterTerminalRow : IAuditedRow
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public AuditColumns Audit { get; set; } = new();
}

public sealed class StaffRow : IAuditedRow
{
    public Guid Id { get; set; }
    public Guid? BranchId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public AuditColumns Audit { get; set; } = new();
}

public sealed class StaffRoleRow : IAuditedRow
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AuditColumns Audit { get; set; } = new();
}

public sealed class ProductCategoryRow : IAuditedRow
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AuditColumns Audit { get; set; } = new();
}

public sealed class TaxCategoryRow : IAuditedRow
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AuditColumns Audit { get; set; } = new();
}

public sealed class ProductRow : IAuditedRow
{
    public Guid Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string TaxCategory { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsActive { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public AuditColumns Audit { get; set; } = new();
}

public sealed class BranchStockRow : IAuditedRow
{
    public Guid BranchId { get; set; }
    public Guid ProductId { get; set; }
    public int QuantityOnHand { get; set; }
    public int ReorderLevel { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public AuditColumns Audit { get; set; } = new();
}

public sealed class StockAdjustmentRow : IAuditedRow
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public Guid ProductId { get; set; }
    public Guid StaffId { get; set; }
    public int QuantityChange { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
    public AuditColumns Audit { get; set; } = new();
}

public sealed class ShippingZoneRow : IAuditedRow
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public AuditColumns Audit { get; set; } = new();
}

public sealed class ShippingRateRow : IAuditedRow
{
    public Guid Id { get; set; }
    public Guid ZoneId { get; set; }
    public ShippingServiceLevel ServiceLevel { get; set; }
    public decimal BasePrice { get; set; }
    public decimal PricePerKilogram { get; set; }
    public decimal MaximumWeightKilograms { get; set; }
    public bool IsActive { get; set; }
    public AuditColumns Audit { get; set; } = new();
}

public sealed class IdentityVerificationRow : IAuditedRow
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public Guid StaffId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public IdentityDocumentType DocumentType { get; set; }
    public string MaskedDocumentIdentifier { get; set; } = string.Empty;
    public decimal MoneyOrderAmount { get; set; }
    public IdentityVerificationStatus Status { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public AuditColumns Audit { get; set; } = new();
}

public sealed class MoneyOrderRow : IAuditedRow
{
    public Guid Id { get; set; }
    public Guid? CounterTransactionId { get; set; }
    public Guid BranchId { get; set; }
    public Guid StaffId { get; set; }
    public Guid? IdentityVerificationId { get; set; }
    public string MoneyOrderNumber { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal Fee { get; set; }
    public IdentityVerificationStatus VerificationStatus { get; set; }
    public IdentityDocumentType? DocumentType { get; set; }
    public string? MaskedDocumentIdentifier { get; set; }
    public MoneyOrderStatus Status { get; set; }
    public DateTimeOffset? RefundedAt { get; set; }
    public Guid? RefundedByStaffId { get; set; }
    public string? RefundReason { get; set; }
    public AuditColumns Audit { get; set; } = new();
}

public sealed class CounterTransactionRow : IAuditedRow
{
    public Guid Id { get; set; }
    public string TransactionNumber { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public Guid StaffId { get; set; }
    public Guid? TerminalId { get; set; }
    public Guid? MoneyOrderId { get; set; }
    public Guid? ShippingRateId { get; set; }
    public string? ShippingZoneName { get; set; }
    public ShippingServiceLevel? ShippingServiceLevel { get; set; }
    public ShipmentType? ShipmentType { get; set; }
    public decimal? ShippingWeightKilograms { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal ShippingCharge { get; set; }
    public decimal ShippingTaxRatePercent { get; set; }
    public decimal ShippingTaxAmount { get; set; }
    public decimal MoneyOrderAmount { get; set; }
    public decimal MoneyOrderFee { get; set; }
    public decimal TotalAmount { get; set; }
    public TransactionStatus Status { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public Guid? ResolvedByStaffId { get; set; }
    public string? ResolutionReason { get; set; }
    public string? ShippingRecipientName { get; set; }
    public string? ShippingAddress { get; set; }
    public decimal? ParcelLengthCentimeters { get; set; }
    public decimal? ParcelWidthCentimeters { get; set; }
    public decimal? ParcelHeightCentimeters { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public AuditColumns Audit { get; set; } = new();
}

public sealed class TransactionLineRow
{
    public Guid Id { get; set; }
    public Guid CounterTransactionId { get; set; }
    public int LineNumber { get; set; }
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public string TaxCategory { get; set; } = string.Empty;
    public decimal TaxRatePercent { get; set; }
    public decimal TaxAmount { get; set; }
}

public sealed class PaymentRow : IAuditedRow
{
    public Guid Id { get; set; }
    public Guid CounterTransactionId { get; set; }
    public PaymentMethod Method { get; set; }
    public decimal Amount { get; set; }
    public DateTimeOffset PaidAt { get; set; }
    public AuditColumns Audit { get; set; } = new();
}

public sealed class AuditEntryRow
{
    public Guid Id { get; set; }
    public Guid? StaffId { get; set; }
    public Guid? BranchId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public int ResultStatusCode { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public long? DurationMilliseconds { get; set; }
    public string? RouteValuesJson { get; set; }
    public string? SafeDetailsJson { get; set; }
}

public sealed class OperationalNotificationRow
{
    public Guid Id { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? StaffId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string? ReferenceId { get; set; }
    public bool IsAcknowledged { get; set; }
}

public sealed class OutboxMessageRow
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
}
