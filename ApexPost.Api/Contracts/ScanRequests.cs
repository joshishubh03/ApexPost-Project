using System.ComponentModel.DataAnnotations;
using ApexPost.Api.Validation;
using ApexPost.Domain.Courier;

namespace ApexPost.Api.Contracts;

/// <summary>Body of POST /shipments/{trackingNumber}/scans: offline scans from the courier device (Part A).</summary>
public sealed class ScanUploadRequest
{
    /// <summary>1 to 100 scans. Re-sending a scan ID that is already stored is safe: it is skipped, not duplicated.</summary>
    [Required]
    [MinLength(1)]
    [MaxLength(100)]
    public List<ScanUploadItemRequest> Scans { get; init; } = [];
}

/// <summary>One offline scan.</summary>
public sealed class ScanUploadItemRequest : IValidatableObject
{
    /// <summary>The device document ID, scan::{Guid}. The server de-duplicates on it.</summary>
    [Required]
    [RegularExpression(ValidationPatterns.ScanId, ErrorMessage = "The scan ID must use the format scan::{Guid}.")]
    public string ScanId { get; init; } = string.Empty;

    /// <summary>Pickup, InTransit, Delivered or Failed.</summary>
    [Required]
    public CourierScanType? ScanType { get; init; }

    /// <summary>Courier who took the scan.</summary>
    [Required]
    [StringLength(40)]
    public string CourierId { get; init; } = string.Empty;

    /// <summary>Where the scan happened.</summary>
    [Required]
    [StringLength(120)]
    public string Location { get; init; } = string.Empty;

    /// <summary>When the scan happened (UTC). Custom rule: cannot be in the future.</summary>
    [Required]
    [NotInFuture]
    public DateTimeOffset? ScannedAtUtc { get; init; }

    /// <summary>Optional courier notes.</summary>
    [StringLength(500)]
    public string? Notes { get; init; }

    /// <summary>Name of the person who signed. Custom rule: required for a Delivered scan.</summary>
    [StringLength(120)]
    public string? SignatureName { get; init; }

    /// <summary>Custom cross-field rule (IValidatableObject): a Delivered scan requires a signature name.</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ScanType == CourierScanType.Delivered && string.IsNullOrWhiteSpace(SignatureName))
        {
            yield return new ValidationResult(
                "A Delivered scan requires the recipient's signature name.",
                [nameof(SignatureName)]);
        }
    }

    /// <summary>GUID part of <see cref="ScanId"/>.</summary>
    internal Guid ScanGuid => Guid.Parse(ScanId.AsSpan("scan::".Length));
}
