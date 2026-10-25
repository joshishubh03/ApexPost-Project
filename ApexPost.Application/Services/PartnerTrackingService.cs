using ApexPost.Application.Abstractions;
using ApexPost.Application.Models;

namespace ApexPost.Application.Services;

public sealed class PartnerTrackingService(ICounterTransactionRepository transactionRepository)
{
    public async Task<ShipmentTrackingSummary?> FindAsync(
        string trackingNumber,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackingNumber);
        var normalizedNumber = trackingNumber.Trim();
        var transaction = (await transactionRepository.GetAllAsync(cancellationToken))
            .FirstOrDefault(item => string.Equals(
                item.TransactionNumber, normalizedNumber, StringComparison.OrdinalIgnoreCase) &&
                item.ShippingRateId is not null);
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
}
