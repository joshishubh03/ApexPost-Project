using ApexPost.Domain.Entities;

namespace ApexPost.Application.Abstractions;

public interface IShippingZoneRepository
{
    Task<IReadOnlyList<ShippingZone>> GetAllAsync(CancellationToken cancellationToken = default);
    Task SaveAllAsync(IEnumerable<ShippingZone> zones, CancellationToken cancellationToken = default);
}
