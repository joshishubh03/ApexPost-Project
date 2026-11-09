using System.Globalization;
using ApexPost.Courier.Offline.Domain;
using ApexPost.Courier.Offline.Monitoring;
using ApexPost.Courier.Offline.Outbox;
using ApexPost.Courier.Offline.Storage;
using Microsoft.Extensions.Logging;

// Week 4 Part A console harness. Works with no network at all: everything is stored in the embedded
// Couchbase Lite database "apexpost_courier". Fictional data only.
var dataDirectory = args.Length >= 2 && args[0] == "--data"
    ? Path.GetFullPath(args[1])
    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ApexPost", "courier-harness");

using var loggerFactory = LoggerFactory.Create(logging => logging
    .AddSimpleConsole(options =>
    {
        options.SingleLine = true;
        options.TimestampFormat = "HH:mm:ss ";
    })
    .SetMinimumLevel(LogLevel.Information));

using var database = new CourierDatabase(dataDirectory, CourierDatabase.DefaultName, loggerFactory.CreateLogger<CourierDatabase>());
var repository = new CouchbaseScanRepository(database);
var uploader = new FakeShipmentUploader();
var outbox = new OutboxService(repository, uploader, TimeProvider.System, loggerFactory.CreateLogger<OutboxService>());
using var monitor = new PendingScanMonitor(database, repository, loggerFactory.CreateLogger<PendingScanMonitor>());
monitor.PendingCountChanged += (_, change) =>
    Console.WriteLine($"   >> live pending scans: {change.PendingCount}");

Console.WriteLine($"ApexPost courier offline harness - database '{database.Name}' in {database.Directory}");
Console.WriteLine($"Value indexes: {string.Join(", ", database.GetIndexNames())}");

while (true)
{
    Console.WriteLine();
    Console.WriteLine($"Pending scans: {monitor.PendingCount} | fake server: {(uploader.IsOnline ? "online" : "OFFLINE")}");
    Console.WriteLine("1) Add Scan   2) Find by Tracking Number   3) List Pending   4) Simulate Sync   5) Delete Scan   6) Exit");
    Console.WriteLine("7) Scans per type (one courier)   8) Toggle fake server online/offline   9) Seed fictional demo scans   10) Show query plans");
    var choice = Prompt("Choose");

    try
    {
        switch (choice)
        {
            case "1":
                await AddScanAsync();
                break;
            case "2":
                PrintScans(await repository.FindByTrackingNumberAsync(Prompt("Tracking number (APX-12345678)")));
                break;
            case "3":
                PrintScans(await repository.ListPendingAsync());
                break;
            case "4":
                var result = await outbox.SyncPendingAsync();
                Console.WriteLine($"Sync: attempted {result.Attempted}, synced {result.Synced}, failed {result.Failed}" +
                                  (result.StoppedEarly ? $", stopped early: {result.Error}" : string.Empty));
                break;
            case "5":
                var id = Prompt("Scan ID (scan::...)");
                Console.WriteLine(await repository.DeleteAsync(id) ? $"Deleted {id}." : $"No scan with ID {id}.");
                break;
            case "6":
                Console.WriteLine("Closing the database (listener tokens are removed first).");
                return;
            case "7":
                var courierId = Prompt("Courier ID");
                foreach (var count in await repository.CountByScanTypeAsync(courierId))
                {
                    Console.WriteLine($"   {count.ScanType,-10} {count.Count,5}");
                }
                break;
            case "8":
                uploader.IsOnline = !uploader.IsOnline;
                Console.WriteLine($"Fake server is now {(uploader.IsOnline ? "online" : "offline")}.");
                break;
            case "9":
                await SeedAsync();
                break;
            case "10":
                Console.WriteLine("Find by tracking number:");
                Console.WriteLine(repository.ExplainTrackingNumberQuery("APX-10000001"));
                Console.WriteLine("Pending scans:");
                Console.WriteLine(repository.ExplainPendingQuery());
                break;
            default:
                Console.WriteLine("Unknown option.");
                break;
        }
    }
    catch (ArgumentException exception)
    {
        Console.WriteLine($"Invalid input: {exception.Message}");
    }
    catch (InvalidOperationException exception)
    {
        Console.WriteLine($"Could not complete the action: {exception.Message}");
    }
}

async Task AddScanAsync()
{
    var trackingNumber = Prompt("Tracking number (APX-12345678)");
    var scanType = Enum.Parse<ScanType>(Prompt("Scan type (Pickup/InTransit/Delivered/Failed)"), ignoreCase: true);
    var courierId = Prompt("Courier ID (e.g. courier-007)");
    var location = Prompt("Location");
    var notes = Prompt("Notes (optional, '#reject' makes the fake server reject it)");
    var signature = scanType == ScanType.Delivered ? Prompt("Recipient signature name") : null;
    var scan = await outbox.RecordScanAsync(trackingNumber, scanType, courierId, location, notes, signature);
    Console.WriteLine($"Saved locally: {scan}");
}

async Task SeedAsync()
{
    // Fictional couriers, places and tracking numbers.
    var start = DateTimeOffset.UtcNow.AddHours(-6);
    (string Tracking, ScanType Type, string Courier, string Location, string? Notes)[] demo =
    [
        ("APX-10000001", ScanType.Pickup, "courier-007", "Maplewood Depot", null),
        ("APX-10000001", ScanType.InTransit, "courier-007", "Northgate Hub", null),
        ("APX-10000001", ScanType.Delivered, "courier-007", "12 Elm Row, Maplewood", "Left with neighbour"),
        ("APX-10000002", ScanType.Pickup, "courier-007", "Maplewood Depot", null),
        ("APX-10000002", ScanType.Failed, "courier-007", "4 Birch Lane, Riverton", "Nobody home #reject"),
        ("APX-10000003", ScanType.Pickup, "courier-011", "Riverton Depot", null),
        ("APX-10000003", ScanType.InTransit, "courier-011", "Northgate Hub", null)
    ];

    for (var i = 0; i < demo.Length; i++)
    {
        var item = demo[i];
        await outbox.RecordScanAsync(item.Tracking, item.Type, item.Courier, item.Location, item.Notes,
            item.Type == ScanType.Delivered ? "J. Doe" : null, start.AddMinutes(30 * i));
    }

    Console.WriteLine($"Seeded {demo.Length} fictional scans.");
}

static void PrintScans(IReadOnlyList<ShipmentScan> scans)
{
    if (scans.Count == 0)
    {
        Console.WriteLine("   (no scans)");
        return;
    }

    foreach (var scan in scans)
    {
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"   {scan.Id}  {scan.TrackingNumber}  {scan.ScanType,-9}  {scan.CourierId,-12}  {scan.ScannedAtUtc:yyyy-MM-dd HH:mm}Z  {scan.SyncStatus,-7}  {scan.Location}{(scan.LastSyncError is null ? string.Empty : "  (" + scan.LastSyncError + ")")}"));
    }
}

static string Prompt(string label)
{
    Console.Write($"{label}: ");
    return Console.ReadLine()?.Trim() ?? string.Empty;
}
