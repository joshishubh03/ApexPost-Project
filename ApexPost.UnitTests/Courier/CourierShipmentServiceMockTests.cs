using ApexPost.Application.Abstractions;
using ApexPost.Application.Courier;
using ApexPost.Application.Exceptions;
using ApexPost.Domain.Courier;
using ApexPost.UnitTests.TestDoubles;
using Moq;

namespace ApexPost.UnitTests.Courier;

/// <summary>
/// Week 5 isolation tests: <see cref="CourierShipmentService"/> with Moq mocks of
/// <see cref="ICourierShipmentRepository"/> and <see cref="IUnitOfWork"/>.
/// <para>
/// These prove <b>interactions</b>: what the service asks the repository, what it stages, and that each write runs
/// as exactly one unit of work (or never touches the repository when a rule fails). They do <b>not</b> prove that
/// the repository's SQL is right, that the unique index exists or that a commit really happens: a mock returns
/// whatever it is told. Those are proved by the SQL Server integration tests.
/// </para>
/// </summary>
public sealed class CourierShipmentServiceMockTests
{
    private const string TrackingNumber = "APX-10000001";
    private static readonly DateTimeOffset Now = FixedClock.DefaultNow;

    private readonly Mock<ICourierShipmentRepository> repository = new();
    private readonly Mock<IUnitOfWork> unitOfWork = new();
    private readonly CourierShipmentService service;

    public CourierShipmentServiceMockTests()
    {
        // The unit of work runs the use case straight away (as the real one does inside its transaction).
        // bool = use cases without a result (ExecuteUseCaseAsync(Func<Task>)); ScanUploadSummary = the scan upload.
        unitOfWork
            .Setup(work => work.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task<bool>>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task<bool>> operation, CancellationToken token) => operation(token));
        unitOfWork
            .Setup(work => work.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task<ScanUploadSummary>>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task<ScanUploadSummary>> operation, CancellationToken token) => operation(token));

        service = new CourierShipmentService(repository.Object, unitOfWork.Object, new FixedClock(Now));
    }

    [Fact]
    public async Task CreateAsync_NewTrackingNumber_AddsOneCreatedShipmentInOneUnitOfWork()
    {
        // Arrange
        repository.Setup(repo => repo.ExistsAsync(TrackingNumber, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        repository.Setup(repo => repo.GetDetailsAsync(TrackingNumber, It.IsAny<CancellationToken>())).ReturnsAsync(Details());

        // Act
        var created = await service.CreateAsync(Command(TrackingNumber));

        // Assert
        Assert.Equal(TrackingNumber, created.TrackingNumber);
        repository.Verify(repo => repo.Add(It.Is<CourierShipment>(shipment =>
                shipment.TrackingNumber == TrackingNumber &&
                shipment.Status == CourierShipmentStatus.Created &&
                shipment.WeightKilograms == 2.5m &&
                shipment.CreatedAtUtc == Now)),
            Times.Once);
        unitOfWork.Verify(work => work.ExecuteInTransactionAsync(
            It.IsAny<Func<CancellationToken, Task<bool>>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_LowerCaseTrackingNumber_IsNormalisedBeforeTheDuplicateCheck()
    {
        // Arrange
        repository.Setup(repo => repo.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        repository.Setup(repo => repo.GetDetailsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(Details());

        // Act
        await service.CreateAsync(Command("  apx-10000001 "));

        // Assert
        repository.Verify(repo => repo.ExistsAsync(TrackingNumber, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(repo => repo.ExistsAsync(It.Is<string>(value => value != TrackingNumber), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_DuplicateTrackingNumber_ThrowsAndNeverStagesAShipment()
    {
        // Arrange
        repository.Setup(repo => repo.ExistsAsync(TrackingNumber, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        await Assert.ThrowsAsync<DuplicateResourceException>(() => service.CreateAsync(Command(TrackingNumber)));

        // Assert
        repository.Verify(repo => repo.Add(It.IsAny<CourierShipment>()), Times.Never);
        repository.Verify(repo => repo.GetDetailsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChangeStatusAsync_TransitionNotAllowed_ThrowsAndNeverUpdates()
    {
        // Arrange
        var shipment = ShipmentIn(CourierShipmentStatus.Created);
        repository.Setup(repo => repo.FindByTrackingNumberAsync(TrackingNumber, It.IsAny<CancellationToken>())).ReturnsAsync(shipment);

        // Act
        await Assert.ThrowsAsync<InvalidShipmentStatusTransitionException>(() =>
            service.ChangeStatusAsync(TrackingNumber, CourierShipmentStatus.Delivered));

        // Assert
        repository.Verify(repo => repo.Update(It.IsAny<CourierShipment>()), Times.Never);
    }

    [Fact]
    public async Task ChangeStatusAsync_AllowedTransition_UpdatesTheShipmentOnceWithTheNewStatus()
    {
        // Arrange
        var shipment = ShipmentIn(CourierShipmentStatus.Created);
        repository.Setup(repo => repo.FindByTrackingNumberAsync(TrackingNumber, It.IsAny<CancellationToken>())).ReturnsAsync(shipment);
        repository.Setup(repo => repo.GetDetailsAsync(TrackingNumber, It.IsAny<CancellationToken>())).ReturnsAsync(Details());

        // Act
        await service.ChangeStatusAsync(TrackingNumber, CourierShipmentStatus.PickedUp);

        // Assert
        repository.Verify(repo => repo.Update(It.Is<CourierShipment>(updated =>
            updated.Status == CourierShipmentStatus.PickedUp && updated.UpdatedAtUtc == Now)), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_ShipmentAlreadyPickedUp_ThrowsAndNeverRemoves()
    {
        // Arrange
        repository.Setup(repo => repo.FindByTrackingNumberAsync(TrackingNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShipmentIn(CourierShipmentStatus.PickedUp));

        // Act
        await Assert.ThrowsAsync<ShipmentRuleViolationException>(() => service.DeleteAsync(TrackingNumber));

        // Assert
        repository.Verify(repo => repo.Remove(It.IsAny<CourierShipment>()), Times.Never);
    }

    [Fact]
    public async Task UploadScansAsync_ScanIdAlreadyStored_IsCountedAsDuplicateAndNotAddedAgain()
    {
        // Arrange
        var scanId = Guid.NewGuid();
        repository.Setup(repo => repo.FindByTrackingNumberAsync(TrackingNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShipmentIn(CourierShipmentStatus.Created));
        repository.Setup(repo => repo.FindExistingScanIdsAsync(
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(scanId)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid> { scanId });

        // Act
        var summary = await service.UploadScansAsync(TrackingNumber, [Scan(scanId, CourierScanType.Pickup)]);

        // Assert
        Assert.Equal(0, summary.Accepted);
        Assert.Equal(1, summary.DuplicatesSkipped);
        repository.Verify(repo => repo.AddScan(It.IsAny<CourierScan>()), Times.Never);
        repository.Verify(repo => repo.Update(It.IsAny<CourierShipment>()), Times.Never);
    }

    [Fact]
    public async Task UploadScansAsync_NewPickupScan_StoresItWithTheServerReceiveTimeAndAdvancesTheStatus()
    {
        // Arrange
        var scanId = Guid.NewGuid();
        repository.Setup(repo => repo.FindByTrackingNumberAsync(TrackingNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShipmentIn(CourierShipmentStatus.Created));
        repository.Setup(repo => repo.FindExistingScanIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid>());

        // Act
        var summary = await service.UploadScansAsync(TrackingNumber, [Scan(scanId, CourierScanType.Pickup)]);

        // Assert
        Assert.Equal(1, summary.Accepted);
        repository.Verify(repo => repo.AddScan(It.Is<CourierScan>(scan =>
            scan.Id == scanId &&
            scan.CourierId == "courier-007" &&
            scan.ReceivedAtUtc == Now)), Times.Once);
        repository.Verify(repo => repo.Update(It.Is<CourierShipment>(shipment =>
            shipment.Status == CourierShipmentStatus.PickedUp)), Times.Once);
    }

    [Fact]
    public async Task UploadScansAsync_UnknownTrackingNumber_ThrowsShipmentNotFoundAndLooksUpNoScans()
    {
        // Arrange
        repository.Setup(repo => repo.FindByTrackingNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CourierShipment?)null);

        // Act
        await Assert.ThrowsAsync<ShipmentNotFoundException>(() =>
            service.UploadScansAsync(TrackingNumber, [Scan(Guid.NewGuid(), CourierScanType.Pickup)]));

        // Assert
        repository.Verify(repo => repo.FindExistingScanIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadScansAsync_ScanUnderAnotherCourierId_IsRefusedBeforeAnyUnitOfWorkStarts()
    {
        // Arrange
        var foreignScan = Scan(Guid.NewGuid(), CourierScanType.Pickup) with { CourierId = "courier-999" };

        // Act
        await Assert.ThrowsAsync<ScanOwnershipException>(() =>
            service.UploadScansAsync(TrackingNumber, [foreignScan], authenticatedCourierId: "courier-007"));

        // Assert
        unitOfWork.Verify(work => work.ExecuteInTransactionAsync(
            It.IsAny<Func<CancellationToken, Task<ScanUploadSummary>>>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.VerifyNoOtherCalls();
    }

    private static CreateCourierShipmentCommand Command(string trackingNumber) =>
        new(trackingNumber, 2.5m, CourierShipmentDomainTests.Recipient());

    private static CourierShipment ShipmentIn(CourierShipmentStatus status) =>
        new(Guid.NewGuid(), TrackingNumber, 2.5m, CourierShipmentDomainTests.Recipient(), status, Now.AddDays(-1), Now.AddDays(-1));

    private static CourierShipmentDetails Details() =>
        new(TrackingNumber, CourierShipmentStatus.Created, 2.5m, CourierShipmentDomainTests.Recipient(), Now, Now, null);

    private static ScanUploadItem Scan(Guid id, CourierScanType type) =>
        new(id, type, "courier-007", "Maplewood Depot", Now.AddMinutes(-15), null, null);
}
