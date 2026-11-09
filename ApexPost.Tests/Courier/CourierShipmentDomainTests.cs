using ApexPost.Domain.Courier;

namespace ApexPost.Tests.Courier;

public sealed class CourierShipmentDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(CourierShipmentStatus.Created, CourierShipmentStatus.PickedUp, true)]
    [InlineData(CourierShipmentStatus.PickedUp, CourierShipmentStatus.InTransit, true)]
    [InlineData(CourierShipmentStatus.InTransit, CourierShipmentStatus.OutForDelivery, true)]
    [InlineData(CourierShipmentStatus.OutForDelivery, CourierShipmentStatus.Delivered, true)]
    [InlineData(CourierShipmentStatus.OutForDelivery, CourierShipmentStatus.Failed, true)]
    [InlineData(CourierShipmentStatus.Created, CourierShipmentStatus.Delivered, false)]
    [InlineData(CourierShipmentStatus.InTransit, CourierShipmentStatus.PickedUp, false)]
    [InlineData(CourierShipmentStatus.Delivered, CourierShipmentStatus.Failed, false)]
    public void Status_machine_allows_only_the_documented_transitions(
        CourierShipmentStatus from, CourierShipmentStatus to, bool allowed)
    {
        Assert.Equal(allowed, CourierShipment.IsAllowedTransition(from, to));
    }

    [Fact]
    public void Invalid_transition_throws_and_leaves_the_status_unchanged()
    {
        var shipment = NewShipment();

        var exception = Assert.Throws<InvalidShipmentStatusTransitionException>(() =>
            shipment.ChangeStatus(CourierShipmentStatus.Delivered, Now));

        Assert.Equal(CourierShipmentStatus.Created, shipment.Status);
        Assert.Contains("PickedUp", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("APX-1234567")]
    [InlineData("apx-12345678")]
    [InlineData("APX-123456789")]
    [InlineData("")]
    public void Tracking_numbers_must_match_APX_and_8_digits(string trackingNumber)
    {
        Assert.Throws<ArgumentException>(() =>
            CourierShipment.Create(trackingNumber, 1m, Recipient(), Now));
    }

    [Theory]
    [InlineData(0.09)]
    [InlineData(70.01)]
    public void Weight_must_be_between_0_1_and_70_kg(double weight)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CourierShipment.Create("APX-12345678", (decimal)weight, Recipient(), Now));
    }

    [Fact]
    public void Only_created_shipments_can_be_deleted_and_final_ones_cannot_be_edited()
    {
        var shipment = NewShipment();
        Assert.True(shipment.CanBeDeleted);

        shipment.ChangeStatus(CourierShipmentStatus.PickedUp, Now);
        shipment.ChangeStatus(CourierShipmentStatus.InTransit, Now);
        shipment.ChangeStatus(CourierShipmentStatus.OutForDelivery, Now);
        shipment.ChangeStatus(CourierShipmentStatus.Delivered, Now);

        Assert.False(shipment.CanBeDeleted);
        Assert.Throws<ShipmentRuleViolationException>(() => shipment.ReplaceDetails(2m, Recipient(), Now));
    }

    [Fact]
    public void Scans_advance_the_status_repeat_safely_and_never_go_backwards()
    {
        var shipment = NewShipment();

        Assert.Null(shipment.ApplyScan(CourierScanType.Pickup, Now));
        Assert.Null(shipment.ApplyScan(CourierScanType.Pickup, Now));
        Assert.Null(shipment.ApplyScan(CourierScanType.InTransit, Now));
        Assert.NotNull(shipment.ApplyScan(CourierScanType.Pickup, Now));
        Assert.NotNull(shipment.ApplyScan(CourierScanType.Delivered, Now));
        Assert.Equal(CourierShipmentStatus.InTransit, shipment.Status);
    }

    [Fact]
    public void Recipient_requires_every_field()
    {
        Assert.Throws<ArgumentException>(() => new Recipient("Jordan", "", "1 Road", "Town"));
        Assert.Throws<ArgumentException>(() => new Recipient("Jordan", "+15550100123", "1 Road", " "));
    }

    internal static Recipient Recipient() => new("Jordan Example", "+15550100123", "12 Elm Row", "Maplewood");

    private static CourierShipment NewShipment() => CourierShipment.Create("APX-12345678", 2.5m, Recipient(), Now);
}
