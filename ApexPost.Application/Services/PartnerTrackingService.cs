using ApexPost.Application.Models;

namespace ApexPost.Application.Services;

public sealed class PartnerTrackingService(SimulatedCourierTrackingClient courierTrackingClient)
{
    public async Task<ShipmentTrackingSummary?> FindAsync(
        string trackingNumber,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackingNumber);
        var transaction = await courierTrackingClient.FindShipmentAsync(trackingNumber, cancellationToken);
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
