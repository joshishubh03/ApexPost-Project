using ApexPost.Domain.Entities;

namespace ApexPost.Application.Abstractions;

public interface ICounterTransactionRepository
{
    Task<IReadOnlyList<CounterTransaction>> GetAllAsync(CancellationToken cancellationToken = default);
    Task SaveAllAsync(IEnumerable<CounterTransaction> transactions, CancellationToken cancellationToken = default);
}
