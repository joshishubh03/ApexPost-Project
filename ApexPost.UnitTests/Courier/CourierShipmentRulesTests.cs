using ApexPost.Domain.Courier;
using ApexPost.UnitTests.TestDoubles;

namespace ApexPost.UnitTests.Courier;

/// <summary>
/// Week 5 unit tests for the courier shipment domain (risk R1 status transitions, R8 validation).
/// Naming: Method_Scenario_ExpectedResult. Every test is Arrange / Act / Assert.
/// </summary>
public sealed class CourierShipmentRulesTests
{
    private static readonly DateTimeOffset Now = FixedClock.DefaultNow;

    // The documented status machine, written out independently of the production table so the test is a second
    // opinion rather than a copy of the implementation.
    private static readonly HashSet<(CourierShipmentStatus From, CourierShipmentStatus To)> Allowed =
    [
        (CourierShipmentStatus.Created, CourierShipmentStatus.PickedUp),
        (CourierShipmentStatus.PickedUp, CourierShipmentStatus.InTransit),
        (CourierShipmentStatus.InTransit, CourierShipmentStatus.OutForDelivery),
        (CourierShipmentStatus.OutForDelivery, CourierShipmentStatus.Delivered),
        (CourierShipmentStatus.OutForDelivery, CourierShipmentStatus.Failed)
    ];

    /// <summary>Every (from, to) pair of statuses: 6 x 6 = 36 rows.</summary>
    public static TheoryData<CourierShipmentStatus, CourierShipmentStatus> AllStatusPairs()
    {
        var data = new TheoryData<CourierShipmentStatus, CourierShipmentStatus>();
        foreach (var from in Enum.GetValues<CourierShipmentStatus>())
        {
            foreach (var to in Enum.GetValues<CourierShipmentStatus>())
            {
                data.Add(from, to);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllStatusPairs))]
    public void IsAllowedTransition_AnyPairOfStatuses_MatchesTheDocumentedStatusMachine(
        CourierShipmentStatus from, CourierShipmentStatus to)
    {
        // Arrange
        var expected = Allowed.Contains((from, to));

        // Act
        var actual = CourierShipment.IsAllowedTransition(from, to);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(CourierShipmentStatus.Delivered, CourierShipmentStatus.InTransit)]
    [InlineData(CourierShipmentStatus.Delivered, CourierShipmentStatus.Failed)]
    [InlineData(CourierShipmentStatus.Failed, CourierShipmentStatus.OutForDelivery)]
    [InlineData(CourierShipmentStatus.InTransit, CourierShipmentStatus.PickedUp)]
    [InlineData(CourierShipmentStatus.Created, CourierShipmentStatus.Delivered)]
    public void ChangeStatus_NotAllowedTransition_ThrowsAndKeepsTheStatus(
        CourierShipmentStatus current, CourierShipmentStatus requested)
    {
        // Arrange
        var shipment = ShipmentIn(current);

        // Act
        var exception = Assert.Throws<InvalidShipmentStatusTransitionException>(() => shipment.ChangeStatus(requested, Now));

        // Assert
        Assert.Equal(current, shipment.Status);
        Assert.Equal(current, exception.From);
        Assert.Equal(requested, exception.To);
    }

    [Fact]
    public void ChangeStatus_AllowedTransition_MovesTheStatusAndStampsTheTime()
    {
        // Arrange
        var shipment = ShipmentIn(CourierShipmentStatus.Created);
        var later = Now.AddHours(2);

        // Act
        shipment.ChangeStatus(CourierShipmentStatus.PickedUp, later);

        // Assert
        Assert.Equal(CourierShipmentStatus.PickedUp, shipment.Status);
        Assert.Equal(later, shipment.UpdatedAtUtc);
    }

    [Theory]
    [InlineData("APX-00000000")]
    [InlineData("APX-12345678")]
    [InlineData("APX-99999999")]
    public void Create_TrackingNumberWithAPXAndEightDigits_IsAccepted(string trackingNumber)
    {
        // Act
        var shipment = CourierShipment.Create(trackingNumber, 1m, CourierShipmentDomainTests.Recipient(), Now);

        // Assert
        Assert.Equal(trackingNumber, shipment.TrackingNumber);
        Assert.Equal(CourierShipmentStatus.Created, shipment.Status);
    }

    [Theory]
    [InlineData("APX-1234567")]      // 7 digits (one short)
    [InlineData("APX-123456789")]    // 9 digits (one too many)
    [InlineData("APX-1234567A")]     // letter in the number
    [InlineData("APX12345678")]      // missing hyphen
    [InlineData("ABC-12345678")]     // wrong prefix
    [InlineData(" APX-12345678")]    // leading space (the service normalises; the domain does not)
    [InlineData("")]
    public void Create_MalformedTrackingNumber_ThrowsArgumentException(string trackingNumber)
    {
        // Act + Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            CourierShipment.Create(trackingNumber, 1m, CourierShipmentDomainTests.Recipient(), Now));
        Assert.Equal("trackingNumber", exception.ParamName);
    }

    [Theory]
    [InlineData("0.1")]     // lower boundary
    [InlineData("0.11")]
    [InlineData("35")]
    [InlineData("69.99")]
    [InlineData("70")]      // upper boundary
    public void Create_WeightInsideTheLimits_IsAccepted(string weight)
    {
        // Arrange
        var kilograms = decimal.Parse(weight, System.Globalization.CultureInfo.InvariantCulture);

        // Act
        var shipment = CourierShipment.Create("APX-12345678", kilograms, CourierShipmentDomainTests.Recipient(), Now);

        // Assert
        Assert.Equal(kilograms, shipment.WeightKilograms);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("0.09")]    // just below the lower boundary
    [InlineData("70.01")]   // just above the upper boundary
    [InlineData("-1")]
    [InlineData("1000")]
    public void Create_WeightOutsideTheLimits_ThrowsArgumentOutOfRangeException(string weight)
    {
        // Arrange
        var kilograms = decimal.Parse(weight, System.Globalization.CultureInfo.InvariantCulture);

        // Act + Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CourierShipment.Create("APX-12345678", kilograms, CourierShipmentDomainTests.Recipient(), Now));
    }

    [Theory]
    [InlineData(CourierShipmentStatus.Delivered, CourierScanType.Failed)]
    [InlineData(CourierShipmentStatus.Delivered, CourierScanType.InTransit)]
    [InlineData(CourierShipmentStatus.Failed, CourierScanType.Delivered)]
    public void ApplyScan_ShipmentAlreadyFinal_ReturnsAReasonAndKeepsTheStatus(
        CourierShipmentStatus final, CourierScanType scanType)
    {
        // Arrange
        var shipment = ShipmentIn(final);

        // Act
        var reason = shipment.ApplyScan(scanType, Now);

        // Assert
        Assert.NotNull(reason);
        Assert.Contains(final.ToString(), reason, StringComparison.Ordinal);
        Assert.Equal(final, shipment.Status);
    }

    [Fact]
    public void StatusAfter_UndefinedScanType_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var undefined = (CourierScanType)99;

        // Act + Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => CourierShipment.StatusAfter(undefined));
    }

    [Fact]
    public void Constructor_EmptyId_ThrowsArgumentException()
    {
        // Act + Assert
        Assert.Throws<ArgumentException>(() => new CourierShipment(
            Guid.Empty, "APX-12345678", 1m, CourierShipmentDomainTests.Recipient(), CourierShipmentStatus.Created, Now, Now));
    }

    [Theory]
    [InlineData(Recipient.MaxNameLength, false)]       // exactly the maximum is fine
    [InlineData(Recipient.MaxNameLength + 1, true)]    // one character more is not
    public void RecipientConstructor_NameLengthAtTheBoundary_IsCheckedInclusively(int length, bool shouldThrow)
    {
        // Arrange
        var name = new string('N', length);

        // Act
        var exception = Record.Exception(() => new Recipient(name, "+15550100123", "12 Elm Row", "Maplewood"));

        // Assert
        if (shouldThrow)
        {
            Assert.IsType<ArgumentException>(exception);
        }
        else
        {
            Assert.Null(exception);
        }
    }

    private static CourierShipment ShipmentIn(CourierShipmentStatus status) =>
        new(Guid.NewGuid(), "APX-12345678", 2.5m, CourierShipmentDomainTests.Recipient(), status, Now, Now);
}
