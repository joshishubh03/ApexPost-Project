using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Infrastructure.Persistence.Entities;

// Persistence model (relational write model). These rows are never exposed outside Infrastructure:
// repositories map them to and from the immutable domain records. Audit columns (CreatedAtUtc, CreatedBy,
// ModifiedAtUtc, ModifiedBy) and RowVersion are EF Core SHADOW properties configured in
// Configurations/*, so neither the rows nor the domain carry them. See ADR 0045.

/// <summary>Marker for rows that receive the shadow audit columns.</summary>
public interface IAuditedRow;

public sealed class BranchRow : IAuditedRow
{
    public Guid Id { get; set; }
    public BranchCode Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public bool IsOpen { get; set; }
    public TimeOnly OpeningTime { get; set; }
    public TimeOnly ClosingTime { get; set; }
    public List<CounterTerminalRow> Terminals { get; set; } = [];
    public List<StaffRow> Staff { get; set; } = [];
}

public sealed class CounterTerminalRow : IAuditedRow
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
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
}

public sealed class StaffRoleRow : IAuditedRow
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class ProductCategoryRow : IAuditedRow
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class TaxCategoryRow : IAuditedRow
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class ProductRow : IAuditedRow
{
    public Guid Id { get; set; }
    public Sku Sku { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string TaxCategory { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsActive { get; set; }
}

public sealed class BranchStockRow : IAuditedRow
{
    public Guid BranchId { get; set; }
    public Guid ProductId { get; set; }
    public int QuantityOnHand { get; set; }
    public int ReorderLevel { get; set; }
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
}

public sealed class ShippingZoneRow : IAuditedRow
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed class ShippingRateRow : IAuditedRow
{
    public Guid Id { get; set; }
    public Guid ZoneId { get; set; }
    public ShippingServiceLevel ServiceLevel { get; set; }
    public Money BasePrice { get; set; }
    public Money PricePerKilogram { get; set; }
    public Weight MaximumWeight { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Separate persistence entity for the IdentityVerification aggregate: it only has a masked identifier
/// column (varchar(16)) - there is no property that could ever hold a full document number. See ADR 0045.
/// </summary>
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
}

public sealed class CounterTransactionRow : IAuditedRow
{
    public Guid Id { get; set; }
    public string TransactionNumber { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public Guid StaffId { get; set; }
    public Guid? TerminalId { get; set; }
    public Guid? MoneyOrderId { get; set; }
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
    public List<TransactionLineRow> Lines { get; set; } = [];
    public PaymentRow? Payment { get; set; }
    public ShipmentRow? Shipment { get; set; }
}

public sealed class TransactionLineRow
{
    public Guid Id { get; set; }
    public Guid CounterTransactionId { get; set; }
    public int LineNumber { get; set; }
    public Guid ProductId { get; set; }
    public Sku Sku { get; set; }
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
}

public enum ShipmentStatus
{
    Booked,
    Cancelled
}

/// <summary>Owned type: parcel dimensions (all three or none; letters have none).</summary>
public sealed class ParcelDimensions
{
    public decimal LengthCentimeters { get; set; }
    public decimal WidthCentimeters { get; set; }
    public decimal HeightCentimeters { get; set; }
}

/// <summary>Owned type: who and where a shipment is addressed to.</summary>
public sealed class ShippingAddress
{
    public string RecipientName { get; set; } = string.Empty;
    public string AddressLine { get; set; } = string.Empty;
}

/// <summary>Shipment booked as part of a counter transaction (0..1 per transaction).</summary>
public sealed class ShipmentRow : IAuditedRow
{
    public Guid Id { get; set; }
    public Guid CounterTransactionId { get; set; }
    public Guid ShippingRateId { get; set; }
    public ShipmentType? ShipmentType { get; set; }
    public Weight Weight { get; set; }
    public ParcelDimensions? Dimensions { get; set; }
    public ShippingAddress? Destination { get; set; }
    public ShipmentStatus Status { get; set; }
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
