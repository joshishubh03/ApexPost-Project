using ApexPost.Application.Abstractions;
using ApexPost.Domain.Entities;

namespace ApexPost.Application.Services;

public sealed class ProductCatalogueService(
    IProductRepository productRepository,
    IApplicationMutationCoordinator mutationCoordinator)
{
    private IReadOnlyList<Product>? cachedProducts;
    private string? cachedVersion;

    public ValueTask<IReadOnlyList<Product>> GetCatalogueAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = Volatile.Read(ref cachedProducts);
        return snapshot is not null
            ? new ValueTask<IReadOnlyList<Product>>(RefreshIfChangedAsync(snapshot, cancellationToken))
            : new ValueTask<IReadOnlyList<Product>>(LoadCatalogueAsync(cancellationToken));
    }

    public async Task<IReadOnlyList<Product>> SearchAsync(
        string? query,
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        var products = await GetCatalogueAsync(cancellationToken);
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
        await mutationCoordinator.RunAsync(async () =>
        {
            var products = (await productRepository.GetAllAsync(cancellationToken)).ToList();
            var product = new Product(Guid.NewGuid(), sku, name, description, category, price, isActive: true, taxCategory: taxCategory);

            if (products.Any(existing => string.Equals(existing.Sku, product.Sku, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("A product with this SKU already exists.");
            }

            products.Add(product);
            await productRepository.SaveAllAsync(products, cancellationToken);
        }, cancellationToken);
        InvalidateCache();
    }

    public async Task SetActiveAsync(Guid productId, bool isActive, CancellationToken cancellationToken = default)
    {
        await mutationCoordinator.RunAsync(async () =>
        {
            var products = (await productRepository.GetAllAsync(cancellationToken)).ToList();
            var index = products.FindIndex(product => product.Id == productId);
            if (index < 0)
            {
                throw new KeyNotFoundException("The product could not be found.");
            }

            products[index] = products[index].WithActiveStatus(isActive);
            await productRepository.SaveAllAsync(products, cancellationToken);
        }, cancellationToken);
        InvalidateCache();
    }

    private async Task<IReadOnlyList<Product>> RefreshIfChangedAsync(
        IReadOnlyList<Product> snapshot,
        CancellationToken cancellationToken)
    {
        var version = await productRepository.GetVersionAsync(cancellationToken);
        if (string.Equals(version, Volatile.Read(ref cachedVersion), StringComparison.Ordinal))
        {
            return snapshot;
        }
        return await LoadCatalogueAsync(cancellationToken);
    }

    private Task<IReadOnlyList<Product>> LoadCatalogueAsync(CancellationToken cancellationToken) =>
        mutationCoordinator.RunAsync(async () =>
        {
            var current = Volatile.Read(ref cachedProducts);
            var version = await productRepository.GetVersionAsync(cancellationToken);
            if (current is not null &&
                string.Equals(version, Volatile.Read(ref cachedVersion), StringComparison.Ordinal))
            {
                return current;
            }

            var products = (await productRepository.GetAllAsync(cancellationToken)).ToArray();
            var loadedVersion = await productRepository.GetVersionAsync(cancellationToken);
            Volatile.Write(ref cachedVersion, loadedVersion);
            Volatile.Write(ref cachedProducts, products);
            return products;
        }, cancellationToken);

    private void InvalidateCache()
    {
        Volatile.Write(ref cachedVersion, null);
        Volatile.Write(ref cachedProducts, null);
    }
}
