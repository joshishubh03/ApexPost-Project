using ApexPost.Application.Models;

namespace ApexPost.Application.Abstractions;

public interface ICourierTrackingClient
{
    Task<ShipmentTrackingSummary?> FindShipmentAsync(
        string trackingNumber,
        CancellationToken cancellationToken = default);
}
