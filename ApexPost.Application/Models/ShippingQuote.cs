using ApexPost.Domain.Enums;

namespace ApexPost.Application.Models;

public sealed record ShippingQuote(
    string ZoneName,
    ShippingServiceLevel ServiceLevel,
    decimal WeightKilograms,
    decimal Price);
