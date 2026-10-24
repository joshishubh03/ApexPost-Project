using ApexPost.Application.Abstractions;
using ApexPost.Domain.Entities;

namespace ApexPost.Infrastructure.Repositories;

public sealed class JsonShippingZoneRepository(string dataDirectory) : IShippingZoneRepository
{
    private readonly JsonCollectionStore<ShippingZone> _store =
        new(Path.Combine(dataDirectory, "shipping-zones.json"));

    public Task<IReadOnlyList<ShippingZone>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _store.ReadAllAsync(cancellationToken);

    public Task SaveAllAsync(IEnumerable<ShippingZone> zones, CancellationToken cancellationToken = default) =>
        _store.WriteAllAsync(zones, cancellationToken);
}
