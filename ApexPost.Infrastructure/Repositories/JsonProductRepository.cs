using ApexPost.Application.Abstractions;
using ApexPost.Domain.Entities;

namespace ApexPost.Infrastructure.Repositories;

public sealed class JsonProductRepository(string dataDirectory) : IProductRepository
{
    private readonly JsonCollectionStore<Product> _store =
        new(Path.Combine(dataDirectory, "products.json"));

    public Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _store.ReadAllAsync(cancellationToken);

    public async Task SaveAllAsync(IEnumerable<Product> products, CancellationToken cancellationToken = default)
    {
        await _store.WriteAllAsync(products, cancellationToken);
        await _store.WriteVersionAsync(cancellationToken);
    }

    public ValueTask<string> GetVersionAsync(CancellationToken cancellationToken = default) =>
        _store.ReadVersionAsync(cancellationToken);
}
