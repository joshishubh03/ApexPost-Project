using ApexPost.Application.Abstractions;
using ApexPost.Application.Services;
using ApexPost.Domain.Entities;

namespace ApexPost.Tests;

public sealed class ProductCatalogueCacheTests
{
    [Fact]
    public async Task Catalogue_reads_are_cached_and_mutations_refresh_the_snapshot()
    {
        var product = new Product(Guid.NewGuid(), "PEN-01", "Pen", "Blue ink pen", "Stationery", 1.25m, true);
        var repository = new FakeProductRepository([product]);
        var service = new ProductCatalogueService(repository);

        var initial = await service.GetCatalogueAsync();
        var warmCache = service.GetCatalogueAsync();

        Assert.True(warmCache.IsCompletedSuccessfully);
        Assert.Same(initial, await warmCache);
        Assert.Equal(1, repository.ReadCount);

        await service.CreateAsync("BOX-01", "Box", "Small carton", "Packaging", 2.50m);
        var afterCreate = await service.GetCatalogueAsync();

        Assert.Equal(2, repository.ReadCount);
        Assert.Equal(2, afterCreate.Count);

        await service.SetActiveAsync(product.Id, false);
        var afterUpdate = await service.GetCatalogueAsync();

        Assert.Equal(3, repository.ReadCount);
        Assert.False(afterUpdate.Single(item => item.Id == product.Id).IsActive);
    }

    private sealed class FakeProductRepository(IEnumerable<Product> products) : IProductRepository
    {
        private IReadOnlyList<Product> items = products.ToArray();
        public int ReadCount { get; private set; }

        public Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadCount++;
            return Task.FromResult(items);
        }

        public Task SaveAllAsync(IEnumerable<Product> products, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            items = products.ToArray();
            return Task.CompletedTask;
        }
    }
}
