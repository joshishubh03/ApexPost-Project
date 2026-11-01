using ApexPost.Application.Abstractions;
using ApexPost.Application.Models;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;

namespace ApexPost.Application.Services;

public sealed class SimulatedCourierTrackingClient : ICourierTrackingClient
{
    private readonly ICounterTransactionRepository transactionRepository;
    private readonly SemaphoreSlim concurrencyGate;
    private readonly TimeSpan timeout;
    private readonly TimeSpan simulatedLatency;

    public SimulatedCourierTrackingClient(
        ICounterTransactionRepository transactionRepository,
        int maximumConcurrency = 3,
        TimeSpan? timeout = null,
        TimeSpan? simulatedLatency = null)
    {
        ArgumentNullException.ThrowIfNull(transactionRepository);
        if (maximumConcurrency < 1)
            throw new ArgumentOutOfRangeException(nameof(maximumConcurrency));

        this.transactionRepository = transactionRepository;
        concurrencyGate = new SemaphoreSlim(maximumConcurrency, maximumConcurrency);
        this.timeout = timeout ?? TimeSpan.FromSeconds(2);
        this.simulatedLatency = simulatedLatency ?? TimeSpan.FromMilliseconds(40);
        if (this.timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        if (this.simulatedLatency < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(simulatedLatency));
    }

    public async Task<ShipmentTrackingSummary?> FindShipmentAsync(
        string trackingNumber,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackingNumber);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        var enteredGate = false;

        try
        {
            await concurrencyGate.WaitAsync(timeoutSource.Token);
            enteredGate = true;
            if (simulatedLatency > TimeSpan.Zero)
                await Task.Delay(simulatedLatency, timeoutSource.Token);

            var transactions = await transactionRepository.GetAllAsync(timeoutSource.Token);
            var transaction = transactions.FirstOrDefault(transaction =>
                transaction.ShippingRateId is not null &&
                string.Equals(transaction.TransactionNumber, trackingNumber.Trim(), StringComparison.OrdinalIgnoreCase));
            return transaction is null
                ? null
                : new ShipmentTrackingSummary(
                    transaction.TransactionNumber,
                    transaction.ShippingZoneName!,
                    transaction.ShippingServiceLevel!.Value,
                    transaction.ShipmentType,
                    transaction.ShippingWeightKilograms!.Value,
                    transaction.Status.ToString(),
                    transaction.OccurredAt);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("The simulated courier tracking lookup exceeded its configured timeout.");
        }
        finally
        {
            if (enteredGate)
                concurrencyGate.Release();
        }
    }
}
