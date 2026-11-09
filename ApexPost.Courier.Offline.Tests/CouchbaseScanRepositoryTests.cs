using ApexPost.Courier.Offline.Domain;
using ApexPost.Courier.Offline.Storage;

namespace ApexPost.Courier.Offline.Tests;

public sealed class CouchbaseScanRepositoryTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);
    private readonly TemporaryCourierDatabase store = new();

    public void Dispose() => store.Dispose();

    [Fact]
    public void Database_uses_the_assignment_name_and_creates_the_value_indexes()
    {
        Assert.Equal("apexpost_courier", store.Database.Name);
        var indexes = store.Database.GetIndexNames();
        Assert.Contains(CourierDatabase.TrackingNumberIndex, indexes);
        Assert.Contains(CourierDatabase.SyncStatusIndex, indexes);
    }

    [Fact]
    public void New_scans_use_scan_guid_ids_and_start_pending()
    {
        var scan = Scan("APX-10000001", ScanType.Pickup, T0);

        Assert.StartsWith("scan::", scan.Id, StringComparison.Ordinal);
        Assert.True(ShipmentScanId.TryGetGuid(scan.Id, out var guid));
        Assert.NotEqual(Guid.Empty, guid);
        Assert.Equal(SyncStatus.Pending, scan.SyncStatus);
    }

    [Fact]
    public async Task Create_then_get_by_id_round_trips_every_field()
    {
        var scan = Scan("APX-10000001", ScanType.Delivered, T0, notes: "Left at reception", signature: "J. Doe");

        await store.Repository.CreateAsync(scan);
        var loaded = await store.Repository.GetByIdAsync(scan.Id);

        Assert.Equal(scan, loaded);
    }

    [Fact]
    public async Task Creating_the_same_id_twice_is_rejected()
    {
        var scan = Scan("APX-10000001", ScanType.Pickup, T0);
        await store.Repository.CreateAsync(scan);

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.Repository.CreateAsync(scan));
    }

    [Fact]
    public async Task Update_replaces_the_document_and_unknown_ids_throw()
    {
        var scan = await store.Repository.CreateAsync(Scan("APX-10000001", ScanType.Pickup, T0));

        await store.Repository.UpdateAsync(scan.MarkSynced(T0.AddMinutes(5)));
        var loaded = await store.Repository.GetByIdAsync(scan.Id);

        Assert.Equal(SyncStatus.Synced, loaded!.SyncStatus);
        Assert.Equal(1, loaded.SyncAttempts);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            store.Repository.UpdateAsync(Scan("APX-10000001", ScanType.Pickup, T0)));
    }

    [Fact]
    public async Task Delete_removes_the_document()
    {
        var scan = await store.Repository.CreateAsync(Scan("APX-10000001", ScanType.Pickup, T0));

        Assert.True(await store.Repository.DeleteAsync(scan.Id));
        Assert.Null(await store.Repository.GetByIdAsync(scan.Id));
        Assert.False(await store.Repository.DeleteAsync(scan.Id));
        Assert.Empty(await store.Repository.ListAllAsync());
    }

    [Fact]
    public async Task Find_by_tracking_number_returns_only_that_parcel_ordered_by_scan_time()
    {
        await store.Repository.CreateAsync(Scan("APX-10000001", ScanType.Delivered, T0.AddHours(3), signature: "J. Doe"));
        await store.Repository.CreateAsync(Scan("APX-10000001", ScanType.Pickup, T0));
        await store.Repository.CreateAsync(Scan("APX-10000002", ScanType.Pickup, T0.AddMinutes(10)));
        await store.Repository.CreateAsync(Scan("APX-10000001", ScanType.InTransit, T0.AddHours(1)));

        var scans = await store.Repository.FindByTrackingNumberAsync("apx-10000001");

        Assert.Equal(new[] { ScanType.Pickup, ScanType.InTransit, ScanType.Delivered }, scans.Select(scan => scan.ScanType));
        Assert.All(scans, scan => Assert.Equal("APX-10000001", scan.TrackingNumber));
    }

    [Fact]
    public async Task Query_values_are_parameters_so_injection_text_matches_nothing()
    {
        await store.Repository.CreateAsync(Scan("APX-10000001", ScanType.Pickup, T0));

        var scans = await store.Repository.FindByTrackingNumberAsync("APX-10000001' OR '1'='1");

        Assert.Empty(scans);
        Assert.DoesNotContain("APX-", CouchbaseScanRepository.ByTrackingNumberQuery, StringComparison.Ordinal);
        Assert.Contains("$trackingNumber", CouchbaseScanRepository.ByTrackingNumberQuery, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pending_query_and_count_ignore_synced_and_failed_scans()
    {
        var pending1 = await store.Repository.CreateAsync(Scan("APX-10000001", ScanType.Pickup, T0.AddMinutes(2)));
        var pending2 = await store.Repository.CreateAsync(Scan("APX-10000002", ScanType.Pickup, T0.AddMinutes(1)));
        var synced = await store.Repository.CreateAsync(Scan("APX-10000003", ScanType.Pickup, T0));
        var failed = await store.Repository.CreateAsync(Scan("APX-10000004", ScanType.Pickup, T0));
        await store.Repository.UpdateAsync(synced.MarkSynced(T0));
        await store.Repository.UpdateAsync(failed.MarkFailed(T0, "rejected"));

        var pending = await store.Repository.ListPendingAsync();

        Assert.Equal(new[] { pending2.Id, pending1.Id }, pending.Select(scan => scan.Id));
        Assert.Equal(2, await store.Repository.CountPendingAsync());
        Assert.Single(await store.Repository.ListPendingAsync(limit: 1));
    }

    [Fact]
    public async Task Counts_scans_per_scan_type_for_one_courier()
    {
        await store.Repository.CreateAsync(Scan("APX-10000001", ScanType.Pickup, T0, courier: "courier-007"));
        await store.Repository.CreateAsync(Scan("APX-10000002", ScanType.Pickup, T0, courier: "courier-007"));
        await store.Repository.CreateAsync(Scan("APX-10000001", ScanType.InTransit, T0, courier: "courier-007"));
        await store.Repository.CreateAsync(Scan("APX-10000003", ScanType.Pickup, T0, courier: "courier-011"));

        var counts = await store.Repository.CountByScanTypeAsync("courier-007");

        Assert.Equal(2, counts.Single(count => count.ScanType == ScanType.Pickup).Count);
        Assert.Equal(1, counts.Single(count => count.ScanType == ScanType.InTransit).Count);
        Assert.DoesNotContain(counts, count => count.ScanType == ScanType.Delivered);
    }

    [Theory]
    [InlineData("APX-1234567")]
    [InlineData("ABC-12345678")]
    [InlineData("APX-12345678X")]
    public void Invalid_tracking_numbers_are_rejected_before_saving(string trackingNumber)
    {
        Assert.Throws<ArgumentException>(() => Scan(trackingNumber, ScanType.Pickup, T0));
    }

    internal static ShipmentScan Scan(
        string trackingNumber,
        ScanType scanType,
        DateTimeOffset at,
        string courier = "courier-007",
        string? notes = null,
        string? signature = null) =>
        ShipmentScan.CreatePending(trackingNumber, scanType, courier, "Maplewood Depot", at, notes, signature);
}
