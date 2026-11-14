using ApexPost.Application.Courier;
using ApexPost.Courier.Offline.Outbox;
using ApexPost.Courier.Offline.Storage;
using ApexPost.UnitTests;
using ApexPost.UnitTests.TestDoubles;

namespace ApexPost.Specs.Support;

/// <summary>
/// Per-scenario "world" for the scan-sync scenarios, shared between step classes through context injection.
/// The phone side is the real <see cref="OutboxService"/> on a real Couchbase Lite database in a temporary
/// folder; the server side is the real <see cref="CourierShipmentService"/> on an in-memory repository.
/// <see cref="DeviceHooks"/> opens it before each scenario and deletes everything after it.
/// </summary>
public sealed class CourierSyncContext
{
    /// <summary>Server clock: fixed, so the scenarios never depend on the real date.</summary>
    public static readonly DateTimeOffset ServerNow = FixedClock.DefaultNow;

    /// <summary>
    /// The phone's clock starts an hour before the server's "now" and ticks one minute per reading, so scans taken
    /// one after another have increasing times (scans are taken before they are uploaded).
    /// </summary>
    public static readonly DateTimeOffset PhoneStart = ServerNow.AddHours(-1);

    private CourierDatabase? database;

    public string CourierId { get; set; } = "courier-007";

    public string? DataDirectory { get; private set; }

    public CouchbaseScanRepository PhoneScans { get; private set; } = null!;

    public OutboxService Outbox { get; private set; } = null!;

    public CourierShipmentService Server { get; private set; } = null!;

    public InProcessShipmentUploader Uploader { get; private set; } = null!;

    public OutboxRunResult? LastSync { get; set; }

    public void Open()
    {
        DataDirectory = Path.Combine(Path.GetTempPath(), "apexpost-specs", Guid.NewGuid().ToString("N"));
        database = new CourierDatabase(DataDirectory);
        PhoneScans = new CouchbaseScanRepository(database);
        Server = new CourierShipmentService(
            new InMemoryCourierShipmentRepository(), ImmediateUnitOfWork.Instance, new FixedClock(ServerNow));
        Uploader = new InProcessShipmentUploader(Server);
        Outbox = new OutboxService(PhoneScans, Uploader, new SteppingClock(PhoneStart));
    }

    public void Close()
    {
        database?.Dispose();
        database = null;
        if (DataDirectory is not null)
        {
            try
            {
                Directory.Delete(DataDirectory, recursive: true);
            }
            catch (IOException)
            {
                // Best effort: the OS may hold a file handle for a moment after the database closes.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
