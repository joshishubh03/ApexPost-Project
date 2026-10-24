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
        PaymentMethod paymentMethod)
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
    }
}
