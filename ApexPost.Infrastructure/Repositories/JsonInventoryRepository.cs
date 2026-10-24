using ApexPost.Application.Abstractions;
using ApexPost.Domain.Entities;

namespace ApexPost.Infrastructure.Repositories;

public sealed class JsonInventoryRepository(string dataDirectory) : IInventoryRepository
{
    private readonly JsonCollectionStore<InventoryItem> _store =
        new(Path.Combine(dataDirectory, "inventory.json"));

    public Task<IReadOnlyList<InventoryItem>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _store.ReadAllAsync(cancellationToken);

    public Task SaveAllAsync(IEnumerable<InventoryItem> items, CancellationToken cancellationToken = default) =>
        _store.WriteAllAsync(items, cancellationToken);
}
