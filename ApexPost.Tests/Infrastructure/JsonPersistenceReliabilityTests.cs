using System.Text.Json;
using ApexPost.Application.Abstractions;
using ApexPost.Application.Models;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Infrastructure.Repositories;
using ApexPost.Infrastructure.Seed;

namespace ApexPost.Tests.Infrastructure;

public sealed class JsonPersistenceReliabilityTests
{
    [Fact]
    public async Task Missing_collection_file_is_read_as_empty()
    {
        await WithTemporaryDirectoryAsync(async directory =>
        {
            var repository = new JsonProductRepository(directory);

            var products = await repository.GetAllAsync();

            Assert.Empty(products);
        });
    }

    [Fact]
    public async Task Malformed_collection_json_is_reported_without_overwriting_the_file()
    {
        await WithTemporaryDirectoryAsync(async directory =>
        {
            var path = Path.Combine(directory, "products.json");
            await File.WriteAllTextAsync(path, "{not valid json");
            var repository = new JsonProductRepository(directory);

            await Assert.ThrowsAsync<JsonException>(() => repository.GetAllAsync());
            Assert.Equal("{not valid json", await File.ReadAllTextAsync(path));
        });
    }

    [Fact]
    public async Task Write_io_failure_propagates_and_temporary_file_is_removed()
    {
        await WithTemporaryDirectoryAsync(async directory =>
        {
            var blockedDestination = Path.Combine(directory, "products.json");
            Directory.CreateDirectory(blockedDestination);
            var repository = new JsonProductRepository(directory);
            var product = new Product(Guid.NewGuid(), "SKU-1", "Test product", "Description", "Stationery", 1m, true);

            var exception = await Record.ExceptionAsync(() => repository.SaveAllAsync([product]));

            Assert.NotNull(exception);
            Assert.True(exception is IOException or UnauthorizedAccessException);
            Assert.True(Directory.Exists(blockedDestination));
            Assert.Empty(Directory.EnumerateFiles(directory, "products.json.*.tmp"));
        });
    }

    [Fact]
    public async Task Concurrent_audit_appends_keep_every_record_and_leave_valid_json()
    {
        await WithTemporaryDirectoryAsync(async directory =>
        {
            var repository = new JsonAuditLogRepository(directory);
            var records = Enumerable.Range(0, 40)
                .Select(index => new AuditRecord(
                    Guid.NewGuid(),
                    DateTimeOffset.UtcNow,
                    "ConcurrentWrite",
                    "TestEntity",
                    $"entity-{index}",
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    $"correlation-{index}",
                    null))
                .ToArray();

            await Task.WhenAll(records.Select(record => repository.AppendAsync(record)));

            var storedRecords = await repository.GetAllAsync();
            Assert.Equal(records.Length, storedRecords.Count);
            Assert.True(records.Select(record => record.Id).ToHashSet()
                .SetEquals(storedRecords.Select(record => record.Id)));
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "audit-records.json")));
            Assert.Equal(records.Length, document.RootElement.GetArrayLength());
        });
    }

    [Fact]
    public async Task Development_seed_can_run_twice_without_duplicates()
    {
        await WithTemporaryDirectoryAsync(async directory =>
        {
            var branches = new JsonBranchRepository(directory);
            var staff = new JsonStaffRepository(directory);
            var products = new JsonProductRepository(directory);
            var inventory = new JsonInventoryRepository(directory);
            var transactions = new JsonCounterTransactionRepository(directory);
            var moneyOrders = new JsonMoneyOrderRepository(directory);
            var verifications = new JsonIdentityVerificationRepository(directory);
            var zones = new JsonShippingZoneRepository(directory);
            var rates = new JsonShippingRateRepository(directory);
            var terminals = new JsonCounterTerminalRepository(directory);
            var seeder = new DemoDataSeeder(
                branches,
                staff,
                products,
                inventory,
                transactions,
                moneyOrders,
                verifications,
                zones,
                rates,
                terminals,
                new TestStaffPasswordService(),
                new MoneyOrderPolicy(10_000m, 50m, "DEMO-VALID-"),
                "CounterDemo!2026",
                new JsonMutationCoordinator(directory));

            await seeder.SeedAsync();
            var firstRunCounts = await ReadCountsAsync(
                branches, staff, products, inventory, transactions, moneyOrders, verifications, zones, rates, terminals);
            await seeder.SeedAsync();
            var secondRunCounts = await ReadCountsAsync(
                branches, staff, products, inventory, transactions, moneyOrders, verifications, zones, rates, terminals);

            Assert.Equal(new[] { 5, 20, 30, 150, 40, 10, 5, 10, 30, 10 }, firstRunCounts);
            Assert.Equal(firstRunCounts, secondRunCounts);
            Assert.Contains(TransactionStatus.Voided, (await transactions.GetAllAsync()).Select(item => item.Status));
            Assert.Contains(TransactionStatus.Refunded, (await transactions.GetAllAsync()).Select(item => item.Status));
            Assert.Equal(5, (await verifications.GetAllAsync()).Count);
            Assert.Equal(1, (await moneyOrders.GetAllAsync()).Count(item => item.Status == MoneyOrderStatus.Refunded));
        });
    }

    [Fact]
    public async Task Seed_with_zero_money_order_threshold_does_not_create_unverified_orders()
    {
        await WithTemporaryDirectoryAsync(async directory =>
        {
            var branches = new JsonBranchRepository(directory);
            var staff = new JsonStaffRepository(directory);
            var products = new JsonProductRepository(directory);
            var inventory = new JsonInventoryRepository(directory);
            var transactions = new JsonCounterTransactionRepository(directory);
            var moneyOrders = new JsonMoneyOrderRepository(directory);
            var verifications = new JsonIdentityVerificationRepository(directory);
            var zones = new JsonShippingZoneRepository(directory);
            var rates = new JsonShippingRateRepository(directory);
            var terminals = new JsonCounterTerminalRepository(directory);
            var seeder = new DemoDataSeeder(
                branches,
                staff,
                products,
                inventory,
                transactions,
                moneyOrders,
                verifications,
                zones,
                rates,
                terminals,
                new TestStaffPasswordService(),
                new MoneyOrderPolicy(0m, 50m, "DEMO-VALID-"),
                "CounterDemo!2026",
                new JsonMutationCoordinator(directory));

            await seeder.SeedAsync();

            var seededOrders = await moneyOrders.GetAllAsync();
            Assert.NotEmpty(seededOrders);
            Assert.All(seededOrders, order => Assert.Equal(IdentityVerificationStatus.Verified, order.VerificationStatus));
        });
    }

    private static async Task<int[]> ReadCountsAsync(
        JsonBranchRepository branches,
        JsonStaffRepository staff,
        JsonProductRepository products,
        JsonInventoryRepository inventory,
        JsonCounterTransactionRepository transactions,
        JsonMoneyOrderRepository moneyOrders,
        JsonIdentityVerificationRepository verifications,
        JsonShippingZoneRepository zones,
        JsonShippingRateRepository rates,
        JsonCounterTerminalRepository terminals) =>
    [
        (await branches.GetAllAsync()).Count,
        (await staff.GetAllAsync()).Count,
        (await products.GetAllAsync()).Count,
        (await inventory.GetAllAsync()).Count,
        (await transactions.GetAllAsync()).Count,
        (await moneyOrders.GetAllAsync()).Count,
        (await verifications.GetAllAsync()).Count,
        (await zones.GetAllAsync()).Count,
        (await rates.GetAllAsync()).Count,
        (await terminals.GetAllAsync()).Count
    ];

    private static async Task WithTemporaryDirectoryAsync(Func<string, Task> action)
    {
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"apexpost-reliability-{Guid.NewGuid():N}"));
        var temporaryRoot = Path.GetFullPath(Path.GetTempPath());
        var relativePath = Path.GetRelativePath(temporaryRoot, directory);
        if (Path.IsPathRooted(relativePath) || relativePath.StartsWith("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The test directory must remain inside the system temporary directory.");
        }

        Directory.CreateDirectory(directory);
        try
        {
            await action(directory);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private sealed class TestStaffPasswordService : IStaffPasswordService
    {
        public string Hash(string password) => $"test-hash:{password}";

        public bool Verify(StaffMember staff, string password) =>
            string.Equals(staff.PasswordHash, Hash(password), StringComparison.Ordinal);
    }
}
