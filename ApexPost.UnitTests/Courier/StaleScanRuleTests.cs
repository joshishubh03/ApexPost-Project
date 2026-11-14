using ApexPost.Application.Courier;
using ApexPost.Domain.Courier;
using ApexPost.UnitTests.TestDoubles;

namespace ApexPost.UnitTests.Courier;

/// <summary>
/// Week 5 TDD rule (see TDD-LOG.md): "A scan taken more than 7 days before it reaches the server is rejected as
/// stale. Exactly 7 days is still accepted. The rest of the upload batch is unaffected."
/// </summary>
public sealed class StaleScanRuleTests
{
    private static readonly DateTimeOffset ReceivedAt = FixedClock.DefaultNow;

    [Theory]
    [InlineData(0)]                          // scanned just now
    [InlineData(60)]                         // a minute ago
    [InlineData(7 * 86_400 - 1)]             // one second inside the limit
    [InlineData(7 * 86_400)]                 // exactly 7 days: still accepted
    public void CheckScanAge_ScanAtMostSevenDaysOld_IsAccepted(int secondsOld)
    {
        // Arrange
        var scannedAt = ReceivedAt.AddSeconds(-secondsOld);

        // Act
        var reason = CourierShipment.CheckScanAge(scannedAt, ReceivedAt);

        // Assert
        Assert.Null(reason);
    }

    [Theory]
    [InlineData(7 * 86_400 + 1)]             // one second over the limit
    [InlineData(8 * 86_400)]
    [InlineData(30 * 86_400)]
    public void CheckScanAge_ScanOlderThanSevenDays_IsRejectedWithAReason(int secondsOld)
    {
        // Arrange
        var scannedAt = ReceivedAt.AddSeconds(-secondsOld);

        // Act
        var reason = CourierShipment.CheckScanAge(scannedAt, ReceivedAt);

        // Assert
        Assert.NotNull(reason);
        Assert.Contains("older than 7 days", reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UploadScansAsync_ScanOlderThanSevenDays_IsRejectedAndTheRestOfTheBatchIsAccepted()
    {
        // Arrange
        var repository = new InMemoryCourierShipmentRepository();
        var service = new CourierShipmentService(repository, ImmediateUnitOfWork.Instance, new FixedClock(ReceivedAt));
        await service.CreateAsync(new CreateCourierShipmentCommand("APX-70000001", 2.5m, CourierShipmentDomainTests.Recipient()));
        var stale = new ScanUploadItem(Guid.NewGuid(), CourierScanType.Pickup, "courier-007", "Depot", ReceivedAt.AddDays(-8), null, null);
        var fresh = new ScanUploadItem(Guid.NewGuid(), CourierScanType.Pickup, "courier-007", "Depot", ReceivedAt.AddMinutes(-5), null, null);

        // Act
        var summary = await service.UploadScansAsync("APX-70000001", [stale, fresh]);

        // Assert
        Assert.Equal(1, summary.Accepted);
        var rejected = Assert.Single(summary.Rejected);
        Assert.Equal(stale.ScanId, rejected.ScanId);
        Assert.Contains("older than 7 days", rejected.Reason, StringComparison.Ordinal);
        Assert.Equal(fresh.ScanId, Assert.Single(await service.GetScanHistoryAsync("APX-70000001")).Id);
    }

    [Fact]
    public async Task UploadScansAsync_OnlyAStaleScan_LeavesTheShipmentStatusUnchanged()
    {
        // Arrange
        var repository = new InMemoryCourierShipmentRepository();
        var service = new CourierShipmentService(repository, ImmediateUnitOfWork.Instance, new FixedClock(ReceivedAt));
        await service.CreateAsync(new CreateCourierShipmentCommand("APX-70000002", 2.5m, CourierShipmentDomainTests.Recipient()));
        var stale = new ScanUploadItem(Guid.NewGuid(), CourierScanType.Pickup, "courier-007", "Depot", ReceivedAt.AddDays(-10), null, null);

        // Act
        var summary = await service.UploadScansAsync("APX-70000002", [stale]);

        // Assert
        Assert.Equal(0, summary.Accepted);
        Assert.Single(summary.Rejected);
        Assert.Equal(CourierShipmentStatus.Created, (await service.GetAsync("APX-70000002")).Status);
        Assert.Empty(await service.GetScanHistoryAsync("APX-70000002"));
    }
}
