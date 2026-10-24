using ApexPost.Domain.Entities;

namespace ApexPost.Application.Models;

public sealed record ShippingSchedule(ShippingZone Zone, IReadOnlyList<ShippingRate> Rates);
