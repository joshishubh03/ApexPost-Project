using System.Diagnostics;
using System.Text.Json;
using ApexPost.Application.Abstractions;

namespace ApexPost.Infrastructure.Repositories;

/// <summary>
/// Coordinates JSON mutations across processes and commits related files as a recoverable unit.
/// </summary>
public sealed class JsonMutationCoordinator : IApplicationMutationCoordinator
{
    private static readonly AsyncLocal<JsonWriteTransactionScope?> AmbientScope = new();
    private static readonly JsonSerializerOptions JournalOptions = new(JsonSerializerDefaults.Web);

    private readonly string dataDirectory;
    private readonly string transactionDirectory;
    private readonly string lockPath;
    private readonly SemaphoreSlim processGate = new(1, 1);

    public JsonMutationCoordinator(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        this.dataDirectory = Path.GetFullPath(dataDirectory);
        transactionDirectory = Path.Combine(this.dataDirectory, ".apexpost-transaction");
        lockPath = Path.Combine(this.dataDirectory, ".apexpost-mutation.lock");
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await processGate.WaitAsync(cancellationToken);
        try
        {
            await using var processLock = await AcquireProcessLockAsync(cancellationToken);
            await RecoverTransactionAsync(cancellationToken);
        }
        finally
        {
            processGate.Release();
        }
    }

    public async Task<TResult> RunAsync<TResult>(
        Func<Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (AmbientScope.Value is { } activeScope &&
            string.Equals(activeScope.DataDirectory, dataDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return await operation();
        }

        await processGate.WaitAsync(cancellationToken);
        try
        {
            await using var processLock = await AcquireProcessLockAsync(cancellationToken);
            await RecoverTransactionAsync(cancellationToken);

            var scope = new JsonWriteTransactionScope(dataDirectory, transactionDirectory);
            var previousScope = AmbientScope.Value;
            AmbientScope.Value = scope;
            try
            {
                var result = await operation();
                await scope.CommitAsync();
                return result;
            }
            catch
            {
                await scope.RollbackAsync();
                throw;
            }
            finally
            {
                AmbientScope.Value = previousScope;
                await scope.DisposeAsync();
            }
        }
        finally
        {
            processGate.Release();
        }
    }

    public async Task RunAsync(Func<Task> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await RunAsync(async () =>
        {
            await operation();
            return true;
        }, cancellationToken);
    }

    internal static Task<(bool Found, string? Json)> TryReadStagedAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetFullPath(Path.GetDirectoryName(path) ?? ".");
        return AmbientScope.Value is { } scope &&
            string.Equals(scope.DataDirectory, directory, StringComparison.OrdinalIgnoreCase)
                ? scope.TryReadStagedAsync(path, cancellationToken)
                : Task.FromResult((false, (string?)null));
    }

    internal static Task StageWriteAsync(string path, string json, CancellationToken cancellationToken) =>
        AmbientScope.Value is { } scope
            ? scope.StageWriteAsync(path, json, cancellationToken)
            : Task.FromException(new InvalidOperationException("No JSON mutation transaction is active."));

    internal static bool HasActiveTransactionFor(string path)
    {
        var directory = Path.GetFullPath(Path.GetDirectoryName(path) ?? ".");
        return AmbientScope.Value is { } scope &&
            string.Equals(scope.DataDirectory, directory, StringComparison.OrdinalIgnoreCase);
    }

    internal static Task<TResult> RunStorageReadAsync<TResult>(
        string path,
        Func<Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetFullPath(Path.GetDirectoryName(path) ?? ".");
        if (AmbientScope.Value is { } scope &&
            string.Equals(scope.DataDirectory, directory, StringComparison.OrdinalIgnoreCase))
        {
            return operation();
        }

        return new JsonMutationCoordinator(directory).RunStorageReadAsync(operation, cancellationToken);
    }

    internal static Task<TResult> RunStorageMutationAsync<TResult>(
        string dataDirectory,
        Func<Task<TResult>> operation,
        CancellationToken cancellationToken) =>
        new JsonMutationCoordinator(dataDirectory).RunAsync(operation, cancellationToken);

    private async Task<FileStream> AcquireProcessLockAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(dataDirectory);
        var delayMilliseconds = 15;
        var waitTime = Stopwatch.StartNew();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException exception)
            {
                if (waitTime.Elapsed >= TimeSpan.FromSeconds(30))
                {
                    throw new TimeoutException("Timed out waiting for the JSON mutation lock.", exception);
                }
                await Task.Delay(delayMilliseconds, cancellationToken);
                delayMilliseconds = Math.Min(delayMilliseconds * 2, 250);
            }
        }
    }

    private async Task<FileStream> AcquireSharedProcessLockAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(dataDirectory);
        if (!File.Exists(lockPath))
        {
            await using var initializer = await AcquireProcessLockAsync(cancellationToken);
        }

        var delayMilliseconds = 15;
        var waitTime = Stopwatch.StartNew();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(lockPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            catch (IOException exception)
            {
                if (waitTime.Elapsed >= TimeSpan.FromSeconds(30))
                {
                    throw new TimeoutException("Timed out waiting for a shared JSON read lock.", exception);
                }
                await Task.Delay(delayMilliseconds, cancellationToken);
                delayMilliseconds = Math.Min(delayMilliseconds * 2, 250);
            }
        }
    }

    private async Task<TResult> RunStorageReadAsync<TResult>(
        Func<Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        var sharedLock = await AcquireSharedProcessLockAsync(cancellationToken);
        if (!Directory.Exists(transactionDirectory))
        {
            await using (sharedLock)
            {
                return await operation();
            }
        }

        await sharedLock.DisposeAsync();
        return await RunExclusiveStorageOperationAsync(operation, cancellationToken);
    }

    private async Task<TResult> RunExclusiveStorageOperationAsync<TResult>(
        Func<Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        await processGate.WaitAsync(cancellationToken);
        try
        {
            await using var processLock = await AcquireProcessLockAsync(cancellationToken);
            await RecoverTransactionAsync(cancellationToken);
            return await operation();
        }
        finally
        {
            processGate.Release();
        }
    }

    private async Task RecoverTransactionAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(transactionDirectory))
        {
            return;
        }

        var journalPath = Path.Combine(transactionDirectory, "journal.json");
        if (!File.Exists(journalPath))
        {
            Directory.Delete(transactionDirectory, recursive: true);
            return;
        }

        var journalJson = await File.ReadAllTextAsync(journalPath, cancellationToken);
        var journal = JsonSerializer.Deserialize<JsonTransactionJournal>(journalJson, JournalOptions)
            ?? throw new InvalidDataException("The JSON transaction journal is empty or invalid.");

        if (journal.State == JsonTransactionState.Prepared)
        {
            await RestoreOriginalFilesAsync(journal.Entries, cancellationToken);
        }

        Directory.Delete(transactionDirectory, recursive: true);
    }

    private async Task RestoreOriginalFilesAsync(
        IReadOnlyList<JsonTransactionEntry> entries,
        CancellationToken cancellationToken)
    {
        foreach (var entry in entries)
        {
            var destination = ResolveDataPath(entry.RelativePath);
            if (entry.ExistedBefore)
            {
                var backupPath = ResolveTransactionPath(entry.BackupFileName
                    ?? throw new InvalidDataException("A transaction backup path is missing."));
                var originalBytes = await File.ReadAllBytesAsync(backupPath, cancellationToken);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                var restorePath = destination + $".{Guid.NewGuid():N}.restore";
                await File.WriteAllBytesAsync(restorePath, originalBytes, cancellationToken);
                File.Move(restorePath, destination, overwrite: true);
            }
            else if (File.Exists(destination))
            {
                File.Delete(destination);
            }
        }
    }

    private string ResolveDataPath(string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(dataDirectory, relativePath));
        var rootPrefix = dataDirectory.EndsWith(Path.DirectorySeparatorChar)
            ? dataDirectory
            : dataDirectory + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("A JSON transaction path escaped its data directory.");
        }
        return fullPath;
    }

    private string ResolveTransactionPath(string fileName)
    {
        if (Path.GetFileName(fileName) != fileName)
        {
            throw new InvalidDataException("A JSON transaction artifact path is invalid.");
        }
        return Path.Combine(transactionDirectory, fileName);
    }

    private enum JsonTransactionState { Prepared, Committed }

    private sealed record JsonTransactionJournal(JsonTransactionState State, IReadOnlyList<JsonTransactionEntry> Entries);

    private sealed record JsonTransactionEntry(
        string RelativePath,
        string StagedFileName,
        string? BackupFileName,
        bool ExistedBefore);

    private sealed class JsonWriteTransactionScope(string dataDirectory, string transactionDirectory) : IAsyncDisposable
    {
        private readonly Dictionary<string, string> stagedFiles = new(StringComparer.OrdinalIgnoreCase);
        private bool committed;
        private bool disposed;

        public string DataDirectory { get; } = Path.GetFullPath(dataDirectory);

        public async Task StageWriteAsync(string path, string json, CancellationToken cancellationToken)
        {
            var fullPath = ResolveDataPath(path);
            if (!stagedFiles.TryGetValue(fullPath, out var stagedFileName))
            {
                stagedFileName = $"staged-{stagedFiles.Count:D4}.json";
                stagedFiles.Add(fullPath, stagedFileName);
            }

            Directory.CreateDirectory(transactionDirectory);
            var stagedPath = Path.Combine(transactionDirectory, stagedFileName);
            var temporaryPath = stagedPath + $".{Guid.NewGuid():N}.tmp";
            try
            {
                await File.WriteAllTextAsync(temporaryPath, json, cancellationToken);
                File.Move(temporaryPath, stagedPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        public async Task<(bool Found, string? Json)> TryReadStagedAsync(
            string path,
            CancellationToken cancellationToken)
        {
            var fullPath = ResolveDataPath(path);
            if (!stagedFiles.TryGetValue(fullPath, out var stagedFileName))
            {
                return (false, null);
            }

            var json = await File.ReadAllTextAsync(Path.Combine(transactionDirectory, stagedFileName), cancellationToken);
            return (true, json);
        }

        public async Task CommitAsync()
        {
            if (stagedFiles.Count == 0)
            {
                committed = true;
                return;
            }

            Directory.CreateDirectory(transactionDirectory);
            var entries = new List<JsonTransactionEntry>(stagedFiles.Count);
            var index = 0;
            foreach (var (destination, stagedFileName) in stagedFiles)
            {
                var relativePath = Path.GetRelativePath(DataDirectory, destination);
                var existedBefore = File.Exists(destination);
                var backupFileName = existedBefore ? $"backup-{index:D4}.json" : null;
                if (backupFileName is not null)
                {
                    var originalBytes = await File.ReadAllBytesAsync(destination, CancellationToken.None);
                    await File.WriteAllBytesAsync(Path.Combine(transactionDirectory, backupFileName), originalBytes, CancellationToken.None);
                }
                entries.Add(new JsonTransactionEntry(relativePath, stagedFileName, backupFileName, existedBefore));
                index++;
            }

            await WriteJournalAsync(new JsonTransactionJournal(JsonTransactionState.Prepared, entries));
            try
            {
                foreach (var entry in entries)
                {
                    var destination = ResolveDataPath(entry.RelativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    File.Move(Path.Combine(transactionDirectory, entry.StagedFileName), destination, overwrite: true);
                }

                await WriteJournalAsync(new JsonTransactionJournal(JsonTransactionState.Committed, entries));
                committed = true;
            }
            catch
            {
                await RestoreOriginalFilesAsync(entries);
                Directory.Delete(transactionDirectory, recursive: true);
                throw;
            }
            finally
            {
                if (committed && Directory.Exists(transactionDirectory))
                {
                    Directory.Delete(transactionDirectory, recursive: true);
                }
            }
        }

        public async Task RollbackAsync()
        {
            if (disposed || committed || !Directory.Exists(transactionDirectory))
            {
                return;
            }

            var journalPath = Path.Combine(transactionDirectory, "journal.json");
            if (File.Exists(journalPath))
            {
                var json = await File.ReadAllTextAsync(journalPath, CancellationToken.None);
                var journal = JsonSerializer.Deserialize<JsonTransactionJournal>(json, JournalOptions);
                if (journal?.State == JsonTransactionState.Prepared)
                {
                    await RestoreOriginalFilesAsync(journal.Entries);
                }
            }

            if (Directory.Exists(transactionDirectory))
            {
                Directory.Delete(transactionDirectory, recursive: true);
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (!committed)
            {
                await RollbackAsync();
            }
            disposed = true;
        }

        private async Task RestoreOriginalFilesAsync(
            IReadOnlyList<JsonTransactionEntry> entries,
            CancellationToken cancellationToken = default)
        {
            foreach (var entry in entries)
            {
                var destination = ResolveDataPath(entry.RelativePath);
                if (entry.ExistedBefore)
                {
                    var backupFileName = entry.BackupFileName
                        ?? throw new InvalidDataException("A transaction backup path is missing.");
                    var originalBytes = await File.ReadAllBytesAsync(Path.Combine(transactionDirectory, backupFileName), cancellationToken);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    var restorePath = destination + $".{Guid.NewGuid():N}.restore";
                    await File.WriteAllBytesAsync(restorePath, originalBytes, cancellationToken);
                    File.Move(restorePath, destination, overwrite: true);
                }
                else if (File.Exists(destination))
                {
                    File.Delete(destination);
                }
            }
        }

        private async Task WriteJournalAsync(JsonTransactionJournal journal)
        {
            var journalPath = Path.Combine(transactionDirectory, "journal.json");
            var temporaryPath = journalPath + $".{Guid.NewGuid():N}.tmp";
            try
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(journal, JournalOptions);
                await File.WriteAllBytesAsync(temporaryPath, bytes, CancellationToken.None);
                File.Move(temporaryPath, journalPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private string ResolveDataPath(string path)
        {
            var fullPath = Path.GetFullPath(Path.IsPathRooted(path)
                ? path
                : Path.Combine(DataDirectory, path));
            var prefix = DataDirectory.EndsWith(Path.DirectorySeparatorChar)
                ? DataDirectory
                : DataDirectory + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("A mutation attempted to write outside its configured data directory.");
            }
            return fullPath;
        }
    }
}
