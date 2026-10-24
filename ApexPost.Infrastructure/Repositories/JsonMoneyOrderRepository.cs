using ApexPost.Application.Abstractions;
using ApexPost.Domain.Entities;

namespace ApexPost.Infrastructure.Repositories;

public sealed class JsonMoneyOrderRepository(string dataDirectory) : IMoneyOrderRepository
{
    private readonly JsonCollectionStore<MoneyOrder> _store =
        new(Path.Combine(dataDirectory, "money-orders.json"));

    public Task<IReadOnlyList<MoneyOrder>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _store.ReadAllAsync(cancellationToken);

    public Task SaveAllAsync(IEnumerable<MoneyOrder> moneyOrders, CancellationToken cancellationToken = default) =>
        _store.WriteAllAsync(moneyOrders, cancellationToken);
}
