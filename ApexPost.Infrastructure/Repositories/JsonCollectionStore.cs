using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApexPost.Infrastructure.Repositories;

internal sealed class JsonCollectionStore<T>(string path)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly string versionPath = path + ".version";

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    public Task<IReadOnlyList<T>> ReadAllAsync(CancellationToken cancellationToken = default) =>
        JsonMutationCoordinator.RunStorageReadAsync(
            path,
            () => ReadAllCoreAsync(cancellationToken),
            cancellationToken);

    private async Task<IReadOnlyList<T>> ReadAllCoreAsync(CancellationToken cancellationToken)
    {
        var staged = await JsonMutationCoordinator.TryReadStagedAsync(path, cancellationToken);
        if (!staged.Found && !File.Exists(path))
        {
            return [];
        }

        var json = staged.Found
            ? staged.Json
            : await File.ReadAllTextAsync(path, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? [];
    }

    public Task WriteAllAsync(IEnumerable<T> items, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(items, JsonOptions);
        if (JsonMutationCoordinator.HasActiveTransactionFor(path))
        {
            return JsonMutationCoordinator.StageWriteAsync(path, json, cancellationToken);
        }

        return JsonMutationCoordinator.RunStorageMutationAsync(
            Path.GetDirectoryName(path) ?? ".",
            async () =>
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
                try
                {
                    await File.WriteAllTextAsync(temporaryPath, json, cancellationToken);
                    File.Move(temporaryPath, path, overwrite: true);
                }
                finally
                {
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }
                }

                return true;
            },
            cancellationToken);
    }

    public ValueTask<string> ReadVersionAsync(CancellationToken cancellationToken = default) =>
        new(JsonMutationCoordinator.RunStorageReadAsync(
            versionPath,
            () => ReadVersionCoreAsync(cancellationToken),
            cancellationToken));

    private async Task<string> ReadVersionCoreAsync(CancellationToken cancellationToken)
    {
        var staged = await JsonMutationCoordinator.TryReadStagedAsync(versionPath, cancellationToken);
        if (staged.Found)
        {
            return staged.Json ?? string.Empty;
        }
        if (File.Exists(versionPath))
        {
            return await File.ReadAllTextAsync(versionPath, cancellationToken);
        }
        return File.Exists(path)
            ? File.GetLastWriteTimeUtc(path).Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : "0";
    }

    public Task WriteVersionAsync(CancellationToken cancellationToken = default)
    {
        var version = Guid.NewGuid().ToString("N");
        if (JsonMutationCoordinator.HasActiveTransactionFor(versionPath))
        {
            return JsonMutationCoordinator.StageWriteAsync(versionPath, version, cancellationToken);
        }

        return JsonMutationCoordinator.RunStorageMutationAsync(
            Path.GetDirectoryName(versionPath) ?? ".",
            async () =>
            {
                var directory = Path.GetDirectoryName(versionPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                var temporaryPath = versionPath + $".{Guid.NewGuid():N}.tmp";
                try
                {
                    await File.WriteAllTextAsync(temporaryPath, version, cancellationToken);
                    File.Move(temporaryPath, versionPath, overwrite: true);
                }
                finally
                {
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }
                }

                return true;
            },
            cancellationToken);
    }
}
