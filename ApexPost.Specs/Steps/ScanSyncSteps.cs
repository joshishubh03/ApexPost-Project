using ApexPost.Application.Courier;
using ApexPost.Courier.Offline.Domain;
using ApexPost.Domain.Courier;
using ApexPost.Specs.Support;

namespace ApexPost.Specs.Steps;

/// <summary>Steps for ScanSync.feature. State lives in <see cref="CourierSyncContext"/> (context injection).</summary>
[Binding]
public sealed class ScanSyncSteps(CourierSyncContext world)
{
    [Given(@"^shipment ""(.*)"" has been registered$")]
    public async Task GivenShipmentHasBeenRegistered(string trackingNumber) =>
        await world.Server.CreateAsync(new CreateCourierShipmentCommand(
            trackingNumber, 2.5m, new Recipient("Jordan Example", "+15550100123", "12 Elm Row", "Maplewood")));

    [Given(@"^courier ""(.*)"" is working without a network connection$")]
    public void GivenCourierIsOffline(string courierId)
    {
        world.CourierId = courierId;
        world.Uploader.IsOnline = false;
    }

    [When(@"^the courier records an? ""(.*)"" scan for ""(.*)"" at ""(.*)""$")]
    public async Task WhenTheCourierRecordsAScan(string scanType, string trackingNumber, string location) =>
        await world.Outbox.RecordScanAsync(trackingNumber, Enum.Parse<ScanType>(scanType), world.CourierId, location);

    [Given(@"^the courier's ""(.*)"" scan for ""(.*)"" reached the server but the phone never got the answer$")]
    public async Task GivenTheScanReachedTheServerButTheAnswerWasLost(string scanType, string trackingNumber)
    {
        // The phone saved the scan and sent it; the server stored it; the response was lost on the way back.
        var scan = await world.Outbox.RecordScanAsync(trackingNumber, Enum.Parse<ScanType>(scanType), world.CourierId, "Maplewood Depot");
        await world.Server.UploadScansAsync(trackingNumber, [InProcessShipmentUploader.ToUploadItem(scan)]);
    }

    [When(@"^the connection comes back and the phone syncs$")]
    public async Task WhenTheConnectionComesBackAndThePhoneSyncs()
    {
        world.Uploader.IsOnline = true;
        world.LastSync = await world.Outbox.SyncPendingAsync();
    }

    [When(@"^the phone tries to sync while still offline$")]
    public async Task WhenThePhoneTriesToSyncWhileOffline() =>
        world.LastSync = await world.Outbox.SyncPendingAsync();

    [Then(@"^(\d+) scans? (?:is|are) waiting to be synced on the phone$")]
    public async Task ThenScansAreWaitingOnThePhone(int expected) =>
        Assert.Equal(expected, await world.PhoneScans.CountPendingAsync());

    [Then(@"^no scans are waiting to be synced on the phone$")]
    public async Task ThenNoScansAreWaiting() =>
        Assert.Equal(0, await world.PhoneScans.CountPendingAsync());

    [Then(@"^the server holds (\d+) scans? for ""(.*)""$")]
    public async Task ThenTheServerHoldsScans(int expected, string trackingNumber) =>
        Assert.Equal(expected, (await world.Server.GetScanHistoryAsync(trackingNumber)).Count);

    [Then(@"^shipment ""(.*)"" is (?:now|still) ""(.*)""$")]
    public async Task ThenShipmentIsInStatus(string trackingNumber, string status) =>
        Assert.Equal(Enum.Parse<CourierShipmentStatus>(status), (await world.Server.GetAsync(trackingNumber)).Status);

    [Then(@"^the server reports (\d+) duplicate scans? skipped$")]
    public void ThenTheServerReportsDuplicatesSkipped(int expected) =>
        Assert.Equal(expected, world.Uploader.DuplicatesSkipped);

    [Then(@"^the phone marks the scan as failed because ""(.*)""$")]
    public async Task ThenThePhoneMarksTheScanAsFailed(string reason)
    {
        var scan = Assert.Single(await world.PhoneScans.ListAllAsync());
        Assert.Equal(SyncStatus.Failed, scan.SyncStatus);
        Assert.Contains(reason, scan.LastSyncError, StringComparison.Ordinal);
        Assert.Equal(1, world.LastSync?.Failed);
    }
}
