using ApexPost.Application.Abstractions;
using ApexPost.Domain.Entities;

namespace ApexPost.Infrastructure.Repositories;

public sealed class JsonCounterTerminalRepository(string dataDirectory) : ICounterTerminalRepository
{
    private readonly JsonCollectionStore<CounterTerminal> store = new(Path.Combine(dataDirectory, "counter-terminals.json"));
    public Task<IReadOnlyList<CounterTerminal>> GetAllAsync(CancellationToken cancellationToken = default) =>
        store.ReadAllAsync(cancellationToken);

    public Task SaveAllAsync(IEnumerable<CounterTerminal> terminals, CancellationToken cancellationToken = default) =>
        store.WriteAllAsync(terminals, cancellationToken);
}
