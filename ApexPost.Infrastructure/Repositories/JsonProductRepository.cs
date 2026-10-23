using ApexPost.Application.Abstractions;
using ApexPost.Domain.Entities;

namespace ApexPost.Infrastructure.Repositories;

public sealed class JsonProductRepository(string dataDirectory) : IProductRepository
{
    private readonly JsonCollectionStore<Product> _store =
        new(Path.Combine(dataDirectory, "products.json"));

    public Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _store.ReadAllAsync(cancellationToken);

    public Task SaveAllAsync(IEnumerable<Product> products, CancellationToken cancellationToken = default) =>
        _store.WriteAllAsync(products, cancellationToken);
}
