using ApexPost.Application.Abstractions;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;

namespace ApexPost.Tests.UnitOfWork;

// Database-free fakes for application-service tests. They only implement the two core members of each
// repository; the focused members (GetByIdAsync, AddAsync, UpdateAsync, ...) come from the interfaces'
// default implementations.

internal sealed class InMemoryBranchRepository(params Branch[] branches) : IBranchRepository
{
    public List<Branch> Items { get; } = [.. branches];
    public Task<IReadOnlyList<Branch>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Branch>>(Items.ToArray());
    public Task<Branch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(item => item.Id == id));
    public Task SaveAllAsync(IEnumerable<Branch> branches, CancellationToken cancellationToken = default) { var copy = branches.ToArray(); Items.Clear(); Items.AddRange(copy); return Task.CompletedTask; }
}

internal sealed class InMemoryStaffRepository(params StaffMember[] staff) : IStaffRepository
{
    public List<StaffMember> Items { get; } = [.. staff];
    public Task<IReadOnlyList<StaffMember>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<StaffMember>>(Items.ToArray());
    public Task<StaffMember?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(item => item.Email == email));
    public Task SaveAllAsync(IEnumerable<StaffMember> staff, CancellationToken cancellationToken = default) { var copy = staff.ToArray(); Items.Clear(); Items.AddRange(copy); return Task.CompletedTask; }
}

internal sealed class InMemoryProductRepository(params Product[] products) : IProductRepository
{
    public List<Product> Items { get; } = [.. products];
    public Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Product>>(Items.ToArray());
    public Task SaveAllAsync(IEnumerable<Product> products, CancellationToken cancellationToken = default) { var copy = products.ToArray(); Items.Clear(); Items.AddRange(copy); return Task.CompletedTask; }
    public ValueTask<string> GetVersionAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(Items.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
}

internal sealed class InMemoryInventoryRepository(params InventoryItem[] items) : IInventoryRepository
{
    public List<InventoryItem> Items { get; } = [.. items];
    public Task<IReadOnlyList<InventoryItem>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<InventoryItem>>(Items.ToArray());
    public Task SaveAllAsync(IEnumerable<InventoryItem> items, CancellationToken cancellationToken = default) { var copy = items.ToArray(); Items.Clear(); Items.AddRange(copy); return Task.CompletedTask; }
}

internal sealed class InMemoryTransactionRepository : ICounterTransactionRepository
{
    public List<CounterTransaction> Items { get; } = [];
    public Task<IReadOnlyList<CounterTransaction>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CounterTransaction>>(Items.ToArray());
    public Task SaveAllAsync(IEnumerable<CounterTransaction> transactions, CancellationToken cancellationToken = default) { var copy = transactions.ToArray(); Items.Clear(); Items.AddRange(copy); return Task.CompletedTask; }
}

internal sealed class InMemoryTerminalRepository(params CounterTerminal[] terminals) : ICounterTerminalRepository
{
    public List<CounterTerminal> Items { get; } = [.. terminals];
    public Task<IReadOnlyList<CounterTerminal>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CounterTerminal>>(Items.ToArray());
    public Task SaveAllAsync(IEnumerable<CounterTerminal> terminals, CancellationToken cancellationToken = default) { var copy = terminals.ToArray(); Items.Clear(); Items.AddRange(copy); return Task.CompletedTask; }
}

internal sealed class InMemoryMoneyOrderRepository : IMoneyOrderRepository
{
    public List<MoneyOrder> Items { get; } = [];
    public Task<IReadOnlyList<MoneyOrder>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MoneyOrder>>(Items.ToArray());
    public Task SaveAllAsync(IEnumerable<MoneyOrder> moneyOrders, CancellationToken cancellationToken = default) { var copy = moneyOrders.ToArray(); Items.Clear(); Items.AddRange(copy); return Task.CompletedTask; }
}

internal sealed class InMemoryVerificationRepository : IIdentityVerificationRepository
{
    public List<IdentityVerificationRecord> Items { get; } = [];
    public Task<IReadOnlyList<IdentityVerificationRecord>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<IdentityVerificationRecord>>(Items.ToArray());
    public Task SaveAllAsync(IEnumerable<IdentityVerificationRecord> records, CancellationToken cancellationToken = default) { var copy = records.ToArray(); Items.Clear(); Items.AddRange(copy); return Task.CompletedTask; }
}

internal sealed class InMemoryZoneRepository : IShippingZoneRepository
{
    public Task<IReadOnlyList<ShippingZone>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ShippingZone>>([]);
    public Task SaveAllAsync(IEnumerable<ShippingZone> zones, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class InMemoryRateRepository : IShippingRateRepository
{
    public Task<IReadOnlyList<ShippingRate>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ShippingRate>>([]);
    public Task SaveAllAsync(IEnumerable<ShippingRate> rates, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class NullEventPublisher : IApplicationEventPublisher
{
    public Task PublishAsync<TEvent>(TEvent applicationEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class AlwaysFailingIdentityVerification : IIdentityVerificationService
{
    public Task<bool> VerifyAsync(IdentityDocumentType documentType, string documentIdentifier, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}

/// <summary>
/// Spy unit of work: counts commits. ExecuteInTransactionAsync commits exactly once when the use case
/// succeeds and never when it throws - the behaviour EfUnitOfWork guarantees against SQL Server.
/// </summary>
internal sealed class SpyUnitOfWork : IUnitOfWork
{
    private int depth;

    public int Commits { get; private set; }
    public int Transactions { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        Commits++;
        return Task.FromResult(1);
    }

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        depth++;
        if (depth == 1)
        {
            Transactions++;
        }

        try
        {
            var result = await operation(cancellationToken);
            if (depth == 1)
            {
                // Outer use-case boundary: one commit. Nested calls join it.
                await SaveChangesAsync(cancellationToken);
            }

            return result;
        }
        finally
        {
            depth--;
        }
    }
}
