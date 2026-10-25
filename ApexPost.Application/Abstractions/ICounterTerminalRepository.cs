using ApexPost.Domain.Entities;

namespace ApexPost.Application.Abstractions;

public interface ICounterTerminalRepository
{
    Task<IReadOnlyList<CounterTerminal>> GetAllAsync(CancellationToken cancellationToken = default);
    Task SaveAllAsync(IEnumerable<CounterTerminal> terminals, CancellationToken cancellationToken = default);
}
