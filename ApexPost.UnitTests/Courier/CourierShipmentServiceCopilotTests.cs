// Copilot-assisted: drafted with GitHub Copilot, reviewed and corrected by hand. See COPILOT-LOG.md.
using ApexPost.Application.Abstractions;
using ApexPost.Application.Courier;
using ApexPost.Application.Exceptions;
using ApexPost.Domain.Courier;
using ApexPost.UnitTests.TestDoubles;
using Moq;

namespace ApexPost.UnitTests.Courier;

/// <summary>
/// <see cref="CourierShipmentService.ReplaceAsync"/> and <see cref="CourierShipmentService.ListAsync"/> with Moq.
/// Drafted by GitHub Copilot; the review decisions for every test are in COPILOT-LOG.md, section 1.3.
/// </summary>
public sealed class CourierShipmentServiceCopilotTests
{
    private const string TrackingNumber = "APX-10000001";
    private static readonly DateTimeOffset Now = FixedClock.DefaultNow;
    private static readonly Recipient Original = new("Jordan Example", "+15550100123", "12 Elm Row", "Maplewood");
    private static readonly Recipient Changed = new("Sam Example", "+15550100999", "4 River Lane", "Riverton");

    private readonly Mock<ICourierShipmentRepository> repository = new();
    private readonly Mock<IUnitOfWork> unitOfWork = new();
    private readonly CourierShipmentService service;

    public CourierShipmentServiceCopilotTests()
    {
        unitOfWork
            .Setup(work => work.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task<bool>>>(),
                It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task<bool>> operation, CancellationToken token) => operation(token));

        service = new CourierShipmentService(repository.Object, unitOfWork.Object, new FixedClock(Now));
    }

    [Fact]
    public async Task ReplaceAsync_ValidChange_UpdatesWeightRecipientAndUpdatedAt()
    {
        // Arrange: a different weight AND a different recipient, so the test proves both are replaced.
        var command = new ReplaceCourierShipmentCommand(3.5m, Changed);
        repository
            .Setup(repo => repo.FindByTrackingNumberAsync(TrackingNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShipmentIn(CourierShipmentStatus.Created));
        repository
            .Setup(repo => repo.GetDetailsAsync(TrackingNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Details());

        // Act
        await service.ReplaceAsync(TrackingNumber, command);

        // Assert: what the service staged, not what the mock returns.
        repository.Verify(repo => repo.Update(It.Is<CourierShipment>(shipment =>
            shipment.TrackingNumber == TrackingNumber &&
            shipment.WeightKilograms == 3.5m &&
            shipment.Recipient.Name == "Sam Example" &&
            shipment.Recipient.City == "Riverton" &&
            shipment.UpdatedAtUtc == Now)), Times.Once);
        unitOfWork.Verify(work => work.ExecuteInTransactionAsync(
            It.IsAny<Func<CancellationToken, Task<bool>>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReplaceAsync_DeliveredShipment_ThrowsAndDoesNotUpdate()
    {
        // Arrange
        repository
            .Setup(repo => repo.FindByTrackingNumberAsync(TrackingNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShipmentIn(CourierShipmentStatus.Delivered));

        // Act
        var exception = await Assert.ThrowsAsync<ShipmentRuleViolationException>(() =>
            service.ReplaceAsync(TrackingNumber, new ReplaceCourierShipmentCommand(3.5m, Changed)));

        // Assert
        Assert.Contains("can no longer be edited", exception.Message, StringComparison.Ordinal);
        repository.Verify(repo => repo.Update(It.IsAny<CourierShipment>()), Times.Never);
    }

    [Fact]
    public async Task ReplaceAsync_UnknownTrackingNumber_ThrowsShipmentNotFound()
    {
        // Arrange
        repository
            .Setup(repo => repo.FindByTrackingNumberAsync(TrackingNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CourierShipment?)null);

        // Act
        await Assert.ThrowsAsync<ShipmentNotFoundException>(() =>
            service.ReplaceAsync(TrackingNumber, new ReplaceCourierShipmentCommand(2.0m, Original)));

        // Assert
        repository.Verify(repo => repo.Update(It.IsAny<CourierShipment>()), Times.Never);
    }

    [Fact]
    public async Task ReplaceAsync_LowerCaseTrackingNumber_IsNormalizedBeforeLookup()
    {
        // Arrange
        repository
            .Setup(repo => repo.FindByTrackingNumberAsync(TrackingNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShipmentIn(CourierShipmentStatus.Created));
        repository
            .Setup(repo => repo.GetDetailsAsync(TrackingNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Details());

        // Act
        await service.ReplaceAsync("  apx-10000001  ", new ReplaceCourierShipmentCommand(3.5m, Changed));

        // Assert
        repository.Verify(repo => repo.FindByTrackingNumberAsync(TrackingNumber, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(repo => repo.GetDetailsAsync(TrackingNumber, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(1)]     // smallest valid page size
    [InlineData(50)]    // largest valid page size
    public async Task ListAsync_PageSizeInsideTheLimits_PassesTheQueryToTheRepository(int pageSize)
    {
        // Arrange
        repository
            .Setup(repo => repo.ListAsync(It.IsAny<CourierShipmentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<CourierShipmentDetails>([], 1, pageSize, 0));

        // Act
        await service.ListAsync(new CourierShipmentQuery(1, pageSize, null, null));

        // Assert: the service let the query through unchanged.
        repository.Verify(repo => repo.ListAsync(
            It.Is<CourierShipmentQuery>(q => q.Page == 1 && q.PageSize == pageSize),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(1, 51, "PageSize")]   // one above the maximum of 50
    [InlineData(1, 0, "PageSize")]    // below the minimum of 1
    [InlineData(0, 20, "Page")]       // page numbers start at 1
    public async Task ListAsync_PageOrPageSizeOutOfRange_ThrowsAndNeverQueries(int page, int pageSize, string parameter)
    {
        // Arrange
        var query = new CourierShipmentQuery(page, pageSize, null, null);

        // Act
        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.ListAsync(query));

        // Assert
        Assert.Equal(parameter, exception.ParamName);
        repository.Verify(repo => repo.ListAsync(It.IsAny<CourierShipmentQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ListAsync_CityFilterTrimsWhitespaceBeforeRepositoryCall()
    {
        // Arrange
        repository
            .Setup(repo => repo.ListAsync(It.IsAny<CourierShipmentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<CourierShipmentDetails>([], 1, 20, 0));

        // Act
        await service.ListAsync(new CourierShipmentQuery(1, 20, null, "  Maplewood  "));

        // Assert
        repository.Verify(repo => repo.ListAsync(
            It.Is<CourierShipmentQuery>(q => q.City == "Maplewood"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListAsync_NullQuery_ThrowsArgumentNullExceptionAndNeverQueries()
    {
        // Act
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.ListAsync(null!));

        // Assert
        repository.VerifyNoOtherCalls();
    }

    private static CourierShipment ShipmentIn(CourierShipmentStatus status) =>
        new(Guid.NewGuid(), TrackingNumber, 2.5m, Original, status, Now.AddDays(-1), Now.AddDays(-1));

    private static CourierShipmentDetails Details() =>
        new(TrackingNumber, CourierShipmentStatus.Created, 3.5m, Changed, Now.AddDays(-1), Now, null);
}
