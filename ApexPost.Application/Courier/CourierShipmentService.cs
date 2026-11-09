using ApexPost.Application.Abstractions;
using ApexPost.Application.Exceptions;
using ApexPost.Domain.Courier;

namespace ApexPost.Application.Courier;

/// <summary>
/// Use cases behind the Week 4 shipments API. Every write is one unit of work (one transaction, one commit) and all
/// business rules live here or in <see cref="CourierShipment"/> - API controllers only bind, call and map.
/// </summary>
public sealed class CourierShipmentService(
    ICourierShipmentRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public Task<PagedResult<CourierShipmentDetails>> ListAsync(CourierShipmentQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfLessThan(query.Page, 1, nameof(query.Page));
        ArgumentOutOfRangeException.ThrowIfLessThan(query.PageSize, 1, nameof(query.PageSize));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(query.PageSize, CourierShipmentQuery.MaximumPageSize, nameof(query.PageSize));

        var city = string.IsNullOrWhiteSpace(query.City) ? null : query.City.Trim();
        return repository.ListAsync(query with { City = city }, cancellationToken);
    }

    public async Task<CourierShipmentDetails> GetAsync(string trackingNumber, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(trackingNumber);
        return await repository.GetDetailsAsync(normalized, cancellationToken)
            ?? throw new ShipmentNotFoundException(normalized);
    }

    public async Task<CourierShipmentDetails> CreateAsync(CreateCourierShipmentCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var trackingNumber = Normalize(command.TrackingNumber);

        await unitOfWork.ExecuteUseCaseAsync(async () =>
        {
            if (await repository.ExistsAsync(trackingNumber, cancellationToken))
            {
                throw new DuplicateResourceException($"A shipment with tracking number {trackingNumber} already exists.");
            }

            repository.Add(CourierShipment.Create(trackingNumber, command.WeightKilograms, command.Recipient, clock.GetUtcNow()));
        }, cancellationToken);

        return await GetAsync(trackingNumber, cancellationToken);
    }

    public async Task<CourierShipmentDetails> ReplaceAsync(
        string trackingNumber,
        ReplaceCourierShipmentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var normalized = Normalize(trackingNumber);

        await unitOfWork.ExecuteUseCaseAsync(async () =>
        {
            var shipment = await LoadAsync(normalized, cancellationToken);
            shipment.ReplaceDetails(command.WeightKilograms, command.Recipient, clock.GetUtcNow());
            repository.Update(shipment);
        }, cancellationToken);

        return await GetAsync(normalized, cancellationToken);
    }

    public async Task<CourierShipmentDetails> ChangeStatusAsync(
        string trackingNumber,
        CourierShipmentStatus newStatus,
        CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(trackingNumber);

        await unitOfWork.ExecuteUseCaseAsync(async () =>
        {
            var shipment = await LoadAsync(normalized, cancellationToken);
            shipment.ChangeStatus(newStatus, clock.GetUtcNow());
            repository.Update(shipment);
        }, cancellationToken);

        return await GetAsync(normalized, cancellationToken);
    }

    /// <summary>Deletes a shipment; only allowed while it is still Created (otherwise 409).</summary>
    public Task DeleteAsync(string trackingNumber, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(trackingNumber);
        return unitOfWork.ExecuteUseCaseAsync(async () =>
        {
            var shipment = await LoadAsync(normalized, cancellationToken);
            if (!shipment.CanBeDeleted)
            {
                throw new ShipmentRuleViolationException(
                    $"Shipment {normalized} is {shipment.Status}; only shipments in status Created can be deleted.");
            }

            repository.Remove(shipment);
        }, cancellationToken);
    }

    /// <summary>
    /// Idempotent bulk upload of offline scans. A scan ID that is already stored (or repeated in the same batch) is
    /// skipped, never duplicated. Scans are applied oldest first; a scan that would break the status machine is
    /// rejected with a reason while the rest of the batch is still accepted.
    /// </summary>
    /// <param name="authenticatedCourierId">
    /// The caller's courierId claim (Part D). When set, every scan must carry the same courier ID.
    /// </param>
    public async Task<ScanUploadSummary> UploadScansAsync(
        string trackingNumber,
        IReadOnlyList<ScanUploadItem> scans,
        string? authenticatedCourierId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scans);
        var normalized = Normalize(trackingNumber);

        if (authenticatedCourierId is not null &&
            scans.FirstOrDefault(scan => !string.Equals(scan.CourierId?.Trim(), authenticatedCourierId, StringComparison.Ordinal))
                is { } foreign)
        {
            throw new ScanOwnershipException(authenticatedCourierId, foreign.CourierId);
        }

        return await unitOfWork.ExecuteUseCaseAsync(async () =>
        {
            var shipment = await LoadAsync(normalized, cancellationToken);
            var alreadyStored = await repository.FindExistingScanIdsAsync(
                scans.Select(scan => scan.ScanId).Distinct().ToArray(), cancellationToken);

            var seenInBatch = new HashSet<Guid>();
            var accepted = 0;
            var duplicates = 0;
            var rejected = new List<RejectedScan>();
            var statusBefore = shipment.Status;
            var receivedAt = clock.GetUtcNow();

            foreach (var scan in scans.OrderBy(scan => scan.ScannedAtUtc).ThenBy(scan => scan.ScanId))
            {
                if (alreadyStored.Contains(scan.ScanId) || !seenInBatch.Add(scan.ScanId))
                {
                    duplicates++;
                    continue;
                }

                var reason = shipment.ApplyScan(scan.ScanType, receivedAt);
                if (reason is not null)
                {
                    rejected.Add(new RejectedScan(scan.ScanId, reason));
                    continue;
                }

                repository.AddScan(new CourierScan(
                    scan.ScanId,
                    shipment.Id,
                    scan.ScanType,
                    scan.CourierId.Trim(),
                    scan.Location.Trim(),
                    scan.ScannedAtUtc.ToUniversalTime(),
                    string.IsNullOrWhiteSpace(scan.Notes) ? null : scan.Notes.Trim(),
                    string.IsNullOrWhiteSpace(scan.SignatureName) ? null : scan.SignatureName.Trim(),
                    receivedAt));
                accepted++;
            }

            if (shipment.Status != statusBefore)
            {
                repository.Update(shipment);
            }

            return new ScanUploadSummary(accepted, duplicates, rejected);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<CourierScan>> GetScanHistoryAsync(string trackingNumber, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(trackingNumber);
        return await repository.GetScanHistoryAsync(normalized, cancellationToken)
            ?? throw new ShipmentNotFoundException(normalized);
    }

    private async Task<CourierShipment> LoadAsync(string trackingNumber, CancellationToken cancellationToken) =>
        await repository.FindByTrackingNumberAsync(trackingNumber, cancellationToken)
            ?? throw new ShipmentNotFoundException(trackingNumber);

    private static string Normalize(string trackingNumber) =>
        (trackingNumber ?? string.Empty).Trim().ToUpperInvariant();
}

/// <summary>A courier tried to upload scans under another courier's ID (HTTP 403).</summary>
public sealed class ScanOwnershipException(string authenticatedCourierId, string? scanCourierId)
    : Exception($"Courier {authenticatedCourierId} may only upload scans under its own courier ID (got '{scanCourierId}').")
{
    public string AuthenticatedCourierId { get; } = authenticatedCourierId;
}
