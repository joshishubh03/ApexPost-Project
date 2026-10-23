using ApexPost.Domain.Entities;

namespace ApexPost.Application.Abstractions;

public interface IProductRepository
{
    Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default);
    Task SaveAllAsync(IEnumerable<Product> products, CancellationToken cancellationToken = default);
}
