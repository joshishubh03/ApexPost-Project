using ApexPost.Domain.Entities;

namespace ApexPost.Application.Abstractions;

public interface IInventoryRepository
{
    Task<IReadOnlyList<InventoryItem>> GetAllAsync(CancellationToken cancellationToken = default);
    Task SaveAllAsync(IEnumerable<InventoryItem> items, CancellationToken cancellationToken = default);
}
