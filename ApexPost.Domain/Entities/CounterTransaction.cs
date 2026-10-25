using ApexPost.Domain.Enums;

namespace ApexPost.Domain.Entities;

public sealed record CounterTransaction
{
    public Guid Id { get; }
    public string TransactionNumber { get; }
    public Guid BranchId { get; }
    public Guid StaffId { get; }
    public DateTimeOffset OccurredAt { get; }
    public IReadOnlyList<TransactionLine> Lines { get; }
    public Guid? ShippingRateId { get; }
    public string? ShippingZoneName { get; }
    public ShippingServiceLevel? ShippingServiceLevel { get; }
    public decimal? ShippingWeightKilograms { get; }
    public decimal Subtotal { get; }
    public decimal ShippingCharge { get; }
    public decimal TotalAmount { get; }
    public PaymentMethod PaymentMethod { get; }
    public TransactionStatus Status { get; }
    public DateTimeOffset? ResolvedAt { get; }
    public Guid? ResolvedByStaffId { get; }
    public string? ResolutionReason { get; }
    public string? ShippingRecipientName { get; }
    public string? ShippingAddress { get; }
    public decimal? ParcelLengthCentimeters { get; }
    public decimal? ParcelWidthCentimeters { get; }
    public decimal? ParcelHeightCentimeters { get; }

    public CounterTransaction(
        Guid id,
        string transactionNumber,
        Guid branchId,
        Guid staffId,
        DateTimeOffset occurredAt,
        IReadOnlyList<TransactionLine> lines,
        Guid? shippingRateId,
        string? shippingZoneName,
        ShippingServiceLevel? shippingServiceLevel,
        decimal? shippingWeightKilograms,
        decimal shippingCharge,
        PaymentMethod paymentMethod,
        TransactionStatus status = TransactionStatus.Completed,
        DateTimeOffset? resolvedAt = null,
        Guid? resolvedByStaffId = null,
        string? resolutionReason = null,
        string? shippingRecipientName = null,
        string? shippingAddress = null,
        decimal? parcelLengthCentimeters = null,
        decimal? parcelWidthCentimeters = null,
        decimal? parcelHeightCentimeters = null)
    {
        if (id == Guid.Empty || branchId == Guid.Empty || staffId == Guid.Empty)
        {
            throw new ArgumentException("Transaction, branch, and staff identifiers are required.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(transactionNumber);
        ArgumentNullException.ThrowIfNull(lines);
        if (!Enum.IsDefined(paymentMethod))
        {
            throw new ArgumentOutOfRangeException(nameof(paymentMethod));
        }

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        if (status == TransactionStatus.Completed &&
            (resolvedAt is not null || resolvedByStaffId is not null || resolutionReason is not null))
        {
            throw new ArgumentException("Completed transactions cannot contain reversal details.", nameof(status));
        }

        if (status != TransactionStatus.Completed &&
            (resolvedAt is null || resolvedByStaffId is null || resolvedByStaffId == Guid.Empty || string.IsNullOrWhiteSpace(resolutionReason)))
        {
            throw new ArgumentException("A void or refund requires an actor, time, and reason.", nameof(status));
        }

        if (lines.Count == 0 && shippingRateId is null)
        {
            throw new ArgumentException("A transaction must contain a product or a shipping service.", nameof(lines));
        }

        if (shippingRateId is null &&
            (shippingZoneName is not null || shippingServiceLevel is not null || shippingWeightKilograms is not null || shippingCharge != 0))
        {
            throw new ArgumentException("Shipping details require a shipping rate.", nameof(shippingRateId));
        }

        if (shippingRateId is not null &&
            (string.IsNullOrWhiteSpace(shippingZoneName) || shippingServiceLevel is null || shippingWeightKilograms is null || shippingWeightKilograms <= 0))
        {
            throw new ArgumentException("Complete shipping details are required when a rate is selected.", nameof(shippingRateId));
        }

        var hasAddressDetails = shippingRecipientName is not null || shippingAddress is not null ||
            parcelLengthCentimeters is not null || parcelWidthCentimeters is not null || parcelHeightCentimeters is not null;
        if (hasAddressDetails &&
            (shippingRateId is null || string.IsNullOrWhiteSpace(shippingRecipientName) ||
             string.IsNullOrWhiteSpace(shippingAddress) || parcelLengthCentimeters is null ||
             parcelWidthCentimeters is null || parcelHeightCentimeters is null))
        {
            throw new ArgumentException("Recipient, destination address, and all parcel dimensions are required together for a shipment.");
        }

        if (shippingRecipientName?.Trim().Length > 100 || shippingAddress?.Trim().Length > 300)
        {
            throw new ArgumentException("Recipient name and destination address exceed their maximum lengths.");
        }

        foreach (var dimension in new[] { parcelLengthCentimeters, parcelWidthCentimeters, parcelHeightCentimeters })
        {
            if (dimension is { } value && (value <= 0 || decimal.Round(value, 2) != value))
            {
                throw new ArgumentOutOfRangeException(nameof(parcelLengthCentimeters), "Parcel dimensions must be positive and use no more than two decimal places.");
            }
        }

        if (shippingCharge < 0 || decimal.Round(shippingCharge, 2) != shippingCharge)
        {
            throw new ArgumentOutOfRangeException(nameof(shippingCharge));
        }

        Id = id;
        TransactionNumber = transactionNumber.Trim();
        BranchId = branchId;
        StaffId = staffId;
        OccurredAt = occurredAt;
        Lines = lines.ToArray();
        ShippingRateId = shippingRateId;
        ShippingZoneName = shippingZoneName;
        ShippingServiceLevel = shippingServiceLevel;
        ShippingWeightKilograms = shippingWeightKilograms;
        Subtotal = decimal.Round(Lines.Sum(line => line.LineTotal), 2, MidpointRounding.AwayFromZero);
        ShippingCharge = shippingCharge;
        TotalAmount = decimal.Round(Subtotal + ShippingCharge, 2, MidpointRounding.AwayFromZero);
        PaymentMethod = paymentMethod;
        Status = status;
        ResolvedAt = resolvedAt;
        ResolvedByStaffId = resolvedByStaffId;
        ResolutionReason = resolutionReason?.Trim();
        ShippingRecipientName = shippingRecipientName?.Trim();
        ShippingAddress = shippingAddress?.Trim();
        ParcelLengthCentimeters = parcelLengthCentimeters;
        ParcelWidthCentimeters = parcelWidthCentimeters;
        ParcelHeightCentimeters = parcelHeightCentimeters;
    }

    public CounterTransaction Void(Guid managerId, string reason, DateTimeOffset occurredAt) =>
        Resolve(TransactionStatus.Voided, managerId, reason, occurredAt);

    public CounterTransaction Refund(Guid managerId, string reason, DateTimeOffset occurredAt) =>
        Resolve(TransactionStatus.Refunded, managerId, reason, occurredAt);

    private CounterTransaction Resolve(TransactionStatus status, Guid managerId, string reason, DateTimeOffset occurredAt)
    {
        if (Status != TransactionStatus.Completed)
        {
            throw new InvalidOperationException("Only a completed transaction can be voided or refunded.");
        }

        if (managerId == Guid.Empty)
        {
            throw new ArgumentException("The manager performing the reversal is required.", nameof(managerId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (reason.Trim().Length > 250)
        {
            throw new ArgumentException("The reason cannot exceed 250 characters.", nameof(reason));
        }

        return new CounterTransaction(
            Id, TransactionNumber, BranchId, StaffId, OccurredAt, Lines, ShippingRateId,
            ShippingZoneName, ShippingServiceLevel, ShippingWeightKilograms, ShippingCharge,
            PaymentMethod, status, occurredAt, managerId, reason, ShippingRecipientName,
            ShippingAddress, ParcelLengthCentimeters, ParcelWidthCentimeters, ParcelHeightCentimeters);
    }
}
