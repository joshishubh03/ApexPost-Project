using ApexPost.Courier.Offline.Storage;

namespace ApexPost.Courier.Offline.Tests;

/// <summary>A fresh apexpost_courier database in its own temp folder, deleted after the test.</summary>
public sealed class TemporaryCourierDatabase : IDisposable
{
    public TemporaryCourierDatabase()
    {
        Directory = Path.Combine(Path.GetTempPath(), "apexpost-courier-tests", Guid.NewGuid().ToString("N"));
        Database = new CourierDatabase(Directory);
        Repository = new CouchbaseScanRepository(Database);
    }

    public string Directory { get; }
    public CourierDatabase Database { get; }
    public CouchbaseScanRepository Repository { get; }

    public void Dispose()
    {
        Database.Dispose();
        try
        {
            System.IO.Directory.Delete(Directory, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: the OS may still hold a handle for a moment.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
