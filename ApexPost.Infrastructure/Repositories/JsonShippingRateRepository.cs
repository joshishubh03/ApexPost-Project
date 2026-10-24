using ApexPost.Application.Abstractions;
using ApexPost.Domain.Entities;

namespace ApexPost.Infrastructure.Repositories;

public sealed class JsonShippingRateRepository(string dataDirectory) : IShippingRateRepository
{
    private readonly JsonCollectionStore<ShippingRate> _store =
        new(Path.Combine(dataDirectory, "shipping-rates.json"));

    public Task<IReadOnlyList<ShippingRate>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _store.ReadAllAsync(cancellationToken);

    public Task SaveAllAsync(IEnumerable<ShippingRate> rates, CancellationToken cancellationToken = default) =>
        _store.WriteAllAsync(rates, cancellationToken);
}
