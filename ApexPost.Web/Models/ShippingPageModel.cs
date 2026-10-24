using ApexPost.Application.Models;
using ApexPost.Domain.Entities;

namespace ApexPost.Web.Models;

public sealed record ShippingPageModel
{
    public IReadOnlyList<ShippingSchedule> Schedules { get; init; } = [];
    public IReadOnlyList<ShippingZone> ActiveZones { get; init; } = [];
    public IReadOnlyList<ShippingRate> ActiveRates { get; init; } = [];
    public CreateShippingZoneInputModel NewZone { get; init; } = new();
    public CreateShippingRateInputModel NewRate { get; init; } = new();
    public ShippingQuoteInputModel QuoteInput { get; init; } = new();
    public ShippingQuote? Quote { get; init; }
}
