using ApexPost.Application.Abstractions;
using ApexPost.Application.Models;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;

namespace ApexPost.Application.Services;

public sealed class ShippingRateService(
    IShippingZoneRepository zoneRepository,
    IShippingRateRepository rateRepository,
    IApplicationMutationCoordinator mutationCoordinator)
{
    public async Task<IReadOnlyList<ShippingSchedule>> GetScheduleAsync(
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        var zones = await zoneRepository.GetAllAsync(cancellationToken);
        var rates = await rateRepository.GetAllAsync(cancellationToken);
        var schedules = zones.Where(zone => includeInactive || zone.IsActive)
            .OrderBy(zone => zone.Code)
            .Select(zone => new ShippingSchedule(
                zone,
                rates.Where(rate => rate.ZoneId == zone.Id && (includeInactive || rate.IsActive))
                    .OrderBy(rate => rate.ServiceLevel)
                    .ToArray()))
            .ToArray();
        return schedules;
    }

    public Task CreateZoneAsync(
        string code,
        string name,
        string destination,
        CancellationToken cancellationToken = default) => mutationCoordinator.RunAsync(async () =>
        {
            var zones = (await zoneRepository.GetAllAsync(cancellationToken)).ToList();
            var zone = new ShippingZone(Guid.NewGuid(), code, name, destination, isActive: true);
            if (zones.Any(existing => string.Equals(existing.Code, zone.Code, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("A shipping zone with this code already exists.");
            }

            zones.Add(zone);
            await zoneRepository.SaveAllAsync(zones, cancellationToken);
        }, cancellationToken);

    public Task CreateRateAsync(
        Guid zoneId,
        ShippingServiceLevel serviceLevel,
        decimal basePrice,
        decimal pricePerKilogram,
        decimal maximumWeightKilograms,
        CancellationToken cancellationToken = default) => mutationCoordinator.RunAsync(async () =>
        {
            var zone = (await zoneRepository.GetAllAsync(cancellationToken))
                .FirstOrDefault(candidate => candidate.Id == zoneId);
            if (zone is null || !zone.IsActive)
            {
                throw new InvalidOperationException("Select an active shipping zone.");
            }

            var rates = (await rateRepository.GetAllAsync(cancellationToken)).ToList();
            if (rates.Any(rate => rate.ZoneId == zoneId && rate.ServiceLevel == serviceLevel))
            {
                throw new InvalidOperationException("This service level already has a rate for the selected zone.");
            }

            rates.Add(new ShippingRate(
                Guid.NewGuid(), zoneId, serviceLevel, basePrice, pricePerKilogram, maximumWeightKilograms, isActive: true));
            await rateRepository.SaveAllAsync(rates, cancellationToken);
        }, cancellationToken);

    public async Task<ShippingQuote> QuoteAsync(
        Guid rateId,
        decimal weightKilograms,
        CancellationToken cancellationToken = default)
    {
        var rate = (await rateRepository.GetAllAsync(cancellationToken))
            .FirstOrDefault(candidate => candidate.Id == rateId && candidate.IsActive)
            ?? throw new KeyNotFoundException("The selected shipping rate is unavailable.");
        var zone = (await zoneRepository.GetAllAsync(cancellationToken))
            .FirstOrDefault(candidate => candidate.Id == rate.ZoneId && candidate.IsActive)
            ?? throw new KeyNotFoundException("The selected shipping zone is unavailable.");

        return new ShippingQuote(
            zone.Name,
            rate.ServiceLevel,
            weightKilograms,
            rate.CalculatePrice(weightKilograms));
    }
}
