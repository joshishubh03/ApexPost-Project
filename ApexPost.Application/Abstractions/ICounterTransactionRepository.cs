using ApexPost.Domain.Entities;

namespace ApexPost.Application.Abstractions;

/// <summary>
/// Aggregate repository for counter transactions (lines, payment and shipment). Implementations stage
/// changes only; the unit of work commits. No member returns IQueryable.
/// </summary>
public interface ICounterTransactionRepository
{
    Task<IReadOnlyList<CounterTransaction>> GetAllAsync(CancellationToken cancellationToken = default);
    Task SaveAllAsync(IEnumerable<CounterTransaction> transactions, CancellationToken cancellationToken = default);

    async Task<CounterTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        (await GetAllAsync(cancellationToken)).FirstOrDefault(transaction => transaction.Id == id);

    async Task<IReadOnlyList<CounterTransaction>> GetRecentForBranchAsync(
        Guid branchId,
        int take,
        CancellationToken cancellationToken = default) =>
        (await GetAllAsync(cancellationToken))
            .Where(transaction => transaction.BranchId == branchId)
            .OrderByDescending(transaction => transaction.OccurredAt)
            .Take(take)
            .ToArray();

    async Task AddAsync(CounterTransaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        var all = (await GetAllAsync(cancellationToken)).ToList();
        all.Add(transaction);
        await SaveAllAsync(all, cancellationToken);
    }

    async Task UpdateAsync(CounterTransaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        var all = (await GetAllAsync(cancellationToken)).ToList();
        var index = all.FindIndex(candidate => candidate.Id == transaction.Id);
        if (index < 0)
            throw new KeyNotFoundException("The transaction could not be found.");
        all[index] = transaction;
        await SaveAllAsync(all, cancellationToken);
    }
}
