using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApexPost.Courier.Core.Api;

// DTOs for the ApexPost API v2. Properties are nullable/defaulted so missing or null fields never crash the app;
// unknown fields are ignored by System.Text.Json.

public enum ShipmentStatusDto
{
    Created,
    PickedUp,
    InTransit,
    OutForDelivery,
    Delivered,
    Failed
}

public sealed class ShipmentDto
{
    public string? TrackingNumber { get; init; }
    public ShipmentStatusDto Status { get; init; }
    public decimal WeightKg { get; init; }
    public RecipientDto? Recipient { get; init; }
    public LatestScanDto? LatestScan { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed class RecipientDto
{
    public string? Name { get; init; }
    public string? Phone { get; init; }
    public string? Address { get; init; }
    public string? City { get; init; }
}

public sealed class LatestScanDto
{
    public string? ScanType { get; init; }
    public string? Location { get; init; }
    public DateTimeOffset? ScannedAtUtc { get; init; }
}

public sealed class ScanUploadRequestDto
{
    public List<ScanUploadItemDto> Scans { get; init; } = [];
}

public sealed class ScanUploadItemDto
{
    public required string ScanId { get; init; }
    public required string ScanType { get; init; }
    public required string CourierId { get; init; }
    public required string Location { get; init; }
    public required DateTimeOffset ScannedAtUtc { get; init; }
    public string? Notes { get; init; }
    public string? SignatureName { get; init; }
}

public sealed class ScanUploadResultDto
{
    public int Accepted { get; init; }
    public int DuplicatesSkipped { get; init; }
    public List<RejectedScanDto>? Rejected { get; init; }
}

public sealed class RejectedScanDto
{
    public string? ScanId { get; init; }
    public string? Reason { get; init; }
}

/// <summary>RFC 9457 Problem Details / ValidationProblemDetails as sent by the API.</summary>
public sealed class ProblemDetailsDto
{
    public string? Type { get; init; }
    public string? Title { get; init; }
    public int? Status { get; init; }
    public string? Detail { get; init; }
    public string? Instance { get; init; }
    public string? TraceId { get; init; }
    public Dictionary<string, string[]>? Errors { get; init; }
}

/// <summary>Response of the API's Development-only /dev/token endpoint.</summary>
public sealed class DevTokenDto
{
    public string? AccessToken { get; init; }
    public DateTimeOffset? ExpiresAtUtc { get; init; }
}

/// <summary>
/// System.Text.Json source-generated metadata for every DTO the app sends or receives (no reflection at run time,
/// trimming/AOT friendly on mobile). Options match the v2 API: camelCase, case-insensitive, enums as strings.
/// </summary>
[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    UseStringEnumConverter = true,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ShipmentDto))]
[JsonSerializable(typeof(ScanUploadRequestDto))]
[JsonSerializable(typeof(ScanUploadResultDto))]
[JsonSerializable(typeof(ProblemDetailsDto))]
[JsonSerializable(typeof(DevTokenDto))]
public sealed partial class CourierJsonContext : JsonSerializerContext;

/// <summary>The single, shared serializer configuration for the courier app.</summary>
public static class CourierJson
{
    public static JsonSerializerOptions Options => CourierJsonContext.Default.Options;
}
