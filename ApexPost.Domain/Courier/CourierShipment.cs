using System.Text.RegularExpressions;

namespace ApexPost.Domain.Courier;

/// <summary>Lifecycle of a courier-tracked shipment (Week 4 API).</summary>
public enum CourierShipmentStatus
{
    Created,
    PickedUp,
    InTransit,
    OutForDelivery,
    Delivered,
    Failed
}

/// <summary>Scan types recorded by the courier device (same names as the offline store in Part A).</summary>
public enum CourierScanType
{
    Pickup,
    InTransit,
    Delivered,
    Failed
}

/// <summary>Who receives the parcel. Grouped into one value object (the v2 API nests it as <c>recipient</c>).</summary>
public sealed record Recipient
{
    public const int MaxNameLength = 120;
    public const int MaxPhoneLength = 20;
    public const int MaxAddressLength = 250;
    public const int MaxCityLength = 80;

    public Recipient(string name, string phone, string address, string city)
    {
        Name = Required(name, MaxNameLength, nameof(name));
        Phone = Required(phone, MaxPhoneLength, nameof(phone));
        Address = Required(address, MaxAddressLength, nameof(address));
        City = Required(city, MaxCityLength, nameof(city));
    }

    public string Name { get; }
    public string Phone { get; }
    public string Address { get; }
    public string City { get; }

    private static string Required(string value, int maxLength, string parameter)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new ArgumentException("A value is required.", parameter);
        if (trimmed.Length > maxLength)
            throw new ArgumentException($"At most {maxLength} characters are allowed.", parameter);
        return trimmed;
    }
}

/// <summary>
/// A parcel tracked by tracking number (APX-########). Owns the status machine:
/// Created -> PickedUp -> InTransit -> OutForDelivery -> Delivered / Failed.
/// </summary>
public sealed partial class CourierShipment
{
    public const string TrackingNumberPattern = @"^APX-\d{8}$";
    public const decimal MinimumWeightKilograms = 0.1m;
    public const decimal MaximumWeightKilograms = 70m;

    private static readonly IReadOnlyDictionary<CourierShipmentStatus, CourierShipmentStatus[]> AllowedTransitions =
        new Dictionary<CourierShipmentStatus, CourierShipmentStatus[]>
        {
            [CourierShipmentStatus.Created] = [CourierShipmentStatus.PickedUp],
            [CourierShipmentStatus.PickedUp] = [CourierShipmentStatus.InTransit],
            [CourierShipmentStatus.InTransit] = [CourierShipmentStatus.OutForDelivery],
            [CourierShipmentStatus.OutForDelivery] = [CourierShipmentStatus.Delivered, CourierShipmentStatus.Failed],
            [CourierShipmentStatus.Delivered] = [],
            [CourierShipmentStatus.Failed] = []
        };

    public CourierShipment(
        Guid id,
        string trackingNumber,
        decimal weightKilograms,
        Recipient recipient,
        CourierShipmentStatus status,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("A shipment ID is required.", nameof(id));
        if (!IsValidTrackingNumber(trackingNumber))
            throw new ArgumentException("Tracking numbers must match APX-######## (8 digits).", nameof(trackingNumber));
        if (!Enum.IsDefined(status))
            throw new ArgumentOutOfRangeException(nameof(status));

        Id = id;
        TrackingNumber = trackingNumber;
        WeightKilograms = ValidWeight(weightKilograms);
        Recipient = recipient ?? throw new ArgumentNullException(nameof(recipient));
        Status = status;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public Guid Id { get; }
    public string TrackingNumber { get; }
    public decimal WeightKilograms { get; private set; }
    public Recipient Recipient { get; private set; }
    public CourierShipmentStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public bool IsFinal => Status is CourierShipmentStatus.Delivered or CourierShipmentStatus.Failed;

    /// <summary>Only a shipment nobody has picked up yet may be deleted.</summary>
    public bool CanBeDeleted => Status == CourierShipmentStatus.Created;

    public static CourierShipment Create(string trackingNumber, decimal weightKilograms, Recipient recipient, DateTimeOffset now) =>
        new(Guid.NewGuid(), trackingNumber, weightKilograms, recipient, CourierShipmentStatus.Created, now, now);

    public static bool IsValidTrackingNumber(string? value) => value is not null && TrackingNumberRegex().IsMatch(value);

    public static bool IsAllowedTransition(CourierShipmentStatus from, CourierShipmentStatus to) =>
        AllowedTransitions.TryGetValue(from, out var targets) && targets.Contains(to);

    public static IReadOnlyList<CourierShipmentStatus> NextStatuses(CourierShipmentStatus from) =>
        AllowedTransitions.TryGetValue(from, out var targets) ? targets : [];

    /// <summary>Status a scan of this type moves the shipment to.</summary>
    public static CourierShipmentStatus StatusAfter(CourierScanType scanType) => scanType switch
    {
        CourierScanType.Pickup => CourierShipmentStatus.PickedUp,
        CourierScanType.InTransit => CourierShipmentStatus.InTransit,
        CourierScanType.Delivered => CourierShipmentStatus.Delivered,
        CourierScanType.Failed => CourierShipmentStatus.Failed,
        _ => throw new ArgumentOutOfRangeException(nameof(scanType))
    };

    /// <summary>Replaces the editable details (weight and recipient). Not allowed once delivered or failed.</summary>
    public void ReplaceDetails(decimal weightKilograms, Recipient recipient, DateTimeOffset now)
    {
        if (IsFinal)
            throw new ShipmentRuleViolationException($"Shipment {TrackingNumber} is {Status} and can no longer be edited.");

        WeightKilograms = ValidWeight(weightKilograms);
        Recipient = recipient ?? throw new ArgumentNullException(nameof(recipient));
        UpdatedAtUtc = now;
    }

    /// <summary>Moves to <paramref name="newStatus"/> when the status machine allows it.</summary>
    public void ChangeStatus(CourierShipmentStatus newStatus, DateTimeOffset now)
    {
        if (!IsAllowedTransition(Status, newStatus))
            throw new InvalidShipmentStatusTransitionException(TrackingNumber, Status, newStatus);

        Status = newStatus;
        UpdatedAtUtc = now;
    }

    /// <summary>
    /// Applies a courier scan. Returns null when the scan is accepted (the status advances, or the scan repeats the
    /// current status), otherwise the reason it is rejected. Scans never move a shipment backwards.
    /// </summary>
    public string? ApplyScan(CourierScanType scanType, DateTimeOffset now)
    {
        var target = StatusAfter(scanType);
        if (target == Status)
            return null;

        if (IsFinal)
            return $"Shipment is already {Status}; a {scanType} scan is not allowed.";

        if (!IsAllowedTransition(Status, target))
            return $"Invalid status transition {Status} -> {target} for a {scanType} scan.";

        Status = target;
        UpdatedAtUtc = now;
        return null;
    }

    private static decimal ValidWeight(decimal weightKilograms) =>
        weightKilograms is >= MinimumWeightKilograms and <= MaximumWeightKilograms
            ? decimal.Round(weightKilograms, 2)
            : throw new ArgumentOutOfRangeException(nameof(weightKilograms),
                $"Weight must be between {MinimumWeightKilograms} and {MaximumWeightKilograms} kg.");

    [GeneratedRegex(TrackingNumberPattern, RegexOptions.CultureInvariant)]
    private static partial Regex TrackingNumberRegex();
}

/// <summary>A scan stored on the server. <see cref="Id"/> is the GUID part of the device's <c>scan::{Guid}</c> ID.</summary>
public sealed record CourierScan(
    Guid Id,
    Guid ShipmentId,
    CourierScanType ScanType,
    string CourierId,
    string Location,
    DateTimeOffset ScannedAtUtc,
    string? Notes,
    string? SignatureName,
    DateTimeOffset ReceivedAtUtc);

/// <summary>The requested status change is not allowed by the status machine (HTTP 409).</summary>
public sealed class InvalidShipmentStatusTransitionException(
    string trackingNumber,
    CourierShipmentStatus from,
    CourierShipmentStatus to)
    : InvalidOperationException(
        $"Shipment {trackingNumber} cannot move from {from} to {to}. Allowed next: " +
        $"{(CourierShipment.NextStatuses(from) is { Count: > 0 } next ? string.Join(", ", next) : "none (final status)")}.")
{
    public CourierShipmentStatus From { get; } = from;
    public CourierShipmentStatus To { get; } = to;
}

/// <summary>The shipment's current state forbids the operation, e.g. deleting after pickup (HTTP 409).</summary>
public sealed class ShipmentRuleViolationException(string message) : InvalidOperationException(message);
