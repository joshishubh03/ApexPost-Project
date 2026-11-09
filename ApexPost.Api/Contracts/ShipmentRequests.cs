using System.ComponentModel.DataAnnotations;
using ApexPost.Api.Validation;
using ApexPost.Domain.Courier;

namespace ApexPost.Api.Contracts;

/// <summary>Body of POST /shipments. Never bound to an EF entity - mapped to a command by the controller.</summary>
public sealed class CreateShipmentRequest
{
    /// <summary>Tracking number, APX- followed by 8 digits (e.g. APX-10000001).</summary>
    [Required]
    [RegularExpression(ValidationPatterns.TrackingNumber, ErrorMessage = "The tracking number must match APX-######## (8 digits).")]
    public string TrackingNumber { get; init; } = string.Empty;

    /// <summary>Parcel weight in kilograms, 0.1 to 70.</summary>
    [Range(typeof(decimal), "0.1", "70", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true,
        ErrorMessage = "Weight must be between 0.1 and 70 kg.")]
    public decimal WeightKg { get; init; }

    /// <summary>Recipient full name.</summary>
    [Required]
    [StringLength(Recipient.MaxNameLength)]
    public string RecipientName { get; init; } = string.Empty;

    /// <summary>Recipient phone in E.164 format, e.g. +15550100123.</summary>
    [Required]
    [RegularExpression(ValidationPatterns.Phone, ErrorMessage = "The phone number must be in E.164 format, e.g. +15550100123.")]
    public string RecipientPhone { get; init; } = string.Empty;

    /// <summary>Street address.</summary>
    [Required]
    [StringLength(Recipient.MaxAddressLength)]
    public string RecipientAddress { get; init; } = string.Empty;

    /// <summary>Destination city (used by the list filter).</summary>
    [Required]
    [StringLength(Recipient.MaxCityLength)]
    public string City { get; init; } = string.Empty;
}

/// <summary>Body of PUT /shipments/{trackingNumber}: replaces the editable details (weight and recipient).</summary>
public sealed class UpdateShipmentRequest
{
    /// <summary>Parcel weight in kilograms, 0.1 to 70.</summary>
    [Range(typeof(decimal), "0.1", "70", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true,
        ErrorMessage = "Weight must be between 0.1 and 70 kg.")]
    public decimal WeightKg { get; init; }

    /// <summary>Recipient full name.</summary>
    [Required]
    [StringLength(Recipient.MaxNameLength)]
    public string RecipientName { get; init; } = string.Empty;

    /// <summary>Recipient phone in E.164 format.</summary>
    [Required]
    [RegularExpression(ValidationPatterns.Phone, ErrorMessage = "The phone number must be in E.164 format, e.g. +15550100123.")]
    public string RecipientPhone { get; init; } = string.Empty;

    /// <summary>Street address.</summary>
    [Required]
    [StringLength(Recipient.MaxAddressLength)]
    public string RecipientAddress { get; init; } = string.Empty;

    /// <summary>Destination city.</summary>
    [Required]
    [StringLength(Recipient.MaxCityLength)]
    public string City { get; init; } = string.Empty;
}

/// <summary>Body of PATCH /shipments/{trackingNumber}/status.</summary>
public sealed class UpdateStatusRequest
{
    /// <summary>
    /// New status. Allowed transitions: Created -> PickedUp -> InTransit -> OutForDelivery -> Delivered / Failed.
    /// </summary>
    [Required]
    public CourierShipmentStatus? Status { get; init; }
}

/// <summary>Query string of GET /shipments.</summary>
public sealed class ShipmentListQuery
{
    /// <summary>1-based page number.</summary>
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    /// <summary>Items per page (default 20, maximum 50).</summary>
    [Range(1, 50)]
    public int PageSize { get; init; } = 20;

    /// <summary>Optional status filter.</summary>
    public CourierShipmentStatus? Status { get; init; }

    /// <summary>Optional city filter (exact match).</summary>
    [StringLength(Recipient.MaxCityLength)]
    public string? City { get; init; }
}
