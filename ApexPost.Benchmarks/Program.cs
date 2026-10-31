using System.Diagnostics;
using System.Text.Json;
using ApexPost.Application.Abstractions;
using ApexPost.Application.Services;
using ApexPost.Domain.Entities;
using ApexPost.Infrastructure.Repositories;

var productCount = ReadPositiveInteger(args.ElementAtOrDefault(0), 1_000);
var iterations = ReadPositiveInteger(args.ElementAtOrDefault(1), 500);
const int rounds = 5;
var dataDirectory = Path.Combine(Path.GetTempPath(), $"apexpost-cache-benchmark-{Guid.NewGuid():N}");
Directory.CreateDirectory(dataDirectory);

try
{
    var repository = new JsonProductRepository(dataDirectory);
    var seededProducts = Enumerable.Range(1, productCount)
        .Select(index => new Product(
            Guid.NewGuid(),
            $"BENCH-{index:000000}",
            $"Benchmark Product {index}",
            "Synthetic local benchmark record",
            "Stationery",
            index % 100 + 0.25m,
            true))
        .ToArray();
    await repository.SaveAllAsync(seededProducts);

    var catalogue = new ProductCatalogueService(repository, new JsonMutationCoordinator(dataDirectory));
    await catalogue.GetCatalogueAsync();

    for (var index = 0; index < 25; index++)
    {
        await repository.GetAllAsync();
        await catalogue.GetCatalogueAsync();
    }

    var coldReadMilliseconds = new double[rounds];
    var cachedReadMilliseconds = new double[rounds];
    for (var round = 0; round < rounds; round++)
    {
        coldReadMilliseconds[round] = await MeasureAsync(iterations, async () =>
        {
            var result = await repository.GetAllAsync();
            if (result.Count != productCount)
                throw new InvalidOperationException("The JSON read returned an unexpected product count.");
        });
        cachedReadMilliseconds[round] = await MeasureAsync(iterations, async () =>
        {
            var result = await catalogue.GetCatalogueAsync();
            if (result.Count != productCount)
                throw new InvalidOperationException("The cache read returned an unexpected product count.");
        });
    }

    var courierConcurrencyOneMilliseconds = await MeasureCourierBatchAsync(maximumConcurrency: 1);
    var courierConcurrencyThreeMilliseconds = await MeasureCourierBatchAsync(maximumConcurrency: 3);
    var coldMedian = Median(coldReadMilliseconds);
    var cachedMedian = Median(cachedReadMilliseconds);
    var report = new
    {
        runtime = Environment.Version.ToString(),
        os = Environment.OSVersion.ToString(),
        processorCount = Environment.ProcessorCount,
        productCount,
        iterationsPerRound = iterations,
        rounds,
        storageNote = "Local temporary JSON file; the OS file cache is warm after warmup.",
        jsonReadDeserializeMedianMilliseconds = Math.Round(coldMedian, 3),
        cachedSnapshotMedianMilliseconds = Math.Round(cachedMedian, 3),
        readSpeedRatio = Math.Round(coldMedian / Math.Max(cachedMedian, 0.001), 2),
        courierBatch = new
        {
            requestCount = 12,
            simulatedLatencyMilliseconds = 40,
            concurrencyOneMilliseconds = Math.Round(courierConcurrencyOneMilliseconds, 3),
            concurrencyThreeMilliseconds = Math.Round(courierConcurrencyThreeMilliseconds, 3),
            note = "Synthetic delay only; this measures the configured bound, not an external courier provider."
        }
    };

    Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
}
finally
{
    if (Directory.Exists(dataDirectory))
        Directory.Delete(dataDirectory, recursive: true);
}

static int ReadPositiveInteger(string? text, int fallback) =>
    int.TryParse(text, out var value) && value > 0 ? value : fallback;

static async Task<double> MeasureAsync(int iterations, Func<Task> operation)
{
    var stopwatch = Stopwatch.StartNew();
    for (var index = 0; index < iterations; index++)
        await operation();
    stopwatch.Stop();
    return stopwatch.Elapsed.TotalMilliseconds;
}

static double Median(IEnumerable<double> values)
{
    var ordered = values.Order().ToArray();
    return ordered[ordered.Length / 2];
}

static async Task<double> MeasureCourierBatchAsync(int maximumConcurrency)
{
    var client = new SimulatedCourierTrackingClient(
        new EmptyCounterTransactionRepository(),
        maximumConcurrency,
        timeout: TimeSpan.FromSeconds(5),
        simulatedLatency: TimeSpan.FromMilliseconds(40));
    var stopwatch = Stopwatch.StartNew();
    await Task.WhenAll(Enumerable.Range(0, 12)
        .Select(index => client.FindShipmentAsync($"BENCH-{index:000}")));
    stopwatch.Stop();
    return stopwatch.Elapsed.TotalMilliseconds;
}

sealed class EmptyCounterTransactionRepository : ICounterTransactionRepository
{
    public Task<IReadOnlyList<CounterTransaction>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<CounterTransaction>>([]);
    }

    public Task SaveAllAsync(IEnumerable<CounterTransaction> transactions, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
