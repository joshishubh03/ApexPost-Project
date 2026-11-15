using ApexPost.Courier.Offline.Abstractions;
using ApexPost.Courier.Offline.Domain;
using ApexPost.Courier.Offline.Outbox;
using ApexPost.UnitTests.TestDoubles;
using Moq;

namespace ApexPost.UnitTests.CourierOffline;

/// <summary>
/// Week 5 isolation tests for the device outbox (risk R7) with Moq mocks of <see cref="IScanRepository"/> and
/// <see cref="IShipmentUploader"/>. They prove the outbox's decisions (Synced / Failed / stay Pending, when to
/// stop) without Couchbase Lite or a network.
/// <para>
/// What they do not prove: that Couchbase stores or queries the scans correctly (see
/// <c>IntegrationTests/CourierOffline</c>), or that the real API answers as the mock does (see the API tests).
/// </para>
/// </summary>
public sealed class OutboxServiceMockTests
{
    private static readonly DateTimeOffset Now = FixedClock.DefaultNow;

    private readonly Mock<IScanRepository> repository = new();
    private readonly Mock<IShipmentUploader> uploader = new();
    private readonly OutboxService outbox;

    public OutboxServiceMockTests()
    {
        repository
            .Setup(repo => repo.UpdateAsync(It.IsAny<ShipmentScan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShipmentScan scan, CancellationToken _) => scan);
        outbox = new OutboxService(repository.Object, uploader.Object, new FixedClock(Now));
    }

    [Fact]
    public async Task RecordScanAsync_NoScanTimeGiven_SavesAPendingScanStampedWithTheClock()
    {
        // Arrange
        repository
            .Setup(repo => repo.CreateAsync(It.IsAny<ShipmentScan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShipmentScan scan, CancellationToken _) => scan);

        // Act
        var saved = await outbox.RecordScanAsync("apx-10000001", ScanType.Pickup, "courier-007", "Depot");

        // Assert
        Assert.Equal(SyncStatus.Pending, saved.SyncStatus);
        repository.Verify(repo => repo.CreateAsync(It.Is<ShipmentScan>(scan =>
                scan.TrackingNumber == "APX-10000001" &&
                scan.SyncStatus == SyncStatus.Pending &&
                scan.ScannedAtUtc == Now &&
                scan.Id.StartsWith(ShipmentScanId.Prefix, StringComparison.Ordinal)),
            It.IsAny<CancellationToken>()), Times.Once);
        uploader.Verify(up => up.UploadAsync(It.IsAny<IReadOnlyList<ShipmentScan>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SyncPendingAsync_NoPendingScans_NeverCallsTheUploader()
    {
        // Arrange
        repository.Setup(repo => repo.ListPendingAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>())).ReturnsAsync(Batch());

        // Act
        var result = await outbox.SyncPendingAsync();

        // Assert
        Assert.Equal(0, result.Attempted);
        Assert.False(result.StoppedEarly);
        uploader.Verify(up => up.UploadAsync(It.IsAny<IReadOnlyList<ShipmentScan>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SyncPendingAsync_DefaultRun_AsksTheRepositoryForBatchesOfTwenty()
    {
        // Arrange
        repository.Setup(repo => repo.ListPendingAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>())).ReturnsAsync(Batch());

        // Act
        await outbox.SyncPendingAsync();

        // Assert
        repository.Verify(repo => repo.ListPendingAsync(It.Is<int?>(limit => limit == 20), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SyncPendingAsync_AcceptedAndDuplicateOutcomes_MarkBothScansSynced()
    {
        // Arrange
        var accepted = PendingScan("APX-10000001");
        var duplicate = PendingScan("APX-10000001");
        repository.SetupSequence(repo => repo.ListPendingAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Batch(accepted, duplicate))
            .ReturnsAsync(Batch());
        uploader.Setup(up => up.UploadAsync(It.IsAny<IReadOnlyList<ShipmentScan>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ScanUploadOutcome.Accepted(accepted.Id), ScanUploadOutcome.Duplicate(duplicate.Id) });

        // Act
        var result = await outbox.SyncPendingAsync();

        // Assert
        Assert.Equal(2, result.Synced);
        Assert.Equal(0, result.Failed);
        repository.Verify(repo => repo.UpdateAsync(It.Is<ShipmentScan>(scan =>
                scan.SyncStatus == SyncStatus.Synced && scan.LastSyncAttemptUtc == Now && scan.SyncAttempts == 1),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task SyncPendingAsync_RejectedOutcome_MarksTheScanFailedWithTheServerReason()
    {
        // Arrange
        var scan = PendingScan("APX-10000001");
        repository.SetupSequence(repo => repo.ListPendingAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Batch(scan))
            .ReturnsAsync(Batch());
        uploader.Setup(up => up.UploadAsync(It.Is<IReadOnlyList<ShipmentScan>>(batch => batch.Count == 1 && batch[0].Id == scan.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ScanUploadOutcome.Rejected(scan.Id, "Invalid status transition Created -> Delivered") });

        // Act
        var result = await outbox.SyncPendingAsync();

        // Assert
        Assert.Equal(1, result.Failed);
        repository.Verify(repo => repo.UpdateAsync(It.Is<ShipmentScan>(updated =>
                updated.Id == scan.Id &&
                updated.SyncStatus == SyncStatus.Failed &&
                updated.LastSyncError == "Invalid status transition Created -> Delivered"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SyncPendingAsync_ServerUnavailable_KeepsEveryScanPendingAndStopsTheRun()
    {
        // Arrange
        var first = PendingScan("APX-10000001");
        var second = PendingScan("APX-10000002");
        repository.Setup(repo => repo.ListPendingAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>())).ReturnsAsync(Batch(first, second));
        uploader.Setup(up => up.UploadAsync(It.IsAny<IReadOnlyList<ShipmentScan>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ShipmentUploadUnavailableException("Server unavailable"));

        // Act
        var result = await outbox.SyncPendingAsync();

        // Assert
        Assert.True(result.StoppedEarly);
        Assert.Equal("Server unavailable", result.Error);
        repository.Verify(repo => repo.UpdateAsync(It.Is<ShipmentScan>(scan =>
                scan.SyncStatus == SyncStatus.Pending && scan.SyncAttempts == 1 && scan.LastSyncError == "Server unavailable"),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
        repository.Verify(repo => repo.ListPendingAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SyncPendingAsync_ServerReturnsNoResults_StopsInsteadOfResendingForever()
    {
        // Arrange: the same pending scan would come back on every query.
        var scan = PendingScan("APX-10000001");
        repository.Setup(repo => repo.ListPendingAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>())).ReturnsAsync(Batch(scan));
        uploader.Setup(up => up.UploadAsync(It.IsAny<IReadOnlyList<ShipmentScan>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ScanUploadOutcome>());

        // Act
        var result = await outbox.SyncPendingAsync();

        // Assert
        Assert.True(result.StoppedEarly);
        uploader.Verify(up => up.UploadAsync(It.IsAny<IReadOnlyList<ShipmentScan>>(), It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(repo => repo.UpdateAsync(It.Is<ShipmentScan>(updated =>
            updated.SyncStatus == SyncStatus.Pending), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SyncPendingAsync_BatchSizeBelowOne_ThrowsArgumentOutOfRange(int batchSize)
    {
        // Act + Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => outbox.SyncPendingAsync(batchSize));
        repository.Verify(repo => repo.ListPendingAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static ShipmentScan PendingScan(string trackingNumber) =>
        ShipmentScan.CreatePending(trackingNumber, ScanType.Pickup, "courier-007", "Depot", Now.AddMinutes(-30));

    private static IReadOnlyList<ShipmentScan> Batch(params ShipmentScan[] scans) => scans;
}
