using ApexPost.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Week 4 Part C console consumer of the NSwag-generated v2 client. Fictional data only.
// Start the API first:  dotnet run --project ApexPost.Api --launch-profile https
var baseUrl = args.FirstOrDefault() ?? Environment.GetEnvironmentVariable("APEXPOST_API_BASEURL") ?? "https://localhost:7301/";

var services = new ServiceCollection();
services.AddLogging(logging => logging.AddSimpleConsole(options => options.SingleLine = true));

// Typed client registered with IHttpClientFactory (no `new HttpClient()` per call).
services.AddHttpClient<IApexPostApiClient, ApexPostApiClient>(client =>
{
    client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

await using var provider = services.BuildServiceProvider();
var api = provider.GetRequiredService<IApexPostApiClient>();
var log = provider.GetRequiredService<ILoggerFactory>().CreateLogger("ApexPost.Client.Consumer");

var trackingNumber = $"APX-{Random.Shared.Next(10_000_000, 99_999_999)}";

// 1. Create a shipment.
var created = await api.CreateShipmentAsync(new CreateShipmentRequest
{
    TrackingNumber = trackingNumber,
    WeightKg = 2.5m,
    RecipientName = "Jordan Example",
    RecipientPhone = "+15550100123",
    RecipientAddress = "12 Elm Row",
    City = "Maplewood"
});
log.LogInformation("Created {TrackingNumber} with status {Status}", created.TrackingNumber, created.Status);

// 2. Fetch it.
var fetched = await api.GetShipmentAsync(trackingNumber);
log.LogInformation("Fetched {TrackingNumber}: recipient {Recipient} in {City}", fetched.TrackingNumber, fetched.Recipient?.Name, fetched.Recipient?.City);

// 3. Upload two scans (scan IDs in the Part A format scan::{Guid}).
var upload = await api.UploadScansAsync(trackingNumber, new ScanUploadRequest
{
    Scans =
    [
        new ScanUploadItemRequest
        {
            ScanId = $"scan::{Guid.NewGuid():D}",
            ScanType = CourierScanType.Pickup,
            CourierId = "courier-007",
            Location = "Maplewood Depot",
            ScannedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-20)
        },
        new ScanUploadItemRequest
        {
            ScanId = $"scan::{Guid.NewGuid():D}",
            ScanType = CourierScanType.InTransit,
            CourierId = "courier-007",
            Location = "Northgate Hub",
            ScannedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5)
        }
    ]
});
log.LogInformation("Scans: accepted {Accepted}, duplicates skipped {Duplicates}, rejected {Rejected}",
    upload.Accepted, upload.DuplicatesSkipped, upload.Rejected?.Count ?? 0);

var afterScans = await api.GetShipmentAsync(trackingNumber);
log.LogInformation("Status is now {Status}; latest scan {ScanType} at {Location}",
    afterScans.Status, afterScans.LatestScan?.ScanType, afterScans.LatestScan?.Location);

// 4. Handle a 404 through the generated exception type.
try
{
    await api.GetShipmentAsync("APX-00000000");
}
catch (ApexPostApiException<ProblemDetails> notFound) when (notFound.StatusCode == 404)
{
    log.LogInformation("404 handled via ApexPostApiException<ProblemDetails>: {Title} - {Detail}", notFound.Result?.Title, notFound.Result?.Detail);
}
catch (ApexPostApiException exception) when (exception.StatusCode == 404)
{
    log.LogInformation("404 handled via ApexPostApiException: {Message}", exception.Message);
}

// Partial-class extension (no generated code edited): null instead of an exception.
var missing = await ((ApexPostApiClient)api).TryGetShipmentAsync("APX-00000001");
log.LogInformation("TryGetShipmentAsync for an unknown tracking number returned {Result}", missing is null ? "null" : "a shipment");
