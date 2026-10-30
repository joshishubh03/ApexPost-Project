using ApexPost.Application.Abstractions;
using ApexPost.Application.Services;
using ApexPost.Domain.Entities;

namespace ApexPost.Tests;

public sealed class CourierTrackingClientTests
{
    [Fact]
    public async Task Lookup_timeout_is_reported_as_a_timeout_failure()
    {
        var client = new SimulatedCourierTrackingClient(
            new DelayedTransactionRepository(TimeSpan.Zero),
            maximumConcurrency: 1,
            timeout: TimeSpan.FromMilliseconds(10),
            simulatedLatency: TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAsync<TimeoutException>(() => client.FindShipmentAsync("APT-TEST-001"));
    }

    [Fact]
    public async Task Caller_cancellation_is_not_converted_to_a_timeout()
    {
        var client = new SimulatedCourierTrackingClient(
            new DelayedTransactionRepository(TimeSpan.Zero),
            maximumConcurrency: 1,
            timeout: TimeSpan.FromSeconds(1),
            simulatedLatency: TimeSpan.FromMilliseconds(100));
        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(10));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.FindShipmentAsync("APT-TEST-001", cancellationSource.Token));
    }

    [Fact]
    public async Task Concurrent_lookup_count_is_bounded_by_the_configured_limit()
    {
        var repository = new DelayedTransactionRepository(TimeSpan.FromMilliseconds(20));
        var client = new SimulatedCourierTrackingClient(
            repository,
            maximumConcurrency: 2,
            timeout: TimeSpan.FromSeconds(2),
            simulatedLatency: TimeSpan.FromMilliseconds(1));

        await Task.WhenAll(Enumerable.Range(0, 12)
            .Select(_ => client.FindShipmentAsync("APT-TEST-001")));

        Assert.True(repository.MaximumActiveReads <= 2);
    }

    private sealed class DelayedTransactionRepository(TimeSpan readDelay) : ICounterTransactionRepository
    {
        private int activeReads;
        private int maximumActiveReads;
        public int MaximumActiveReads => Volatile.Read(ref maximumActiveReads);

        public async Task<IReadOnlyList<CounterTransaction>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var current = Interlocked.Increment(ref activeReads);
            UpdateMaximum(current);
            try
            {
                if (readDelay > TimeSpan.Zero)
                    await Task.Delay(readDelay, cancellationToken);
                return Array.Empty<CounterTransaction>();
            }
            finally
            {
                Interlocked.Decrement(ref activeReads);
            }
        }

        public Task SaveAllAsync(IEnumerable<CounterTransaction> transactions, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        private void UpdateMaximum(int value)
        {
            var observed = Volatile.Read(ref maximumActiveReads);
            while (value > observed)
            {
                var updated = Interlocked.CompareExchange(ref maximumActiveReads, value, observed);
                if (updated == observed)
                    return;
                observed = updated;
            }
        }
    }
}
