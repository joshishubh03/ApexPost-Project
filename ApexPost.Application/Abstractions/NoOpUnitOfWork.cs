namespace ApexPost.Application.Abstractions;

/// <summary>
/// Compatibility no-op unit of work used by services that are also valid in non-persistence scenarios.
/// </summary>
public sealed class NoOpUnitOfWork : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(0);

    public Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return operation(cancellationToken);
    }
}
