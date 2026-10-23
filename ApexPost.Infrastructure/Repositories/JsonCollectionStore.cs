using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApexPost.Infrastructure.Repositories;

internal sealed class JsonCollectionStore<T>(string path)
{
    private static readonly SemaphoreSlim WriteGate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    public async Task<IReadOnlyList<T>> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? [];
    }

    public async Task WriteAllAsync(IEnumerable<T> items, CancellationToken cancellationToken = default)
    {
        await WriteGate.WaitAsync(cancellationToken);
        string? temporaryPath = null;

        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
            var json = JsonSerializer.Serialize(items, JsonOptions);
            await File.WriteAllTextAsync(temporaryPath, json, cancellationToken);
            File.Move(temporaryPath, path, overwrite: true);
            temporaryPath = null;
        }
        finally
        {
            if (temporaryPath is not null && File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            WriteGate.Release();
        }
    }
}
