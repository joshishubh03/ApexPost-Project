using ApexPost.Application.Abstractions;
using ApexPost.Domain.Entities;

namespace ApexPost.Infrastructure.Repositories;

public sealed class JsonCounterTransactionRepository(string dataDirectory) : ICounterTransactionRepository
{
    private readonly JsonCollectionStore<CounterTransaction> _store =
        new(Path.Combine(dataDirectory, "counter-transactions.json"));

    public Task<IReadOnlyList<CounterTransaction>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _store.ReadAllAsync(cancellationToken);

    public Task SaveAllAsync(IEnumerable<CounterTransaction> transactions, CancellationToken cancellationToken = default) =>
        _store.WriteAllAsync(transactions, cancellationToken);
}
