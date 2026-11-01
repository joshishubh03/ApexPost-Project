using System.Text.Json;
using ApexPost.Domain.Entities;
using ApexPost.Infrastructure.Repositories;

namespace ApexPost.Tests.Infrastructure;

public sealed class JsonMutationCoordinatorTests
{
    [Fact]
    public async Task Separate_coordinators_serialize_full_read_modify_write_operations()
    {
        await WithTemporaryDirectoryAsync(async directory =>
        {
            var repository = new JsonProductRepository(directory);
            var coordinators = new[] { new JsonMutationCoordinator(directory), new JsonMutationCoordinator(directory) };
            const int mutationCount = 24;

            await Task.WhenAll(Enumerable.Range(0, mutationCount).Select(index =>
                coordinators[index % coordinators.Length].RunAsync(async () =>
                {
                    var products = (await repository.GetAllAsync()).ToList();
                    await Task.Delay(2);
                    products.Add(new Product(
                        Guid.NewGuid(),
                        $"LOCK-{index:000}",
                        $"Concurrent {index}",
                        "Concurrent coordinator test",
                        "Stationery",
                        1m,
                        true));
                    await repository.SaveAllAsync(products);
                })));

            var saved = await repository.GetAllAsync();
            Assert.Equal(mutationCount, saved.Count);
            Assert.Equal(mutationCount, saved.Select(product => product.Sku).Distinct(StringComparer.Ordinal).Count());
        });
    }

    [Fact]
    public async Task Related_json_collections_commit_together()
    {
        await WithTemporaryDirectoryAsync(async directory =>
        {
            var coordinator = new JsonMutationCoordinator(directory);
            var products = new JsonProductRepository(directory);
            var inventory = new JsonInventoryRepository(directory);
            var product = new Product(Guid.NewGuid(), "COMMIT-01", "Committed item", "Test", "Stationery", 3m, true);
            var stock = new InventoryItem(Guid.NewGuid(), product.Id, 5, 1, []);

            await coordinator.RunAsync(async () =>
            {
                await products.SaveAllAsync([product]);
                await inventory.SaveAllAsync([stock]);
            });

            Assert.Equal(product.Id, Assert.Single(await products.GetAllAsync()).Id);
            Assert.Equal(product.Id, Assert.Single(await inventory.GetAllAsync()).ProductId);
            Assert.False(Directory.Exists(Path.Combine(directory, ".apexpost-transaction")));
        });
    }

    [Fact]
    public async Task Readers_wait_until_related_json_collections_have_committed()
    {
        await WithTemporaryDirectoryAsync(async directory =>
        {
            var coordinator = new JsonMutationCoordinator(directory);
            var products = new JsonProductRepository(directory);
            var inventory = new JsonInventoryRepository(directory);
            var product = new Product(Guid.NewGuid(), "READ-01", "Committed item", "Test", "Stationery", 3m, true);
            var stock = new InventoryItem(Guid.NewGuid(), product.Id, 5, 1, []);
            var staged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var allowCommit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var mutation = coordinator.RunAsync(async () =>
            {
                await products.SaveAllAsync([product]);
                await inventory.SaveAllAsync([stock]);
                staged.SetResult();
                await allowCommit.Task;
            });
            await staged.Task;

            var productRead = products.GetAllAsync();
            var inventoryRead = inventory.GetAllAsync();
            var reads = Task.WhenAll(productRead, inventoryRead);
            await Task.Yield();
            Assert.False(reads.IsCompleted);

            allowCommit.SetResult();
            await mutation;
            await reads;
            var savedProducts = await productRead;
            var savedInventory = await inventoryRead;
            Assert.Equal(product.Id, Assert.Single(savedProducts).Id);
            Assert.Equal(product.Id, Assert.Single(savedInventory).ProductId);
        });
    }

    [Fact]
    public async Task Failed_operation_leaves_all_json_collections_unchanged()
    {
        await WithTemporaryDirectoryAsync(async directory =>
        {
            var coordinator = new JsonMutationCoordinator(directory);
            var products = new JsonProductRepository(directory);
            var inventory = new JsonInventoryRepository(directory);
            var product = new Product(Guid.NewGuid(), "ROLLBACK-01", "Rolled back item", "Test", "Stationery", 3m, true);

            await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.RunAsync(async () =>
            {
                await products.SaveAllAsync([product]);
                await inventory.SaveAllAsync([new InventoryItem(Guid.NewGuid(), product.Id, 5, 1, [])]);
                throw new InvalidOperationException("Simulated failure before commit.");
            }));

            Assert.Empty(await products.GetAllAsync());
            Assert.Empty(await inventory.GetAllAsync());
            Assert.False(Directory.Exists(Path.Combine(directory, ".apexpost-transaction")));
        });
    }

    [Fact]
    public async Task Initialization_recovers_a_partially_replaced_collection_from_its_journal()
    {
        await WithTemporaryDirectoryAsync(async directory =>
        {
            var repository = new JsonBranchRepository(directory);
            var original = new Branch(Guid.NewGuid(), "AP-OLD", "Original", "North", true);
            var replacement = new Branch(Guid.NewGuid(), "AP-NEW", "Replacement", "South", true);
            await repository.SaveAllAsync([original]);
            var originalBytes = await File.ReadAllBytesAsync(Path.Combine(directory, "branches.json"));
            await repository.SaveAllAsync([replacement]);

            var transactionDirectory = Path.Combine(directory, ".apexpost-transaction");
            Directory.CreateDirectory(transactionDirectory);
            await File.WriteAllBytesAsync(Path.Combine(transactionDirectory, "backup-0000.json"), originalBytes);
            await File.WriteAllTextAsync(Path.Combine(transactionDirectory, "staged-0000.json"), "[]");
            var journal = new
            {
                state = 0,
                entries = new[]
                {
                    new
                    {
                        relativePath = "branches.json",
                        stagedFileName = "staged-0000.json",
                        backupFileName = "backup-0000.json",
                        existedBefore = true
                    }
                }
            };
            await File.WriteAllTextAsync(
                Path.Combine(transactionDirectory, "journal.json"),
                JsonSerializer.Serialize(journal));

            await new JsonMutationCoordinator(directory).InitializeAsync();

            var recovered = Assert.Single(await repository.GetAllAsync());
            Assert.Equal(original.Id, recovered.Id);
            Assert.False(Directory.Exists(transactionDirectory));
        });
    }

    private static async Task WithTemporaryDirectoryAsync(Func<string, Task> test)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"apexpost-mutation-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            await test(directory);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
