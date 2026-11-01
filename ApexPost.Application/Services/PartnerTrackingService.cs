using ApexPost.Application.Abstractions;
using ApexPost.Application.Models;

namespace ApexPost.Application.Services;

public sealed class PartnerTrackingService(ICourierTrackingClient courierTrackingClient)
{
    public async Task<ShipmentTrackingSummary?> FindAsync(
        string trackingNumber,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackingNumber);
        return await courierTrackingClient.FindShipmentAsync(trackingNumber, cancellationToken);
    }
}
