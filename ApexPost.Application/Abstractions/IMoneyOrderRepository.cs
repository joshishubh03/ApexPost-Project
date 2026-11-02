using ApexPost.Domain.Entities;

namespace ApexPost.Application.Abstractions;

public interface IMoneyOrderRepository
{
    Task<IReadOnlyList<MoneyOrder>> GetAllAsync(CancellationToken cancellationToken = default);
    Task SaveAllAsync(IEnumerable<MoneyOrder> moneyOrders, CancellationToken cancellationToken = default);

    async Task<MoneyOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        (await GetAllAsync(cancellationToken)).FirstOrDefault(order => order.Id == id);

    async Task<IReadOnlyList<MoneyOrder>> GetRecentForBranchAsync(
        Guid branchId,
        int take,
        CancellationToken cancellationToken = default) =>
        (await GetAllAsync(cancellationToken))
            .Where(order => order.BranchId == branchId)
            .OrderByDescending(order => order.OccurredAt)
            .Take(take)
            .ToArray();

    async Task AddAsync(MoneyOrder order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        var all = (await GetAllAsync(cancellationToken)).ToList();
        all.Add(order);
        await SaveAllAsync(all, cancellationToken);
    }

    async Task UpdateAsync(MoneyOrder order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        var all = (await GetAllAsync(cancellationToken)).ToList();
        var index = all.FindIndex(candidate => candidate.Id == order.Id);
        if (index < 0)
            throw new KeyNotFoundException("The money order could not be found.");
        all[index] = order;
        await SaveAllAsync(all, cancellationToken);
    }
}
