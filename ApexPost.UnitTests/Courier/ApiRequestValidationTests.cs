using System.ComponentModel.DataAnnotations;
using ApexPost.Api.Contracts;
using ApexPost.Api.Validation;
using ApexPost.Domain.Courier;
using ApexPost.UnitTests.TestDoubles;

namespace ApexPost.UnitTests.Courier;

/// <summary>
/// Week 5 unit tests for the API request DTO rules (risk R8). The same DataAnnotations the [ApiController] pipeline
/// runs are executed directly with <see cref="Validator"/>, so each rule is checked in microseconds without a host.
/// The HTTP side (400 ValidationProblemDetails) is covered by the API integration tests.
/// </summary>
public sealed class ApiRequestValidationTests
{
    private static readonly DateTimeOffset Now = FixedClock.DefaultNow;

    [Theory]
    [InlineData("APX-10000001", true)]
    [InlineData("APX-00000000", true)]
    [InlineData("APX-1000000", false)]      // 7 digits
    [InlineData("APX-100000011", false)]    // 9 digits
    [InlineData("apx-10000001", false)]     // the regex is case-sensitive
    [InlineData("APX 10000001", false)]
    [InlineData("", false)]
    public void CreateShipmentRequest_TrackingNumberFormat_IsValidatedByTheRegex(string trackingNumber, bool valid)
    {
        // Arrange
        var request = ValidCreateRequest(trackingNumber: trackingNumber);

        // Act
        var errors = Validate(request);

        // Assert
        Assert.Equal(valid, !errors.Any(error => error.MemberNames.Contains(nameof(CreateShipmentRequest.TrackingNumber))));
    }

    [Theory]
    [InlineData("0.1", true)]     // lower boundary is inclusive
    [InlineData("0.09", false)]
    [InlineData("70", true)]      // upper boundary is inclusive
    [InlineData("70.01", false)]
    [InlineData("0", false)]
    public void CreateShipmentRequest_WeightAtAndAroundTheLimits_IsValidatedInclusively(string weight, bool valid)
    {
        // Arrange
        var request = ValidCreateRequest(weightKg: decimal.Parse(weight, System.Globalization.CultureInfo.InvariantCulture));

        // Act
        var errors = Validate(request);

        // Assert
        Assert.Equal(valid, !errors.Any(error => error.MemberNames.Contains(nameof(CreateShipmentRequest.WeightKg))));
    }

    [Theory]
    [InlineData("+15550100123", true)]
    [InlineData("15550100123", true)]       // + is optional
    [InlineData("+1555010", true)]          // 7 digits: shortest allowed
    [InlineData("+155501", false)]          // 6 digits: too short
    [InlineData("+1555010012345678", false)] // 16 digits: too long
    [InlineData("+05550100123", false)]     // first digit cannot be 0
    [InlineData("555-0100", false)]
    public void CreateShipmentRequest_PhoneNumber_MustBeE164(string phone, bool valid)
    {
        // Arrange
        var request = ValidCreateRequest(phone: phone);

        // Act
        var errors = Validate(request);

        // Assert
        Assert.Equal(valid, !errors.Any(error => error.MemberNames.Contains(nameof(CreateShipmentRequest.RecipientPhone))));
    }

    [Fact]
    public void CreateShipmentRequest_MissingRecipientFields_ReportsEachMissingField()
    {
        // Arrange
        var request = new CreateShipmentRequest { TrackingNumber = "APX-10000001", WeightKg = 1m };

        // Act
        var members = Validate(request).SelectMany(error => error.MemberNames).ToHashSet();

        // Assert
        Assert.Contains(nameof(CreateShipmentRequest.RecipientName), members);
        Assert.Contains(nameof(CreateShipmentRequest.RecipientPhone), members);
        Assert.Contains(nameof(CreateShipmentRequest.RecipientAddress), members);
        Assert.Contains(nameof(CreateShipmentRequest.City), members);
    }

    [Theory]
    [InlineData(0, true)]          // now
    [InlineData(300, true)]        // exactly the 5-minute clock-skew allowance
    [InlineData(301, false)]       // one second beyond it
    [InlineData(-86_400, true)]    // yesterday
    public void NotInFuture_ScanTimeRelativeToTheInjectedClock_HonoursTheSkewAllowance(int secondsFromNow, bool valid)
    {
        // Arrange: the attribute reads the clock from the validation services, so the test controls "now".
        var request = ValidScan(scannedAt: Now.AddSeconds(secondsFromNow));

        // Act
        var errors = Validate(request, new FixedClock(Now));

        // Assert
        Assert.Equal(valid, !errors.Any(error => error.MemberNames.Contains(nameof(ScanUploadItemRequest.ScannedAtUtc))));
    }

    [Theory]
    [InlineData(CourierScanType.Delivered, null, false)]
    [InlineData(CourierScanType.Delivered, "  ", false)]
    [InlineData(CourierScanType.Delivered, "J. Doe", true)]
    [InlineData(CourierScanType.Pickup, null, true)]
    public void ScanUploadItemRequest_DeliveredScan_RequiresASignatureName(CourierScanType scanType, string? signature, bool valid)
    {
        // Arrange
        var request = ValidScan(scanType: scanType, signature: signature);

        // Act
        var errors = Validate(request, new FixedClock(Now));

        // Assert
        Assert.Equal(valid, !errors.Any(error => error.MemberNames.Contains(nameof(ScanUploadItemRequest.SignatureName))));
    }

    [Theory]
    [InlineData("scan::3f2504e0-4f89-11d3-9a0c-0305e82c3301", true)]
    [InlineData("3f2504e0-4f89-11d3-9a0c-0305e82c3301", false)]      // missing prefix
    [InlineData("scan::not-a-guid", false)]
    [InlineData("scan:3f2504e0-4f89-11d3-9a0c-0305e82c3301", false)]  // single colon
    public void ScanUploadItemRequest_ScanId_MustUseThePartAFormat(string scanId, bool valid)
    {
        // Arrange
        var request = ValidScan(scanId: scanId);

        // Act
        var errors = Validate(request, new FixedClock(Now));

        // Assert
        Assert.Equal(valid, !errors.Any(error => error.MemberNames.Contains(nameof(ScanUploadItemRequest.ScanId))));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(100, true)]   // upper boundary
    [InlineData(101, false)]
    public void ScanUploadRequest_NumberOfScans_IsBetweenOneAndOneHundred(int count, bool valid)
    {
        // Arrange
        var request = new ScanUploadRequest { Scans = Enumerable.Range(0, count).Select(_ => ValidScan()).ToList() };

        // Act (only the request object itself: the list's own attributes)
        var errors = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), errors, validateAllProperties: true);

        // Assert
        Assert.Equal(valid, errors.Count == 0);
    }

    [Fact]
    public void ValidationPatterns_TrackingNumber_MatchesTheDomainRule()
    {
        // The API regex and the domain regex are separate constants; this test keeps them in step.
        Assert.Equal(CourierShipment.TrackingNumberPattern, ValidationPatterns.TrackingNumber);
    }

    private static CreateShipmentRequest ValidCreateRequest(
        string trackingNumber = "APX-10000001",
        decimal weightKg = 2.5m,
        string phone = "+15550100123") => new()
    {
        TrackingNumber = trackingNumber,
        WeightKg = weightKg,
        RecipientName = "Jordan Example",
        RecipientPhone = phone,
        RecipientAddress = "12 Elm Row",
        City = "Maplewood"
    };

    private static ScanUploadItemRequest ValidScan(
        string scanId = "scan::3f2504e0-4f89-11d3-9a0c-0305e82c3301",
        CourierScanType scanType = CourierScanType.Pickup,
        DateTimeOffset? scannedAt = null,
        string? signature = null) => new()
    {
        ScanId = scanId,
        ScanType = scanType,
        CourierId = "courier-007",
        Location = "Maplewood Depot",
        ScannedAtUtc = scannedAt ?? Now.AddMinutes(-10),
        SignatureName = signature
    };

    private static List<ValidationResult> Validate(object instance, TimeProvider? clock = null)
    {
        var context = new ValidationContext(instance, new ClockServices(clock), items: null);
        var errors = new List<ValidationResult>();
        Validator.TryValidateObject(instance, context, errors, validateAllProperties: true);
        return errors;
    }

    /// <summary>Minimal service provider: hands the fixed clock to [NotInFuture].</summary>
    private sealed class ClockServices(TimeProvider? clock) : IServiceProvider
    {
        public object? GetService(Type serviceType) => serviceType == typeof(TimeProvider) ? clock : null;
    }
}
