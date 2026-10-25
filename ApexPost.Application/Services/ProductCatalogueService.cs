using ApexPost.Application.Abstractions;
using ApexPost.Domain.Entities;

namespace ApexPost.Application.Services;

public sealed class ProductCatalogueService(IProductRepository productRepository)
{
    public async Task<IReadOnlyList<Product>> SearchAsync(
        string? query,
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        var products = await productRepository.GetAllAsync(cancellationToken);
        var filtered = products.AsEnumerable();

        if (!includeInactive)
        {
            filtered = filtered.Where(product => product.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var search = query.Trim();
            filtered = filtered.Where(product =>
                product.Sku.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                product.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                product.Category.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                product.Description.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        return filtered.OrderBy(product => product.Category)
            .ThenBy(product => product.Name)
            .ToArray();
    }

    public async Task CreateAsync(
        string sku,
        string name,
        string description,
        string category,
        decimal price,
        string taxCategory = "Standard",
        CancellationToken cancellationToken = default)
    {
        var products = (await productRepository.GetAllAsync(cancellationToken)).ToList();
        var product = new Product(Guid.NewGuid(), sku, name, description, category, price, isActive: true, taxCategory: taxCategory);

        if (products.Any(existing => string.Equals(existing.Sku, product.Sku, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("A product with this SKU already exists.");
        }

        products.Add(product);
        await productRepository.SaveAllAsync(products, cancellationToken);
    }

    public async Task SetActiveAsync(Guid productId, bool isActive, CancellationToken cancellationToken = default)
    {
        var products = (await productRepository.GetAllAsync(cancellationToken)).ToList();
        var index = products.FindIndex(product => product.Id == productId);
        if (index < 0)
        {
            throw new KeyNotFoundException("The product could not be found.");
        }

        products[index] = products[index].WithActiveStatus(isActive);
        await productRepository.SaveAllAsync(products, cancellationToken);
    }
}
