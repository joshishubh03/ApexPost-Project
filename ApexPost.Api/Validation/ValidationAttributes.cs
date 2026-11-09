using System.ComponentModel.DataAnnotations;

namespace ApexPost.Api.Validation;

/// <summary>Shared patterns for request validation.</summary>
public static class ValidationPatterns
{
    /// <summary>APX- followed by exactly 8 digits.</summary>
    public const string TrackingNumber = @"^APX-\d{8}$";

    /// <summary>E.164 style: optional +, then 7-15 digits, first digit not 0 (e.g. +15550100123).</summary>
    public const string Phone = @"^\+?[1-9]\d{6,14}$";

    /// <summary>Courier device scan ID from Part A: scan::{Guid}.</summary>
    public const string ScanId = @"^scan::[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$";
}

/// <summary>
/// Custom rule: a timestamp may not be in the future (a small clock-skew allowance is accepted, because courier
/// phones are not perfectly synchronised).
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class NotInFutureAttribute(int allowedClockSkewSeconds = 300) : ValidationAttribute(
    "{0} cannot be in the future.")
{
    public int AllowedClockSkewSeconds { get; } = allowedClockSkewSeconds;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var now = validationContext.GetService(typeof(TimeProvider)) is TimeProvider clock
            ? clock.GetUtcNow()
            : DateTimeOffset.UtcNow;
        var limit = now.AddSeconds(AllowedClockSkewSeconds);

        var isInFuture = value switch
        {
            null => false,
            DateTimeOffset offset => offset > limit,
            DateTime dateTime => new DateTimeOffset(dateTime.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
                : dateTime) > limit,
            _ => false
        };

        return isInFuture
            ? new ValidationResult(FormatErrorMessage(validationContext.DisplayName),
                validationContext.MemberName is { } member ? new[] { member } : null)
            : ValidationResult.Success;
    }
}
