using ApexPost.Domain.Entities;

namespace ApexPost.Application.Abstractions;

public interface IShippingRateRepository
{
    Task<IReadOnlyList<ShippingRate>> GetAllAsync(CancellationToken cancellationToken = default);
    Task SaveAllAsync(IEnumerable<ShippingRate> rates, CancellationToken cancellationToken = default);
}
