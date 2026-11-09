using System.Net;
using ApexPost.Courier.Core.Api;
using ApexPost.Courier.Offline.Abstractions;
using ApexPost.Courier.Offline.Domain;
using Microsoft.Extensions.Logging;

namespace ApexPost.Courier.Core.Sync;

/// <summary>
/// Part D replacement for Part A's FakeShipmentUploader: sends pending scans to POST /api/v2/shipments/{tn}/scans.
/// <list type="bullet">
/// <item>Accepted and duplicate scans -> Synced (the server de-duplicates by scan ID, so a retry is harmless).</item>
/// <item>Scans the server rejects (status machine, 400 validation, 403 foreign courier, 404 unknown parcel) -> Failed with the reason.</item>
/// <item>Network error, timeout, 5xx, open circuit or 401 -> <see cref="ShipmentUploadUnavailableException"/>: the batch stays Pending.</item>
/// </list>
/// </summary>
/// <remarks>
/// The typed client is resolved per upload through <paramref name="apiFactory"/>: the outbox is a long-lived singleton
/// and must not capture one HttpClient forever (IHttpClientFactory rotates the handlers).
/// </remarks>
public sealed class ApiShipmentUploader(Func<ApexApiClient> apiFactory, ILogger<ApiShipmentUploader> logger) : IShipmentUploader
{
    public async Task<IReadOnlyList<ScanUploadOutcome>> UploadAsync(IReadOnlyList<ShipmentScan> scans, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scans);
        var outcomes = new List<ScanUploadOutcome>(scans.Count);
        var api = apiFactory();

        foreach (var parcel in scans.GroupBy(scan => scan.TrackingNumber, StringComparer.Ordinal))
        {
            var batch = parcel.ToList();
            var request = new ScanUploadRequestDto { Scans = batch.Select(ToDto).ToList() };

            try
            {
                var result = await api.UploadScansAsync(parcel.Key, request, cancellationToken);
                var rejected = (result.Rejected ?? [])
                    .Where(item => !string.IsNullOrWhiteSpace(item.ScanId))
                    .GroupBy(item => item.ScanId!, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => group.First().Reason ?? "Rejected by the server.", StringComparer.OrdinalIgnoreCase);

                foreach (var scan in batch)
                {
                    outcomes.Add(rejected.TryGetValue(scan.Id, out var reason)
                        ? ScanUploadOutcome.Rejected(scan.Id, reason)
                        : ScanUploadOutcome.Accepted(scan.Id));
                }

                logger.LogInformation("Uploaded {Count} scan(s) for {TrackingNumber}: {Accepted} accepted, {Duplicates} duplicates, {Rejected} rejected",
                    batch.Count, parcel.Key, result.Accepted, result.DuplicatesSkipped, rejected.Count);
            }
            catch (ApiUnavailableException exception)
            {
                throw new ShipmentUploadUnavailableException(exception.UserMessage, exception);
            }
            catch (ApiMalformedResponseException exception)
            {
                // We cannot tell what the server stored; keep the scans Pending (re-sending is idempotent).
                throw new ShipmentUploadUnavailableException(exception.UserMessage, exception);
            }
            catch (ApiProblemException exception) when (exception.StatusCode == HttpStatusCode.Unauthorized)
            {
                // Signed out: keep everything Pending until the courier logs in again.
                throw new ShipmentUploadUnavailableException(exception.UserMessage, exception);
            }
            catch (ApiProblemException exception)
            {
                logger.LogWarning("Server rejected {Count} scan(s) for {TrackingNumber} with {Status}", batch.Count, parcel.Key, (int)exception.StatusCode);
                outcomes.AddRange(batch.Select(scan => ScanUploadOutcome.Rejected(scan.Id, exception.UserMessage)));
            }
        }

        return outcomes;
    }

    private static ScanUploadItemDto ToDto(ShipmentScan scan) => new()
    {
        ScanId = scan.Id,
        ScanType = scan.ScanType.ToString(),
        CourierId = scan.CourierId,
        Location = scan.Location,
        ScannedAtUtc = scan.ScannedAtUtc,
        Notes = scan.Notes,
        SignatureName = scan.SignatureName
    };
}
