using ApexPost.Domain.Enums;

namespace ApexPost.Application.Models;

public sealed record ShipmentTrackingSummary(
    string TrackingNumber,
    string ZoneName,
    ShippingServiceLevel ServiceLevel,
    decimal WeightKilograms,
    string Status,
    DateTimeOffset AcceptedAt);
